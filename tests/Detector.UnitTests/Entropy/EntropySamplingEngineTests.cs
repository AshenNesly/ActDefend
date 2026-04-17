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
        var result = await _engine.AnalyseAsync(BuildScoringResult(100, [], []), CancellationToken.None);
        
        result.IsConfirmed.Should().BeFalse();
        result.Explanation.Should().Contain("No valid file targets available");
    }

    // ── New tests validating the renamed-file fix (root cause of simulator detection failure) ──

    /// <summary>
    /// Validates that Stage 2 successfully samples a high-entropy file that has been renamed
    /// to the .locked extension — simulating the exact ransomware write-then-rename pattern.
    /// This test reproduces the root cause: the original file path no longer exists; the
    /// entropy engine must probe the .locked variant to confirm detection.
    /// </summary>
    [Fact]
    public async Task AnalyseAsync_Confirms_WhenOriginalFileRenamedToLocked()
    {
        // Arrange: write high-entropy content to a .locked file as the simulator does.
        var workspace = Path.Combine(Path.GetTempPath(), $"actdefend-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspace);
        try
        {
            var originalPath = Path.Combine(workspace, "document_0.txt");
            var renamedPath  = originalPath + ".locked";

            // Write high-entropy (random) bytes directly to the renamed path.
            var randomBytes = new byte[8192];
            new Random(42).NextBytes(randomBytes);
            File.WriteAllBytes(renamedPath, randomBytes);

            // The ORIGINAL path does NOT exist (it was renamed away).
            File.Exists(originalPath).Should().BeFalse();

            // Act: Stage 2 receives the ORIGINAL path in recentWrittenFiles; the renamed path in
            // recentRenamedSourceFiles. It must discover the .locked file and confirm.
            var engine = new EntropySamplingEngine(
                NullLogger<EntropySamplingEngine>.Instance,
                Options.Create(new ActDefendOptions
                {
                    Stage2 = new Stage2Options
                    {
                        EntropyThreshold     = 7.2,
                        SampleBytesLimit     = 65536,
                        MaxFilesToSample     = 5,
                        ConfirmationMinFiles = 1,   // 1 high-entropy file is enough for this test
                        CooldownSeconds      = 1
                    }
                }));

            var result = await engine.AnalyseAsync(
                BuildScoringResult(777,
                    recentWritten:  [originalPath],    // original path — does NOT exist
                    recentRenamed:  [originalPath]),    // rename source — also original path; engine probes .locked
                CancellationToken.None);

            // Assert: Stage 2 should have found and sampled the .locked file.
            result.IsConfirmed.Should().BeTrue(
                "Stage 2 must discover the renamed .locked file via extension probing");
            result.AverageEntropy.Should().BeGreaterThan(7.5,
                "random bytes have near-maximum entropy");
            result.HighEntropyFileCount.Should().Be(1);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    /// <summary>
    /// Validates that Stage 2 correctly merges RecentWrittenFiles and RecentRenamedSourceFiles,
    /// deduplicates paths, and does not double-count a file that appears in both lists.
    /// </summary>
    [Fact]
    public async Task AnalyseAsync_MergesCandidateLists_WithoutDuplication()
    {
        var workspace = Path.Combine(Path.GetTempPath(), $"actdefend-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspace);
        try
        {
            var lockedPath = Path.Combine(workspace, "doc.txt.locked");
            var randomBytes = new byte[4096];
            new Random(99).NextBytes(randomBytes);
            File.WriteAllBytes(lockedPath, randomBytes);

            // Both lists point to the same original path "doc.txt" (which doesn't exist).
            var originalPath = Path.Combine(workspace, "doc.txt");

            var engine = new EntropySamplingEngine(
                NullLogger<EntropySamplingEngine>.Instance,
                Options.Create(new ActDefendOptions
                {
                    Stage2 = new Stage2Options
                    {
                        EntropyThreshold     = 7.0,
                        SampleBytesLimit     = 65536,
                        MaxFilesToSample     = 10,
                        ConfirmationMinFiles = 1,
                        CooldownSeconds      = 1
                    }
                }));

            var result = await engine.AnalyseAsync(
                // Same path in both lists — deduplication must prevent double-sampling.
                BuildScoringResult(888,
                    recentWritten:  [originalPath],
                    recentRenamed:  [originalPath]),
                CancellationToken.None);

            // Should sample exactly once (the .locked variant found once).
            result.Samples.Should().HaveCount(1, "duplicate path should be deduplicated before sampling");
            result.IsConfirmed.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    /// <summary>
    /// Confirms Stage 2 does NOT confirm when the renamed .locked file also has LOW entropy
    /// (e.g. benign rename — shouldn't be triggered as alarm).
    /// </summary>
    [Fact]
    public async Task AnalyseAsync_DoesNotConfirm_WhenLockedFileHasLowEntropy()
    {
        var workspace = Path.Combine(Path.GetTempPath(), $"actdefend-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspace);
        try
        {
            var lockedPath = Path.Combine(workspace, "benign.txt.locked");
            // Write low-entropy content (all zeros).
            File.WriteAllBytes(lockedPath, new byte[4096]);

            var originalPath = Path.Combine(workspace, "benign.txt");

            var engine = new EntropySamplingEngine(
                NullLogger<EntropySamplingEngine>.Instance,
                Options.Create(new ActDefendOptions
                {
                    Stage2 = new Stage2Options
                    {
                        EntropyThreshold     = 7.2,
                        SampleBytesLimit     = 65536,
                        MaxFilesToSample     = 5,
                        ConfirmationMinFiles = 1,
                        CooldownSeconds      = 1
                    }
                }));

            var result = await engine.AnalyseAsync(
                BuildScoringResult(999,
                    recentWritten:  [originalPath],
                    recentRenamed:  [originalPath]),
                CancellationToken.None);

            result.IsConfirmed.Should().BeFalse(
                "low-entropy content must not confirm even if found via extension probe");
            result.AverageEntropy.Should().BeLessThan(1.0,
                "all-zero file has entropy near 0");
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    /// <summary>
    /// Confirms that Stage 2 defensively skips sampling extensions like .dll and .zip
    /// since they are known to be high-entropy and cause False Positives for installers.
    /// </summary>
    [Fact]
    public async Task AnalyseAsync_ExcludesBenignHighEntropyExtensions_ToPreventFalsePositives()
    {
        var workspace = Path.Combine(Path.GetTempPath(), $"actdefend-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspace);
        try
        {
            var dllPath = Path.Combine(workspace, "binary.dll");
            // High entropy content will be ignored because of extension.
            var randomBytes = new byte[8192];
            new Random(123).NextBytes(randomBytes);
            File.WriteAllBytes(dllPath, randomBytes);

            var engine = new EntropySamplingEngine(
                NullLogger<EntropySamplingEngine>.Instance,
                Options.Create(new ActDefendOptions
                {
                    Stage2 = new Stage2Options
                    {
                        EntropyThreshold     = 7.0,
                        SampleBytesLimit     = 65536,
                        MaxFilesToSample     = 5,
                        ConfirmationMinFiles = 1,
                        CooldownSeconds      = 1
                    }
                }));

            var result = await engine.AnalyseAsync(
                BuildScoringResult(1001, recentWritten: [dllPath]),
                CancellationToken.None);

            result.IsConfirmed.Should().BeFalse(
                "even though the .dll has high entropy, it should be excluded from sampling to prevent false positives");
            result.Samples.Should().BeEmpty(
                ".dll is a KnownBenignHighEntropyExtension and should not produce a FileSample");
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    private static ScoringResult BuildScoringResult(
        int pid,
        List<string> recentWritten,
        List<string>? recentRenamed = null)
    {
        var snapshot = new FeatureSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            ProcessId = pid,
            ProcessName = "test.exe",
            RecentWrittenFiles = recentWritten,
            RecentRenamedSourceFiles = recentRenamed ?? [],
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

