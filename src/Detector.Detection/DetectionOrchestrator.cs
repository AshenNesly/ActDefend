using ActDefend.Core.Interfaces;
using ActDefend.Core.Models;
using Microsoft.Extensions.Logging;

namespace ActDefend.Detection;

/// <summary>
/// Detection orchestrator — coordinates the full pipeline:
///   FeatureExtractor (emits) → ScoringEngine (Stage 1) → EntropyEngine (Stage 2)
///   → AlertPublisher → Storage
///
/// This is the central coordinator. It does NOT contain detection logic itself;
/// it delegates to the focused subsystems via their interfaces.
///
/// Phase 4 will activate the real scoring loop.
/// Phase 5 will activate Stage 2 entropy confirmation.
/// This Phase 1 skeleton wires the service graph and logs pipeline state.
/// </summary>
public sealed class DetectionOrchestrator
{
    private readonly ILogger<DetectionOrchestrator> _logger;
    private readonly IFeatureExtractor   _extractor;
    private readonly IScoringEngine      _scorer;
    private readonly IEntropyEngine      _entropy;
    private readonly IAlertPublisher     _publisher;
    private readonly IAlertRepository    _alerts;

    public DetectionOrchestrator(
        ILogger<DetectionOrchestrator> logger,
        IFeatureExtractor  extractor,
        IScoringEngine     scorer,
        IEntropyEngine     entropy,
        IAlertPublisher    publisher,
        IAlertRepository   alerts)
    {
        _logger    = logger;
        _extractor = extractor;
        _scorer    = scorer;
        _entropy   = entropy;
        _publisher = publisher;
        _alerts    = alerts;
    }

    /// <summary>
    /// Main processing tick — called periodically by the pipeline host service.
    /// Emits feature snapshots, scores them, and triggers Stage 2 where appropriate.
    /// </summary>
    public async Task TickAsync(CancellationToken cancellationToken)
    {
        var snapshots = _extractor.Emit();

        foreach (var snapshot in snapshots)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = _scorer.Score(snapshot);

            if (!result.IsSuspicious)
                continue;

            _logger.LogInformation(
                "Stage 1 suspicion — PID={Pid} ({Name}) Score={Score:F1} [{Reason}]",
                snapshot.ProcessId, snapshot.ProcessName, result.Score, result.Explanation);

            // Stage 2: only check if entropy engine is past its cooldown for this process.
            if (!_entropy.IsReady(snapshot.ProcessId))
            {
                _logger.LogDebug("Stage 2 skipped for PID={Pid}: cooldown active.", snapshot.ProcessId);
                continue;
            }

            var entropyResult = await _entropy.AnalyseAsync(result, cancellationToken)
                                              .ConfigureAwait(false);

            if (!entropyResult.IsConfirmed)
            {
                _logger.LogInformation(
                    "Stage 2 — PID={Pid} NOT confirmed. AvgEntropy={Avg:F2}. {Reason}",
                    snapshot.ProcessId, entropyResult.AverageEntropy, entropyResult.Explanation);
                continue;
            }

            // Both stages confirm — raise alert.
            var alert = BuildAlert(result, entropyResult);

            await _alerts.SaveAsync(alert, cancellationToken).ConfigureAwait(false);
            _publisher.Publish(alert);

            _logger.LogWarning(
                "DETECTION ALERT — PID={Pid} ({Name}) Severity={Severity} AlertId={Id}",
                alert.ProcessId, alert.ProcessName, alert.Severity, alert.AlertId);
        }

        _extractor.ExpireInactiveState();
    }

    private static DetectionAlert BuildAlert(ScoringResult s1, EntropyResult s2)
    {
        var severity = s1.Score switch
        {
            >= 90 => AlertSeverity.Critical,
            >= 75 => AlertSeverity.High,
            >= 60 => AlertSeverity.Medium,
            _     => AlertSeverity.Low
        };

        return new DetectionAlert
        {
            AlertId       = Guid.NewGuid(),
            Timestamp     = DateTimeOffset.UtcNow,
            ProcessId     = s1.Snapshot.ProcessId,
            ProcessName   = s1.Snapshot.ProcessName,
            ProcessPath   = s1.Snapshot.ProcessPath,
            Severity      = severity,
            Stage1Result  = s1,
            Stage2Result  = s2,
            AffectedFileCount = s1.Snapshot.UniqueFilesWritten,
            Summary       = $"{s1.Snapshot.ProcessName} (PID {s1.Snapshot.ProcessId}) — " +
                            $"S1={s1.Score:F1} S2=confirmed AvgEntropy={s2.AverageEntropy:F2}",
            CorrelationId = Guid.NewGuid()
        };
    }
}
