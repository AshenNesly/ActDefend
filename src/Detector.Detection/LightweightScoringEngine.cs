using System.Text;
using ActDefend.Core.Configuration;
using ActDefend.Core.Interfaces;
using ActDefend.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActDefend.Detection;

/// <summary>
/// Stage 1 lightweight scoring engine.
///
/// Computes real weighted-rule scoring against:
/// - Per-feature normalisation against configured upper bounds
/// - Weighted sum → composite suspicion score [0–100]
/// - Per-feature contribution breakdown for explainability
/// </summary>
public sealed class LightweightScoringEngine : IScoringEngine
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
        var contributions = new Dictionary<string, double>();
        double totalScore = 0.0;

        // Calculate independent contributions capped linearly by Thresholds.
        var writeRateContrib = ComputeContribution(
            snapshot.WriteRatePerSec, 
            _options.Thresholds.WriteRatePerSec, 
            _options.Weights.WriteRate);
        contributions["WriteRate"] = writeRateContrib;
        totalScore += writeRateContrib;

        var uniqueFilesContrib = ComputeContribution(
            snapshot.UniqueFilesWritten, 
            _options.Thresholds.UniqueFilesPerWindow, 
            _options.Weights.UniqueFilesWritten);
        contributions["UniqueFilesWritten"] = uniqueFilesContrib;
        totalScore += uniqueFilesContrib;

        var renameRateContrib = ComputeContribution(
            snapshot.RenameRatePerSec, 
            _options.Thresholds.RenameRatePerSec, 
            _options.Weights.RenameRate);
        contributions["RenameRate"] = renameRateContrib;
        totalScore += renameRateContrib;

        var dirSpreadContrib = ComputeContribution(
            snapshot.UniqueDirectoriesTouched, 
            _options.Thresholds.UniqueDirectoriesPerWindow, 
            _options.Weights.DirectorySpread);
        contributions["DirectorySpread"] = dirSpreadContrib;
        totalScore += dirSpreadContrib;

        var writeReadContrib = ComputeContribution(
            snapshot.WriteReadRatio == double.MaxValue ? _options.Thresholds.WriteReadRatioMax : snapshot.WriteReadRatio, 
            _options.Thresholds.WriteReadRatioMax, 
            _options.Weights.WriteReadRatio);
        contributions["WriteReadRatio"] = writeReadContrib;
        totalScore += writeReadContrib;

        var preExistingContrib = ComputeContribution(
            snapshot.PreExistingModifyRatePerSec,
            _options.Thresholds.PreExistingModifyRatePerSec,
            _options.Weights.PreExistingModifyRate);
        contributions["PreExistingModifyRate"] = preExistingContrib;
        totalScore += preExistingContrib;

        // Cap strictly at 100 for percentage scale
        totalScore = Math.Min(totalScore, 100.0);
        
        var isSuspicious = totalScore >= _options.SuspicionThreshold;

        return new ScoringResult
        {
            Timestamp     = DateTimeOffset.UtcNow,
            Snapshot      = snapshot,
            Score         = totalScore,
            IsSuspicious  = isSuspicious,
            Explanation   = BuildExplanation(totalScore, isSuspicious, contributions),
            FeatureContributions = contributions
        };
    }

    private static double ComputeContribution(double value, double threshold, double maxWeight)
    {
        if (threshold <= 0) return 0;
        
        // Capped normalization string up to 1.0 (exceeding threshold gives max weight)
        var ratio = Math.Min(value / threshold, 1.0);
        return ratio * maxWeight;
    }

    private string BuildExplanation(double totalScore, bool isSuspicious, Dictionary<string, double> contributions)
    {
        if (totalScore == 0) return "No significant metrics detected in window.";

        var sb = new StringBuilder();
        sb.Append($"Score: {totalScore:F1} ");
        sb.Append(isSuspicious ? "(Exceeds Threshold). " : "(Under Threshold). ");
        
        // Find top contributors
        var top = contributions
            .Where(c => c.Value > 0)
            .OrderByDescending(c => c.Value)
            .Take(3); // Log top 3 drivers

        sb.Append("Top contributors: ");
        sb.AppendJoin(", ", top.Select(c => $"{c.Key} ({c.Value:F1}pts)"));

        return sb.ToString();
    }
}
