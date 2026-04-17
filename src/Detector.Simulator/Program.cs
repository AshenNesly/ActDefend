using System;
using System.IO;
using ActDefend.Simulator;

namespace ActDefend.Simulator;

/// <summary>
/// CLI entry point for the safe ransomware-like simulator.
/// All core logic lives in SimulatorRunner for testability.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine("⛨ ActDefend Safe Simulator");
        Console.WriteLine("─────────────────────────────────────────────");

        if (args.Length < 2)
        {
            PrintUsage();
            return 1;
        }

        var mode      = args[0].ToLowerInvariant();
        var workspace = Path.GetFullPath(args[1]);

        if (!SimulatorRunner.IsWorkspaceSafe(workspace))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[!] SAFETY ABORT: Workspace '{workspace}' is not permitted.");
            Console.WriteLine("    The workspace folder name MUST be 'simulator-workspace' or 'test-workspace'.");
            Console.ResetColor();
            return 1;
        }

        // Parse optional configuration.
        int delayMs   = mode == "--ransomware" ? 10  : 500;
        int fileCount = mode == "--ransomware" ? 30  : 5;
        int dirDepth  = mode == "--ransomware" ? 3   : 1;

        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--delay-ms"   && i + 1 < args.Length && int.TryParse(args[i + 1], out var d))  delayMs   = d;
            if (args[i] == "--file-count" && i + 1 < args.Length && int.TryParse(args[i + 1], out var f))  fileCount = f;
            if (args[i] == "--dir-depth"  && i + 1 < args.Length && int.TryParse(args[i + 1], out var dp)) dirDepth  = dp;
        }

        // Always reset workspace before running — prevents rename collision on reruns.
        int removed = SimulatorRunner.ResetWorkspace(workspace);
        if (removed > 0)
            Console.WriteLine($"[~] Workspace reset: removed {removed} file(s) from previous run.");

        Console.WriteLine($"[Config] Mode: {mode}, Delay: {delayMs}ms, Files: {fileCount}, Depth: {dirDepth}");
        Console.WriteLine();

        switch (mode)
        {
            case "--benign":
                Console.WriteLine("[*] Running BENIGN workload — slow intermittent reads/writes");
                SimulatorRunner.RunBenignWorkload(workspace, fileCount, delayMs);
                Console.WriteLine("[+] Benign workload complete.");
                break;

            case "--ransomware":
                Console.WriteLine("[*] Running RANSOMWARE workload — burst writes + renames + directory spread");
                Console.WriteLine($"   -> Provisioning {fileCount} files across {dirDepth} directory depth...");
                int renamed = SimulatorRunner.RunRansomwareWorkload(workspace, fileCount, delayMs, dirDepth);
                Console.WriteLine($"   -> {renamed} file(s) encrypted and renamed to .locked.");
                Console.WriteLine("[+] Ransomware workload complete.");
                break;

            default:
                PrintUsage();
                return 1;
        }

        return 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  ActDefend.Simulator --benign     <workspace-path> [options]");
        Console.WriteLine("  ActDefend.Simulator --ransomware <workspace-path> [options]");
        Console.WriteLine();
        Console.WriteLine("workspace-path MUST be named 'simulator-workspace' or 'test-workspace'.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --delay-ms <ms>       Delay between file operations   (default: benign=500, ransomware=10)");
        Console.WriteLine("  --file-count <count>  Number of files to create        (default: benign=5,   ransomware=30)");
        Console.WriteLine("  --dir-depth <depth>   Directory tree depth for spread  (default: benign=1,   ransomware=3)");
        Console.WriteLine();
        Console.WriteLine("The simulator ALWAYS clears the workspace before each run.");
        Console.WriteLine("This guarantees clean, repeatable reruns with no rename collisions.");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  ActDefend.Simulator --benign    .\\simulator-workspace");
        Console.WriteLine("  ActDefend.Simulator --ransomware .\\simulator-workspace --file-count 50 --delay-ms 0 --dir-depth 5");
    }
}
