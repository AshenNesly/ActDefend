using ActDefend.Core.Configuration;
using ActDefend.Core.Models;
using ActDefend.Detection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ActDefend.UnitTests.Detection;

public sealed class ScoringEngineTests
{
    private readonly ActDefendOptions _opts;
    private readonly LightweightScoringEngine _engine;

    public ScoringEngineTests()
    {
        _opts = new ActDefendOptions
        {
            Stage1 = new Stage1Options
            {
                SuspicionThreshold = 60.0,
                Weights = new Stage1Weights
                {
                    WriteRate = 20.0,
                    UniqueFilesWritten = 20.0,
                    RenameRate = 25.0,
                    DirectorySpread = 20.0,
                    WriteReadRatio = 15.0
                },
                Thresholds = new Stage1Thresholds
                {
                    WriteRatePerSec = 10.0,
                    UniqueFilesPerWindow = 30,
                    RenameRatePerSec = 5.0,
                    UniqueDirectoriesPerWindow = 10,
                    WriteReadRatioMax = 5.0
                }
            }
        };

        _engine = new LightweightScoringEngine(
            NullLogger<LightweightScoringEngine>.Instance,
            Options.Create(_opts));
    }

    [Fact]
    public void Score_ZeroSnapshot_ReturnsZeroScore()
    {
        var snapshot = BuildSnapshot();
        var result = _engine.Score(snapshot);

        result.Score.Should().Be(0.0);
        result.IsSuspicious.Should().BeFalse();
        result.FeatureContributions.Values.All(v => v == 0.0).Should().BeTrue();
    }

    [Fact]
    public void Score_MaxesOut_WhenExceedingAllThresholds()
    {
        var snapshot = BuildSnapshot(
            writeRate: 50.0, 
            uniqueFiles: 100, 
            renameRate: 20.0, 
            uniqueDirs: 50, 
            writeReadRatio: 10.0);

        var result = _engine.Score(snapshot);

        result.Score.Should().Be(100.0);
        result.IsSuspicious.Should().BeTrue();
    }

    [Fact]
    public void Score_PartialMatches_ScaleLinearly()
    {
        // Half threshold on WriteRate, and zero on others
        var snapshot = BuildSnapshot(writeRate: 5.0); 

        var result = _engine.Score(snapshot);

        result.Score.Should().Be(10.0); // 5.0 / 10.0 threshold = 50% of 20pt max weight
        result.IsSuspicious.Should().BeFalse();
    }

    [Fact]
    public void Score_TriggersSuspicious_WhenExceedingThreshold()
    {
        // Only aggressive rename operations mapping to > 60 points
        var snapshot = BuildSnapshot(
            renameRate: 5.0, // 1.0 * 25pts = 25pts
            uniqueFiles: 30, // 1.0 * 20pts = 20pts
            uniqueDirs: 10); // 1.0 * 20pts = 20pts

        var result = _engine.Score(snapshot);

        result.Score.Should().Be(65.0);
        result.IsSuspicious.Should().BeTrue();
    }

    [Fact]
    public void Score_HandlesDoubleMaxValue_InWriteReadRatioSafely()
    {
        var snapshot = BuildSnapshot(
            writeReadRatio: double.MaxValue); 

        var result = _engine.Score(snapshot);

        // Max val ratio should just map mathematically cleanly to max component contribution.
        // It shouldn't crash or go infinite.
        result.Score.Should().Be(15.0); // Max weight for ratio
        result.IsSuspicious.Should().BeFalse();
    }

    private static FeatureSnapshot BuildSnapshot(
        double writeRate = 0,
        int uniqueFiles = 0,
        double renameRate = 0,
        int uniqueDirs = 0,
        double writeReadRatio = 0)
    {
        return new FeatureSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            ProcessId = 1234,
            ProcessName = "test.exe",
            WriteRatePerSec = writeRate,
            UniqueFilesWritten = uniqueFiles,
            RenameRatePerSec = renameRate,
            UniqueDirectoriesTouched = uniqueDirs,
            WriteReadRatio = writeReadRatio,
            
            PrimaryWindowDuration = TimeSpan.FromSeconds(5),
            ContextWindowDuration = TimeSpan.FromSeconds(15) // Placeholder
        };
    }
}
