using ActDefend.Core.Models;

namespace ActDefend.Core.Interfaces;

/// <summary>
/// Contract for the Stage 1 lightweight scoring engine.
/// Evaluates a <see cref="FeatureSnapshot"/> and produces a <see cref="ScoringResult"/>
/// with a numeric suspicion score and a human-readable explanation.
///
/// Design rules (from brief §10):
/// - Always on; called on every feature snapshot emit.
/// - Must remain low-cost (no I/O, no heavy computation).
/// - Thresholds and weights are configured externally.
/// - Every result must include a per-feature breakdown for explainability.
/// </summary>
public interface IScoringEngine
{
    /// <summary>
    /// Evaluates the provided feature snapshot and returns a scoring result.
    /// </summary>
    ScoringResult Score(FeatureSnapshot snapshot);
}
