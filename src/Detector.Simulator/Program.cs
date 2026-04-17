using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace ActDefend.Simulator;

/// <summary>
/// Safe constrained defensive execution mimicking ransomware string hooks for evaluation metrics.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine("⛨ ActDefend Safe Simulator");

        if (args.Length < 2)
        {
            PrintUsage();
            return 1;
        }

        var mode = args[0].ToLowerInvariant();
        var workspace = Path.GetFullPath(args[1]);

        if (!IsWorkspaceSafe(workspace))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[!] SAFETY ABORT: Workspace '{workspace}' is not explicitly permitted.");
            Console.WriteLine("    Please use a designated safe path like '.\\simulator-workspace\\' or '.\\test-workspace\\'");
            Console.ResetColor();
            return 1;
        }

        Directory.CreateDirectory(workspace);

        // Parse optional configuration
        int delayMs = mode == "--ransomware" ? 10 : 500;
        int fileCount = mode == "--ransomware" ? 30 : 5;
        int dirDepth = mode == "--ransomware" ? 3 : 1;

        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--delay-ms" && i + 1 < args.Length && int.TryParse(args[i + 1], out var d)) delayMs = d;
            if (args[i] == "--file-count" && i + 1 < args.Length && int.TryParse(args[i + 1], out var f)) fileCount = f;
            if (args[i] == "--dir-depth" && i + 1 < args.Length && int.TryParse(args[i + 1], out var dp)) dirDepth = dp;
        }

        Console.WriteLine($"[Config] Delay: {delayMs}ms, Files: {fileCount}, Depth: {dirDepth}");

        switch (mode)
        {
            case "--benign":
                RunBenignWorkload(workspace, fileCount, delayMs);
                break;
            case "--ransomware":
                RunRansomwareWorkload(workspace, fileCount, delayMs, dirDepth);
                break;
            default:
                PrintUsage();
                return 1;
        }

        return 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: ActDefend.Simulator.exe [--benign | --ransomware] <workspace-path> [options]");
        Console.WriteLine("Options:");
        Console.WriteLine("  --delay-ms <ms>       Delay between actions");
        Console.WriteLine("  --file-count <count>  Number of files to generate");
        Console.WriteLine("  --dir-depth <depth>   Depth of directory spread");
    }

    private static bool IsWorkspaceSafe(string path)
    {
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
        return name.Equals("simulator-workspace", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("test-workspace", StringComparison.OrdinalIgnoreCase);
    }

    private static void RunBenignWorkload(string workspace, int fileCount, int delayMs)
    {
        Console.WriteLine("[*] Running BENIGN workload (Slow intermittent reads/writes)");
        
        for (int i = 0; i < fileCount; i++)
        {
            var file = Path.Combine(workspace, $"benign_log_{i}.txt");
            File.WriteAllText(file, $"User log entry {DateTime.UtcNow} - low entropy safe text string.");
            
            Thread.Sleep(delayMs); 
            
            var read = File.ReadAllText(file);
        }
        Console.WriteLine("[+] Benign workload complete.");
    }

    private static void RunRansomwareWorkload(string workspace, int fileCount, int delayMs, int dirDepth)
    {
        Console.WriteLine("[*] Running RANSOMWARE workload (Fast heavy random-byte bursts + renames + directory spread)");
        
        var random = new Random();
        var filesToEncrypt = new string[fileCount];

        // 1. Setup target files with directory spread
        int filesPerDir = Math.Max(1, fileCount / dirDepth);
        for (int i = 0; i < fileCount; i++)
        {
            int currentDepth = (i / filesPerDir) + 1;
            string currentDir = workspace;
            for (int d = 1; d <= currentDepth; d++)
            {
                currentDir = Path.Combine(currentDir, $"folder_{d}");
                if (!Directory.Exists(currentDir))
                {
                    Directory.CreateDirectory(currentDir);
                }
            }
            
            filesToEncrypt[i] = Path.Combine(currentDir, $"document_{i}.txt");
            File.WriteAllText(filesToEncrypt[i], "Important business document content.");
        }

        Console.WriteLine($"   -> {fileCount} files provisioned across {dirDepth} directory depth. Simulating attack burst...");
        Thread.Sleep(1000); // Give the collector a moment to map the setup safely into old sliding windows

        // 2. Explode rapidly mapping exactly to stage-2 bounds
        foreach (var file in filesToEncrypt)
        {
            if (!File.Exists(file)) continue;

            // Drop maximum entropy payload simulating encryption
            var encryptedBytes = new byte[8192];
            random.NextBytes(encryptedBytes); 

            // Simulate direct rewrite stream
            File.WriteAllBytes(file, encryptedBytes);

            // Immediately simulate ransomware renaming structure
            File.Move(file, file + ".locked");

            // Very tight boundary (mimics true processing speeds throwing standard features up)
            if (delayMs > 0)
                Thread.Sleep(delayMs);
        }

        Console.WriteLine("[+] Ransomware workload complete.");
    }
}
