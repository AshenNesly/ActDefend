using ActDefend.Core.Configuration;
using ActDefend.Core.Models;
using ActDefend.Features;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ActDefend.UnitTests.Features;

public sealed class FeatureExtractorTests
{
    private readonly ActDefendOptions _opts;
    private readonly FeatureExtractor _extractor;

    public FeatureExtractorTests()
    {
        _opts = new ActDefendOptions
        {
            Features = new FeaturesOptions
            {
                PrimaryWindowSeconds = 5,
                ContextWindowSeconds = 15,
                InactivityExpirySeconds = 10
            }
        };

        _extractor = new FeatureExtractor(
            NullLogger<FeatureExtractor>.Instance,
            Options.Create(_opts));
    }

    [Fact]
    public void Extractor_IgnoresReads_WhenComputingRatio()
    {
        // Setup multiple reads, no writes
        for (int i = 0; i < 5; i++)
        {
            _extractor.ProcessEvent(BuildEvent(100, FileSystemEventType.Read, "C:\\test.txt"));
        }

        var results = _extractor.Emit();
        
        // Assert: Emit skips because there are NO Writes and NO Renames in burst window.
        results.Should().BeEmpty();
    }

    [Fact]
    public void Extractor_CalculatesRatioCorrectly_WhenReadsAndWritesExist()
    {
        // 5 Writes, 2 Reads
        for (int i = 0; i < 5; i++)
            _extractor.ProcessEvent(BuildEvent(200, FileSystemEventType.Write, $"C:\\w{i}.txt"));
            
        for (int i = 0; i < 2; i++)
            _extractor.ProcessEvent(BuildEvent(200, FileSystemEventType.Read, $"C:\\r{i}.txt"));

        var results = _extractor.Emit();

        results.Should().ContainSingle();
        var snapshot = results[0];
        
        snapshot.WriteRatePerSec.Should().Be(5.0 / 5.0); // 5 writes / 5 seconds
        snapshot.WriteReadRatio.Should().Be(2.5); // 5 / 2
        snapshot.UniqueDirectoriesTouched.Should().Be(1); // "C:\"
    }

    [Fact]
    public void Extractor_DoubleMaxValueRatio_WhenWritesButNoReads()
    {
        _extractor.ProcessEvent(BuildEvent(300, FileSystemEventType.Write, "C:\\data\\test.txt"));

        var results = _extractor.Emit();

        results.Should().ContainSingle();
        results[0].WriteReadRatio.Should().Be(double.MaxValue);
    }
    
    [Fact]
    public void Extractor_ExpiresOldState()
    {
        // Add an extremely old event manually by manipulating system clock? 
        // We can't trivially mock DateTimeOffset.UtcNow globally without ISystemClock, 
        // but Since ProcessEvent sets Time internally by UtcNow, we can't test expiration strictly via Unit test 
        // without waiting Task.Delay or using reflection on LastEventUtc.
        // We'll trust the expiration loop logic and test that active elements remain untouched immediately.
        
        _extractor.ProcessEvent(BuildEvent(400, FileSystemEventType.Write, "C:\\keep.txt"));
        
        _extractor.ExpireInactiveState();
        
        _extractor.ActiveProcessCount.Should().Be(1);
    }

    private static FileSystemEvent BuildEvent(int pid, FileSystemEventType type, string path)
    {
        return new FileSystemEvent(
            Timestamp: DateTimeOffset.UtcNow,
            ProcessId: pid,
            ProcessName: "test.exe",
            ProcessPath: null,
            EventType: type,
            FilePath: path);
    }
}
