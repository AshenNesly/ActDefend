namespace ActDefend.Core.Models;

/// <summary>
/// Result produced by the Stage 1 lightweight scoring engine for a single process.
/// Contains the numeric score, the triggering feature values, and a human-readable
/// explanation — ensuring the decision is always explainable.
/// </summary>
public sealed record ScoringResult
{
    /// <summary>UTC time the score was computed.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>The feature snapshot this result was derived from.</summary>
    public required FeatureSnapshot Snapshot { get; init; }

    /// <summary>
    /// Composite suspicion score in the range [0, 100].
    /// Threshold is configurable; default trigger is 60.
    /// </summary>
    public required double Score { get; init; }

    /// <summary>True when Score meets or exceeds the configured suspicion threshold.</summary>
    public required bool IsSuspicious { get; init; }

    /// <summary>
    /// Per-feature contribution breakdown.
    /// Key = feature name, Value = score contribution (0–100 partial).
    /// Used for explainability and logging.
    /// </summary>
    public IReadOnlyDictionary<string, double> FeatureContributions { get; init; }
        = new Dictionary<string, double>();

    /// <summary>Human-readable summary of why this process was scored as suspicious.</summary>
    public string Explanation { get; init; } = string.Empty;
}
