using ActDefend.Core.Models;

namespace ActDefend.Core.Interfaces;

/// <summary>
/// Contract for the Stage 2 entropy sampling and confirmation engine.
/// Only invoked when Stage 1 scoring crosses the suspicion threshold.
///
/// Design rules (from brief §10):
/// - Must NOT run continuously.
/// - Sampling is bounded: limited file count and byte count per run.
/// - Must respect the configured cooldown between runs per process.
/// - Must record all sampling decisions for explainability and logging.
/// - CPU impact must be controlled at all times.
/// </summary>
public interface IEntropyEngine
{
    /// <summary>
    /// Runs entropy sampling on recently written files for the given suspicious process.
    /// Returns a confirmation result regardless of whether sampling confirms or refutes.
    /// 
    /// This call may perform file I/O; it must be awaited off the hot event path.
    /// </summary>
    /// <param name="result">The Stage 1 scoring result that triggered this Stage 2 check.</param>
    /// <param name="cancellationToken">Token to observe for shutdown.</param>
    Task<EntropyResult> AnalyseAsync(ScoringResult result, CancellationToken cancellationToken);

    /// <summary>
    /// Returns true when the engine is ready to check the given process
    /// i.e. outside the configured cooldown period for that process.
    /// </summary>
    bool IsReady(int processId);
}
