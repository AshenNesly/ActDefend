using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using ActDefend.Core.Configuration;

namespace ActDefend.Evaluation;

public static class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("ActDefend Evaluation & Benchmark Runner");
        
        if (!IsAdministrator())
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n[ERROR] Administrator privileges are required to run the evaluation.");
            Console.WriteLine("ETW collection requires elevated permissions.");
            Console.WriteLine("Please restart your console or IDE as Administrator and try again.");
            Console.ResetColor();
            return;
        }

        var runAll = args.Contains("--all") || args.Length == 0;
        var profileNameArg = GetArgValue(args, "--profile");
        var scenarioName = GetArgValue(args, "--scenario");
        var outputBase = GetArgValue(args, "--output") ?? "evaluation-output";
        
        var outputDir = Path.Combine(outputBase, DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss"));
        
        var scenariosToRun = new List<ScenarioDefinition>();
        var allScenarios = GetPredefinedScenarios();
        
        if (!string.IsNullOrEmpty(scenarioName))
        {
            var match = allScenarios.FirstOrDefault(s => s.Name.Equals(scenarioName, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                scenariosToRun.Add(match);
            else
            {
                Console.WriteLine($"Scenario '{scenarioName}' not found.");
                return;
            }
        }
        else
        {
            scenariosToRun.AddRange(allScenarios);
        }
        
        var profilesToRun = new List<ConfigurationProfile>();
        if (!string.IsNullOrEmpty(profileNameArg))
        {
            if (Enum.TryParse<ConfigurationProfile>(profileNameArg, true, out var p))
                profilesToRun.Add(p);
            else
            {
                Console.WriteLine($"Profile '{profileNameArg}' not found.");
                return;
            }
        }
        else
        {
            profilesToRun.Add(ConfigurationProfile.Balanced);
            profilesToRun.Add(ConfigurationProfile.Sensitive);
            profilesToRun.Add(ConfigurationProfile.LowResource);
            profilesToRun.Add(ConfigurationProfile.Conservative);
        }
        
        Console.WriteLine($"Output directory: {outputDir}");
        Console.WriteLine($"Running {scenariosToRun.Count} scenarios across {profilesToRun.Count} profiles...");
        
        var allResults = new Dictionary<ConfigurationProfile, List<EvaluationResult>>();
        
        foreach (var profile in profilesToRun)
        {
            Console.WriteLine($"\n=======================================================");
            Console.WriteLine($"=== EVALUATING PROFILE: {profile}");
            Console.WriteLine($"=======================================================");
            
            var profileOutputDir = Path.Combine(outputDir, profile.ToString());
            var runner = new EvaluationRunner(profileOutputDir, profile);
            
            var results = await runner.RunAllAsync(scenariosToRun);
            allResults[profile] = results;
            
            Console.WriteLine($"\nGenerating reports for {profile}...");
            ReportGenerator.GenerateCsv(results, Path.Combine(profileOutputDir, "results.csv"));
            ReportGenerator.GenerateJson(results, Path.Combine(profileOutputDir, "results.json"));
            ReportGenerator.GenerateMarkdownSummary(results, Path.Combine(profileOutputDir, "summary.md"));
        }
        
        if (profilesToRun.Count > 1)
        {
            Console.WriteLine("\nGenerating cross-profile comparison reports...");
            ProfileComparisonGenerator.GenerateCsv(allResults, Path.Combine(outputDir, "profile-comparison.csv"));
            ProfileComparisonGenerator.GenerateJson(allResults, Path.Combine(outputDir, "profile-comparison.json"));
            ProfileComparisonGenerator.GenerateMarkdownSummary(allResults, Path.Combine(outputDir, "profile-comparison.md"));
        }
        
        Console.WriteLine($"Evaluation complete. Results saved to {outputDir}");
    }
    
    private static List<ScenarioDefinition> GetPredefinedScenarios()
    {
        return new List<ScenarioDefinition>
        {
            new ScenarioDefinition { Name = "RansomwareFast", WorkloadType = WorkloadType.Ransomware, FileCount = 50, DelayMs = 0, DirectoryDepth = 5, ExpectedAlert = true },
            new ScenarioDefinition { Name = "RansomwareMedium", WorkloadType = WorkloadType.Ransomware, FileCount = 100, DelayMs = 10, DirectoryDepth = 5, ExpectedAlert = true },
            new ScenarioDefinition { Name = "RansomwareSlow", WorkloadType = WorkloadType.Ransomware, FileCount = 100, DelayMs = 50, DirectoryDepth = 5, ExpectedAlert = true },
            new ScenarioDefinition { Name = "RansomwareVerySlow", WorkloadType = WorkloadType.Ransomware, FileCount = 100, DelayMs = 200, DirectoryDepth = 5, ExpectedAlert = true }, // Expected alert depends on tuning, but we mark true to see if it misses
            new ScenarioDefinition { Name = "RansomwareLarge", WorkloadType = WorkloadType.Ransomware, FileCount = 500, DelayMs = 0, DirectoryDepth = 5, ExpectedAlert = true },
            new ScenarioDefinition { Name = "BenignSimulator", WorkloadType = WorkloadType.Benign, FileCount = 100, DelayMs = 10, DirectoryDepth = 5, ExpectedAlert = false }
        };
    }
    
    private static string? GetArgValue(string[] args, string key)
    {
        var idx = Array.IndexOf(args, key);
        if (idx >= 0 && idx < args.Length - 1)
        {
            return args[idx + 1];
        }
        return null;
    }
    
    private static bool IsAdministrator()
    {
        if (OperatingSystem.IsWindows())
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        return false;
    }
}
