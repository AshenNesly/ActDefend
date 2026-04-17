using System;
using System.IO;
using System.Linq;
using ActDefend.Simulator;
using FluentAssertions;
using Xunit;

namespace ActDefend.UnitTests.Simulator;

/// <summary>
/// Unit tests for SimulatorRunner safety rules, workspace reset,
/// and repeated-run rename collision handling.
/// Tests run against a real temp directory but are fully isolated.
/// </summary>
public sealed class SimulatorRunnerTests : IDisposable
{
    // A unique temp directory per test class instance; name satisfies safety check.
    private readonly string _workspace;

    public SimulatorRunnerTests()
    {
        _workspace = Path.Combine(Path.GetTempPath(), "test-workspace", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workspace);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspace))
            Directory.Delete(_workspace, recursive: true);
    }

    // ── Safety boundary tests ─────────────────────────────────────────────────

    [Fact]
    public void IsWorkspaceSafe_AllowsSimulatorWorkspace()
    {
        var path = Path.Combine("C:", "temp", "simulator-workspace");
        SimulatorRunner.IsWorkspaceSafe(path).Should().BeTrue();
    }

    [Fact]
    public void IsWorkspaceSafe_AllowsTestWorkspace()
    {
        var path = Path.Combine("C:", "temp", "test-workspace");
        SimulatorRunner.IsWorkspaceSafe(path).Should().BeTrue();
    }

    [Theory]
    [InlineData("C:\\Users\\bob\\Documents")]
    [InlineData("C:\\temp\\myfiles")]
    [InlineData("C:\\")]
    [InlineData("D:\\work\\project")]
    public void IsWorkspaceSafe_RejectsArbitraryPaths(string path)
    {
        SimulatorRunner.IsWorkspaceSafe(path).Should().BeFalse();
    }

    [Fact]
    public void IsWorkspaceSafe_IsCaseInsensitive()
    {
        SimulatorRunner.IsWorkspaceSafe(Path.Combine("C:", "SIMULATOR-WORKSPACE")).Should().BeTrue();
        SimulatorRunner.IsWorkspaceSafe(Path.Combine("C:", "TEST-WORKSPACE")).Should().BeTrue();
    }

    // ── ResetWorkspace tests ──────────────────────────────────────────────────

    [Fact]
    public void ResetWorkspace_ClearsFilesFromPreviousRun()
    {
        // Arrange: pre-populate with stale files (simulating a previous run).
        File.WriteAllText(Path.Combine(_workspace, "document_0.txt.locked"), "stale");
        File.WriteAllText(Path.Combine(_workspace, "document_1.txt.locked"), "stale");

        // Act
        int removed = SimulatorRunner.ResetWorkspace(_workspace);

        // Assert
        removed.Should().Be(2);
        Directory.EnumerateFiles(_workspace).Should().BeEmpty();
    }

    [Fact]
    public void ResetWorkspace_RecreatesDirectoryIfItExisted()
    {
        SimulatorRunner.ResetWorkspace(_workspace);
        Directory.Exists(_workspace).Should().BeTrue();
    }

    [Fact]
    public void ResetWorkspace_DeletesSubdirectoriesFromPreviousRun()
    {
        var subdir = Path.Combine(_workspace, "folder_1");
        Directory.CreateDirectory(subdir);
        File.WriteAllText(Path.Combine(subdir, "document_0.txt.locked"), "stale");

        SimulatorRunner.ResetWorkspace(_workspace);

        Directory.Exists(subdir).Should().BeFalse();
    }

    [Fact]
    public void ResetWorkspace_ReturnsZeroWhenWorkspaceEmpty()
    {
        // Workspace was just created and is empty.
        int removed = SimulatorRunner.ResetWorkspace(_workspace);
        removed.Should().Be(0);
    }

    // ── Ransomware workload tests ─────────────────────────────────────────────

    [Fact]
    public void RansomwareWorkload_CreatesLockedFilesAndNoOriginals()
    {
        SimulatorRunner.ResetWorkspace(_workspace);
        int renamed = SimulatorRunner.RunRansomwareWorkload(_workspace, fileCount: 3, delayMs: 0, dirDepth: 1);

        renamed.Should().Be(3);

        var allFiles = Directory.EnumerateFiles(_workspace, "*", SearchOption.AllDirectories).ToList();
        allFiles.Should().OnlyContain(f => f.EndsWith(".locked", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RansomwareWorkload_CanRunTwiceWithoutCrashing()
    {
        // First run
        SimulatorRunner.ResetWorkspace(_workspace);
        var act1 = () => SimulatorRunner.RunRansomwareWorkload(_workspace, fileCount: 3, delayMs: 0, dirDepth: 1);
        act1.Should().NotThrow();

        // Second run — this is what previously crashed with IOException.
        SimulatorRunner.ResetWorkspace(_workspace);
        var act2 = () => SimulatorRunner.RunRansomwareWorkload(_workspace, fileCount: 3, delayMs: 0, dirDepth: 1);
        act2.Should().NotThrow();

        // Workspace should still be in a clean post-run state.
        var files = Directory.EnumerateFiles(_workspace, "*", SearchOption.AllDirectories).ToList();
        files.Should().OnlyContain(f => f.EndsWith(".locked", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RansomwareWorkload_CanRunManyTimesWithoutCrashing()
    {
        for (int run = 0; run < 5; run++)
        {
            SimulatorRunner.ResetWorkspace(_workspace);
            var act = () => SimulatorRunner.RunRansomwareWorkload(_workspace, fileCount: 5, delayMs: 0, dirDepth: 2);
            act.Should().NotThrow($"run {run + 1} should not throw");
        }
    }

    [Fact]
    public void RansomwareWorkload_SpreadFilesAcrossSubdirectories()
    {
        SimulatorRunner.ResetWorkspace(_workspace);
        SimulatorRunner.RunRansomwareWorkload(_workspace, fileCount: 6, delayMs: 0, dirDepth: 3);

        // With 6 files over 3 depths (2 files/dir), expect at least 2 subdirectories.
        var dirs = Directory.EnumerateDirectories(_workspace, "*", SearchOption.AllDirectories).ToList();
        dirs.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    // ── Benign workload tests ─────────────────────────────────────────────────

    [Fact]
    public void BenignWorkload_WritesPlainTextFilesWithNoLockedExtension()
    {
        SimulatorRunner.ResetWorkspace(_workspace);
        SimulatorRunner.RunBenignWorkload(_workspace, fileCount: 3, delayMs: 0);

        var files = Directory.EnumerateFiles(_workspace).ToList();
        files.Should().HaveCount(3);
        files.Should().OnlyContain(f => f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BenignWorkload_CanRerunWithoutCrashing()
    {
        SimulatorRunner.ResetWorkspace(_workspace);
        SimulatorRunner.RunBenignWorkload(_workspace, fileCount: 3, delayMs: 0);

        SimulatorRunner.ResetWorkspace(_workspace);
        var act = () => SimulatorRunner.RunBenignWorkload(_workspace, fileCount: 3, delayMs: 0);
        act.Should().NotThrow();
    }
}
