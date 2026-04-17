using ActDefend.App.Services;
using ActDefend.Core.Configuration;
using ActDefend.Core.Interfaces;
using ActDefend.Detection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActDefend.App.Services;

/// <summary>
/// The central pipeline host service — implements IHostedService and coordinates
/// the collector → feature-extractor → detection-orchestrator loop.
///
/// Lifecycle:
///   StartAsync: verify elevation, start collector, start processing loop
///   StopAsync:  stop processing loop, stop collector, flush state
///
/// Phase 1 behaviour:
///   - Elevation check runs and is logged.
///   - Collector is started (placeholder: no ETW events yet).
///   - Processing loop runs on the configured emit interval.
///   - No real events flow yet — the pipeline is idle but healthy.
/// </summary>
public sealed class PipelineHostService : BackgroundService
{
    private readonly ILogger<PipelineHostService> _logger;
    private readonly IEventCollector        _collector;
    private readonly IFeatureExtractor      _extractor;
    private readonly DetectionOrchestrator  _orchestrator;
    private readonly MonitoringStatusService _status;
    private readonly FeaturesOptions        _featuresOpts;

    // Tracks the last cumulative dropped-event count synced to MonitoringStatusService
    // so we can compute a delta on each tick instead of re-syncing the whole total.
    private long _lastReportedDropped;

    public PipelineHostService(
        ILogger<PipelineHostService> logger,
        IEventCollector       collector,
        IFeatureExtractor     extractor,
        DetectionOrchestrator orchestrator,
        MonitoringStatusService status,
        IOptions<ActDefendOptions> options)
    {
        _logger       = logger;
        _collector    = collector;
        _extractor    = extractor;
        _orchestrator = orchestrator;
        _status       = status;
        _featuresOpts = options.Value.Features;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PipelineHostService starting.");

        // Check elevation and record status.
        var elevated = ActDefend.Core.Elevation.ElevationHelper.IsElevated();
        _status.SetElevated(elevated);

        if (!elevated)
        {
            _logger.LogError(
                "Process is NOT elevated. ETW monitoring requires Administrator privileges. " +
                "Monitoring will not start. Please restart as Administrator.");
            // Do not start collector or loop — wait for shutdown.
            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
            return;
        }

        _logger.LogInformation("Process is elevated. Starting ETW collector.");

        try
        {
            await _collector.StartAsync(stoppingToken).ConfigureAwait(false);
            _status.SetCollectorRunning(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to start ETW collector. Monitoring will not run. Error: {Message}", ex.Message);
            _status.SetCollectorRunning(false);
            return;
        }

        // Run the event-reading loop and the orchestration tick loop concurrently.
        var readTask  = RunEventReadLoopAsync(stoppingToken);
        var tickTask  = RunOrchestrationLoopAsync(stoppingToken);

        await Task.WhenAll(readTask, tickTask).ConfigureAwait(false);

        _logger.LogInformation("PipelineHostService stopping. Flushing state.");
        await _collector.StopAsync(CancellationToken.None).ConfigureAwait(false);
        _status.SetCollectorRunning(false);
    }

    /// <summary>
    /// Continuously reads events from the collector and feeds them to the feature extractor.
    /// This is the hot path — keep it lean.
    /// </summary>
    private async Task RunEventReadLoopAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Event read loop started.");
        try
        {
            await foreach (var evt in _collector.ReadEventsAsync(stoppingToken)
                                                .ConfigureAwait(false))
            {
                _extractor.ProcessEvent(evt);
                _status.IncrementEventsProcessed();
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown path.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Event read loop terminated unexpectedly: {Message}", ex.Message);
        }
        _logger.LogDebug("Event read loop exited.");
    }

    /// <summary>
    /// Periodically triggers the detection orchestrator on the emit interval.
    /// Decoupled from the event read loop so a slow tick does not block event ingestion.
    /// </summary>
    private async Task RunOrchestrationLoopAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(_featuresOpts.EmitIntervalSeconds);
        _logger.LogDebug("Orchestration loop started. Interval={Interval}s", interval.TotalSeconds);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
                _status.SetActiveProcessCount(_extractor.ActiveProcessCount);

                await _orchestrator.TickAsync(stoppingToken).ConfigureAwait(false);

                // Sync dropped-event counter from collector to the shared status service.
                // The collector holds a cumulative absolute counter; we derive the delta per tick.
                var dropped = _collector.DroppedEventCount;
                if (dropped > _lastReportedDropped)
                {
                    var newDrops = (int)(dropped - _lastReportedDropped);
                    for (int i = 0; i < newDrops; i++)
                        _status.IncrementEventsDropped();
                    _lastReportedDropped = dropped;
                    _logger.LogWarning("Collector dropped {New} event(s) this tick (total: {Total})", newDrops, dropped);
                }

                // Dynamically sync status back to UI in case the underlying ETW session crashed silently.
                if (!_collector.IsRunning)
                {
                    _status.SetCollectorRunning(false);
                    _logger.LogWarning("Collector is no longer running. Monitoring pipeline is halted.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown path.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Orchestration loop terminated unexpectedly: {Message}", ex.Message);
        }
        _logger.LogDebug("Orchestration loop exited.");
    }
}
