namespace ActDefend.Core.Models;

/// <summary>
/// Result produced by the Stage 2 entropy-sampling confirmation engine.
/// Records which files were sampled, their entropy values, and the final
/// confirmation decision — all for explainability and logging.
/// </summary>
public sealed record EntropyResult
{
    /// <summary>UTC time sampling completed.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Process ID that triggered Stage 2.</summary>
    public required int ProcessId { get; init; }

    /// <summary>Short image name of the process.</summary>
    public required string ProcessName { get; init; }

    /// <summary>True when entropy sampling confirms ransomware-like activity.</summary>
    public required bool IsConfirmed { get; init; }

    /// <summary>Per-file entropy measurements collected during this run.</summary>
    public IReadOnlyList<FileSample> Samples { get; init; } = [];

    /// <summary>Average entropy across all sampled files.</summary>
    public double AverageEntropy { get; init; }

    /// <summary>Number of files whose entropy exceeded the configured threshold.</summary>
    public int HighEntropyFileCount { get; init; }

    /// <summary>Human-readable explanation of the Stage 2 decision.</summary>
    public string Explanation { get; init; } = string.Empty;

    /// <summary>Stage 1 scoring result that triggered this Stage 2 run.</summary>
    public required ScoringResult TriggerResult { get; init; }
}

/// <summary>Entropy measurement for a single sampled file.</summary>
public sealed record FileSample(
    string FilePath,
    long   BytesSampled,
    double ShannonEntropy,
    bool   ExceedsThreshold);
