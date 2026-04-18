namespace ActDefend.Core.Models;

/// <summary>
/// A snapshot of per-process behavioural features computed over one or more sliding windows.
/// Produced by the Feature Extractor and consumed by the Stage 1 Scoring Engine.
/// All values represent observations within the configured time windows.
/// </summary>
public sealed record FeatureSnapshot
{
    /// <summary>UTC time this snapshot was emitted.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>OS process ID.</summary>
    public required int ProcessId { get; init; }

    /// <summary>Short image name (e.g. "suspicious.exe").</summary>
    public required string ProcessName { get; init; }

    /// <summary>Full executable path if resolved, or null.</summary>
    public string? ProcessPath { get; init; }

    // ── Primary window metrics (short burst window) ──────────────────────────

    /// <summary>Write events per second in the primary window.</summary>
    public double WriteRatePerSec { get; init; }

    /// <summary>Number of distinct file paths written in the primary window.</summary>
    public int UniqueFilesWritten { get; init; }

    /// <summary>Rename events per second in the primary window.</summary>
    public double RenameRatePerSec { get; init; }

    /// <summary>Number of distinct directories touched (any operation) in the primary window.</summary>
    public int UniqueDirectoriesTouched { get; init; }

    /// <summary>
    /// Critical context modifier: Extracted purely from Writes/Renames/Deletes bounded against 
    /// paths that WERE NOT CREATED by this active process window. High bounds indicate modifying 
    /// existing user-data directly instead of just extracting installation caches natively.
    /// </summary>
    public double PreExistingModifyRatePerSec { get; init; }

    /// <summary>
    /// Write-to-read ratio in the primary window.
    /// High values indicate write-heavy behaviour with little reading — a ransomware pattern.
    /// Set to <see cref="double.MaxValue"/> when read count is zero and write count is positive.
    /// </summary>
    public double WriteReadRatio { get; init; }

    // ── Context window metrics (wider stabilisation window) ──────────────────

    /// <summary>Total write events observed in the context window.</summary>
    public int TotalWritesInContext { get; init; }

    /// <summary>Total rename events in the context window.</summary>
    public int TotalRenamesInContext { get; init; }

    // ── Metadata ─────────────────────────────────────────────────────────────

    /// <summary>Duration of the primary window this snapshot covers.</summary>
    public TimeSpan PrimaryWindowDuration { get; init; }

    /// <summary>Duration of the context window this snapshot covers.</summary>
    public TimeSpan ContextWindowDuration { get; init; }

    /// <summary>
    /// Most recently written file paths (bounded, for Stage 2 entropy sampling candidates).
    /// Contains the original write-event paths — may have been renamed away.
    /// Stage 2 will also try common ransomware extensions if the path is no longer readable.
    /// </summary>
    public IReadOnlyList<string> RecentWrittenFiles { get; init; } = [];

    /// <summary>
    /// Source paths of the most recent rename events (bounded).
    /// In ransomware patterns, these are the original file names before extension substitution.
    /// Stage 2 uses these paths (with extension probing) as additional entropy candidates.
    /// </summary>
    public IReadOnlyList<string> RecentRenamedSourceFiles { get; init; } = [];
}
