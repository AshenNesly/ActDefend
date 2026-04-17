using ActDefend.Core.Configuration;
using ActDefend.Core.Models;
using ActDefend.Entropy;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ActDefend.UnitTests.Entropy;

public sealed class EntropySamplingEngineTests
{
    private readonly ActDefendOptions _opts;
    private readonly EntropySamplingEngine _engine;

    public EntropySamplingEngineTests()
    {
        _opts = new ActDefendOptions
        {
            Stage2 = new Stage2Options
            {
                EntropyThreshold = 7.2,
                SampleBytesLimit = 65536,
                MaxFilesToSample = 5,
                ConfirmationMinFiles = 2,
                CooldownSeconds = 10
            }
        };

        _engine = new EntropySamplingEngine(
            NullLogger<EntropySamplingEngine>.Instance,
            Options.Create(_opts));
    }

    [Fact]
    public void CalculateShannonEntropy_ReturnsZero_ForEmptyData()
    {
        var entropy = EntropySamplingEngine.CalculateShannonEntropy(Array.Empty<byte>());
        entropy.Should().Be(0.0);
    }

    [Fact]
    public void CalculateShannonEntropy_ReturnsZero_ForUniformData()
    {
        var data = new byte[100]; // Array initialized continuously to 0
        var entropy = EntropySamplingEngine.CalculateShannonEntropy(data);
        entropy.Should().Be(0.0);
    }

    [Fact]
    public void CalculateShannonEntropy_ReturnsHighValue_ForRandomData()
    {
        var random = new Random(12345);
        var data = new byte[8192];
        random.NextBytes(data);

        var entropy = EntropySamplingEngine.CalculateShannonEntropy(data);

        // Perfectly random distribution over a massive 8K bounds should map very closely near 8.0 bits.
        entropy.Should().BeGreaterThan(7.9);
    }

    [Fact]
    public async Task IsReady_TracksCooldownsCorrectly()
    {
        var pid = 999;
        
        // Initial state should bypass effortlessly
        _engine.IsReady(pid).Should().BeTrue();

        // Simulate an analysis which records internal Cooldown
        await _engine.AnalyseAsync(BuildScoringResult(pid, []), CancellationToken.None);

        // Immediate check should boundary failure since we lock down for 10 seconds.
        _engine.IsReady(pid).Should().BeFalse();
    }
    
    [Fact]
    public async Task AnalyseAsync_BypassesIfNoFilesAvailable()
    {
        var result = await _engine.AnalyseAsync(BuildScoringResult(100, []), CancellationToken.None);
        
        result.IsConfirmed.Should().BeFalse();
        result.Explanation.Should().Contain("No valid file targets available");
    }

    private static ScoringResult BuildScoringResult(int pid, List<string> recentFiles)
    {
        var snapshot = new FeatureSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            ProcessId = pid,
            ProcessName = "test.exe",
            RecentWrittenFiles = recentFiles,
            // Arbitrary values
            PrimaryWindowDuration = TimeSpan.FromSeconds(5),
            ContextWindowDuration = TimeSpan.FromSeconds(15)
        };

        return new ScoringResult
        {
            Timestamp = DateTimeOffset.UtcNow,
            Snapshot = snapshot,
            Score = 100.0,
            IsSuspicious = true,
            Explanation = "Test triggering"
        };
    }
}
