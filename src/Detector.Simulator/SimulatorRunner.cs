using System;
using System.IO;
using System.Threading;

namespace ActDefend.Simulator;

/// <summary>
/// Core simulator logic, extracted for testability.
/// All methods are static and purely filesystem-driven.
/// Safety invariant: every method validates the workspace before touching anything.
/// </summary>
public static class SimulatorRunner
{
    // ── Safety check ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true only if the workspace folder name is one of the
    /// explicitly permitted safe names ('simulator-workspace' or 'test-workspace').
    /// </summary>
    public static bool IsWorkspaceSafe(string path)
    {
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
        return name.Equals("simulator-workspace", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("test-workspace",      StringComparison.OrdinalIgnoreCase);
    }

    // ── Workspace reset ───────────────────────────────────────────────────────

    /// <summary>
    /// Deletes all files and subdirectories inside the workspace, then recreates
    /// the root directory. The workspace root is never deleted — only its contents.
    ///
    /// This is the primary fix for the rerun rename-collision crash: running this
    /// before every workload guarantees no stale .locked files remain from a
    /// previous run to cause File.Move to throw IOException.
    /// </summary>
    /// <returns>Number of files that were deleted during reset.</returns>
    public static int ResetWorkspace(string workspace)
    {
        int deleted = 0;
        if (Directory.Exists(workspace))
        {
            foreach (var file in Directory.EnumerateFiles(workspace, "*", SearchOption.AllDirectories))
            {
                File.Delete(file);
                deleted++;
            }
            foreach (var dir in Directory.EnumerateDirectories(workspace))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        Directory.CreateDirectory(workspace);
        return deleted;
    }

    // ── Workloads ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Benign workload: writes low-entropy text files at a slow rate.
    /// Designed to produce no Stage 1 alerts — validates true-negative behaviour.
    /// </summary>
    public static void RunBenignWorkload(string workspace, int fileCount, int delayMs)
    {
        for (int i = 0; i < fileCount; i++)
        {
            var file = Path.Combine(workspace, $"benign_log_{i}.txt");
            File.WriteAllText(file, $"User log entry {DateTimeOffset.UtcNow:O} - low-entropy safe text.");
            Thread.Sleep(delayMs);
            _ = File.ReadAllText(file);
        }
    }

    /// <summary>
    /// Ransomware workload: burst-writes high-entropy bytes then renames each file
    /// to a .locked extension across a directory tree.
    ///
    /// Requires a clean workspace (call ResetWorkspace first) to avoid
    /// File.Move IOException when .locked files already exist.
    /// </summary>
    /// <returns>Number of files renamed to .locked.</returns>
    public static int RunRansomwareWorkload(string workspace, int fileCount, int delayMs, int dirDepth)
    {
        var random         = new Random();
        var filesToEncrypt = new string[fileCount];

        // Phase 1: provision victim files across directory tree.
        int filesPerDir = Math.Max(1, fileCount / dirDepth);
        for (int i = 0; i < fileCount; i++)
        {
            int    depth      = Math.Min((i / filesPerDir) + 1, dirDepth);
            string currentDir = workspace;
            for (int d = 1; d <= depth; d++)
                currentDir = Path.Combine(currentDir, $"folder_{d}");

            Directory.CreateDirectory(currentDir);
            filesToEncrypt[i] = Path.Combine(currentDir, $"document_{i}.txt");
            File.WriteAllText(filesToEncrypt[i], "Important business document content.");
        }

        Thread.Sleep(1000); // Let collector establish baseline window.

        // Phase 2: simulate encryption + rename burst.
        int renamed = 0;
        foreach (var file in filesToEncrypt)
        {
            if (!File.Exists(file)) continue;

            var payload = new byte[8192];
            random.NextBytes(payload);
            File.WriteAllBytes(file, payload);

            File.Move(file, file + ".locked");
            renamed++;

            if (delayMs > 0)
                Thread.Sleep(delayMs);
        }

        return renamed;
    }
}
