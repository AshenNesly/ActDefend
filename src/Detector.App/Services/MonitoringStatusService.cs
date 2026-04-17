using ActDefend.Core.Interfaces;
using ActDefend.Core.Models;
using Microsoft.Extensions.Logging;

namespace ActDefend.App.Services;

/// <summary>
/// Concrete implementation of IMonitoringStatus.
/// Provides the GUI with a live read-only view of pipeline health
/// without coupling the GUI to any pipeline component directly.
/// Thread-safe: all mutations use Interlocked or volatile.
/// </summary>
public sealed class MonitoringStatusService : IMonitoringStatus
{
    private readonly ILogger<MonitoringStatusService> _logger;
    private volatile bool _collectorRunning;
    private volatile bool _elevated;
    private long _eventsProcessed;
    private long _eventsDropped;
    private int  _activeProcessCount;

    public MonitoringStatusService(ILogger<MonitoringStatusService> logger)
    {
        _logger = logger;
    }

    // ── IMonitoringStatus ────────────────────────────────────────────────────

    public bool IsCollectorRunning => _collectorRunning;
    public bool IsElevated         => _elevated;
    public int  ActiveProcessCount => Volatile.Read(ref _activeProcessCount);
    public long TotalEventsProcessed => Interlocked.Read(ref _eventsProcessed);
    public long TotalEventsDropped   => Interlocked.Read(ref _eventsDropped);
    public DateTimeOffset? StartedAt { get; private set; }

    public event EventHandler? StatusChanged;

    // ── Mutation methods (called by PipelineHostService) ─────────────────────

    public void SetElevated(bool elevated)
    {
        _elevated = elevated;
        RaiseChanged();
    }

    public void SetCollectorRunning(bool running)
    {
        _collectorRunning = running;
        if (running) StartedAt = DateTimeOffset.UtcNow;
        _logger.LogInformation("Collector status changed: IsRunning={Running}", running);
        RaiseChanged();
    }

    public void IncrementEventsProcessed()   => Interlocked.Increment(ref _eventsProcessed);
    public void IncrementEventsDropped()     => Interlocked.Increment(ref _eventsDropped);
    public void SetActiveProcessCount(int n) => Volatile.Write(ref _activeProcessCount, n);

    private void RaiseChanged()
        => StatusChanged?.Invoke(this, EventArgs.Empty);
}
