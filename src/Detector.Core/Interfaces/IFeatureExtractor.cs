using ActDefend.Core.Models;

namespace ActDefend.Core.Interfaces;

/// <summary>
/// Contract for the feature extraction component.
/// Consumes a stream of <see cref="FileSystemEvent"/> records, maintains per-process
/// sliding-window state, and periodically emits <see cref="FeatureSnapshot"/> objects.
///
/// Design rules (from brief §8, §9):
/// - State must be bounded: inactive processes are expired.
/// - Sliding windows are configurable in appsettings.json.
/// - No detection logic lives here — only measurement.
/// </summary>
public interface IFeatureExtractor
{
    /// <summary>
    /// Processes a single normalized event, updating the relevant process state.
    /// Must be fast; called for every event on the collector hot path.
    /// </summary>
    void ProcessEvent(FileSystemEvent evt);

    /// <summary>
    /// Returns a snapshot of current behavioural features for all processes
    /// that have seen activity since the last emit. Resets burst counters.
    /// Called by the orchestrator on the configured emit interval.
    /// </summary>
    IReadOnlyList<FeatureSnapshot> Emit();

    /// <summary>
    /// Expires and removes state for processes that have been inactive
    /// longer than the configured inactivity threshold.
    /// </summary>
    void ExpireInactiveState();

    /// <summary>Number of process contexts currently tracked.</summary>
    int ActiveProcessCount { get; }
}
