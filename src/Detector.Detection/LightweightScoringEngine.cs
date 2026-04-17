using ActDefend.Core.Configuration;
using ActDefend.Core.Interfaces;
using ActDefend.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActDefend.Detection;

/// <summary>
/// Placeholder Stage 1 lightweight scoring engine for Phase 1.
///
/// Phase 4 replaces this with the real weighted-rule scoring:
/// - Per-feature normalisation against configured upper bounds
/// - Weighted sum → composite suspicion score [0–100]
/// - Per-feature contribution breakdown for explainability
/// - Configurable threshold trigger for Stage 2
///
/// This placeholder returns a zero score for every snapshot so the
/// DI graph compiles and the detection orchestrator skeleton works.
/// </summary>
internal sealed class LightweightScoringEngine : IScoringEngine
{
    private readonly ILogger<LightweightScoringEngine> _logger;
    private readonly Stage1Options _options;

    public LightweightScoringEngine(
        ILogger<LightweightScoringEngine> logger,
        IOptions<ActDefendOptions> options)
    {
        _logger  = logger;
        _options = options.Value.Stage1;
    }

    /// <inheritdoc />
    public ScoringResult Score(FeatureSnapshot snapshot)
    {
        // Phase 4: compute real weighted score from snapshot features.
        _logger.LogTrace("[PLACEHOLDER] Scoring PID={Pid} ({Name}) — returning score=0 in Phase 1.",
            snapshot.ProcessId, snapshot.ProcessName);

        return new ScoringResult
        {
            Timestamp     = DateTimeOffset.UtcNow,
            Snapshot      = snapshot,
            Score         = 0.0,
            IsSuspicious  = false,
            Explanation   = "Phase 1 placeholder — real scoring implemented in Phase 4.",
            FeatureContributions = new Dictionary<string, double>
            {
                ["WriteRate"]          = 0.0,
                ["UniqueFilesWritten"] = 0.0,
                ["RenameRate"]         = 0.0,
                ["DirectorySpread"]    = 0.0,
                ["WriteReadRatio"]     = 0.0
            }
        };
    }
}
