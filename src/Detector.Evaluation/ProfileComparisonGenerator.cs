using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using ActDefend.Core.Configuration;

namespace ActDefend.Evaluation;

public static class ProfileComparisonGenerator
{
    public static void GenerateMarkdownSummary(Dictionary<ConfigurationProfile, List<EvaluationResult>> resultsByProfile, string outputPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# ActDefend Profile Comparison");
        sb.AppendLine();
        sb.AppendLine("> **Note:** Balanced is the default recommended profile. Other profiles are evaluated to demonstrate sensitivity/performance trade-offs. These results are based on controlled simulator workloads only; no real malware was used.");
        sb.AppendLine();
        
        sb.AppendLine("## Profile Comparison");
        sb.AppendLine();
        sb.AppendLine("| Profile | Detection Rate | Detected Scenarios | Missed Scenarios | False Positives | Avg Latency (ms) | Avg CPU (%) | Peak CPU (%) | Avg RAM (MB) | Peak RAM (MB) | Interpretation |");
        sb.AppendLine("|---------|----------------|--------------------|------------------|-----------------|------------------|-------------|--------------|--------------|---------------|----------------|");
        
        foreach (var kvp in resultsByProfile.OrderBy(k => (int)k.Key))
        {
            var profile = kvp.Key;
            var results = kvp.Value;
            
            var ransomware = results.Where(r => r.WorkloadType == WorkloadType.Ransomware).ToList();
            var benign = results.Where(r => r.WorkloadType == WorkloadType.Benign).ToList();
            
            var successCount = ransomware.Count(r => r.AlertRaised);
            var successRate = ransomware.Any() ? (double)successCount / ransomware.Count * 100 : 0;
            var detectedNames = string.Join("<br/>", ransomware.Where(r => r.AlertRaised).Select(r => r.ScenarioName));
            var missedNames = string.Join("<br/>", ransomware.Where(r => !r.AlertRaised).Select(r => r.ScenarioName));
            
            var falsePositives = benign.Count(r => r.AlertRaised);
            
            var avgLatency = ransomware.Where(r => r.AlertRaised).Select(r => r.DetectionLatencyMs).DefaultIfEmpty(0).Average();
            var avgCpu = results.Select(r => r.AverageCpuUsagePercent).DefaultIfEmpty(0).Average();
            var peakCpu = results.Select(r => r.PeakCpuUsagePercent).DefaultIfEmpty(0).Max();
            var avgMem = results.Select(r => r.AverageMemoryMb).DefaultIfEmpty(0).Average();
            var peakMem = results.Select(r => r.PeakMemoryMb).DefaultIfEmpty(0).Max();
            
            var interpretation = GetInterpretation(profile);
            
            sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "| **{0}** | {1:F1}% ({2}/{3}) | {4} | {5} | {6}/{7} | {8:F1} | {9:F1} | {10:F1} | {11:F1} | {12:F1} | {13} |",
                profile, successRate, successCount, ransomware.Count,
                string.IsNullOrEmpty(detectedNames) ? "-" : detectedNames,
                string.IsNullOrEmpty(missedNames) ? "-" : missedNames,
                falsePositives, benign.Count,
                avgLatency, avgCpu, peakCpu, avgMem, peakMem, interpretation));
        }
        
        File.WriteAllText(outputPath, sb.ToString());
    }

    public static void GenerateCsv(Dictionary<ConfigurationProfile, List<EvaluationResult>> resultsByProfile, string outputPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Profile,DetectionSuccessRatePercent,DetectedScenarios,MissedScenarios,FalsePositives,TotalBenign,AvgLatencyMs,AvgCpuPercent,PeakCpuPercent,AvgMemoryMb,PeakMemoryMb");
        
        foreach (var kvp in resultsByProfile.OrderBy(k => (int)k.Key))
        {
            var profile = kvp.Key;
            var results = kvp.Value;
            
            var ransomware = results.Where(r => r.WorkloadType == WorkloadType.Ransomware).ToList();
            var benign = results.Where(r => r.WorkloadType == WorkloadType.Benign).ToList();
            
            var successCount = ransomware.Count(r => r.AlertRaised);
            var successRate = ransomware.Any() ? (double)successCount / ransomware.Count * 100 : 0;
            var detectedCount = successCount;
            var missedCount = ransomware.Count - successCount;
            var falsePositives = benign.Count(r => r.AlertRaised);
            
            var avgLatency = ransomware.Where(r => r.AlertRaised).Select(r => r.DetectionLatencyMs).DefaultIfEmpty(0).Average();
            var avgCpu = results.Select(r => r.AverageCpuUsagePercent).DefaultIfEmpty(0).Average();
            var peakCpu = results.Select(r => r.PeakCpuUsagePercent).DefaultIfEmpty(0).Max();
            var avgMem = results.Select(r => r.AverageMemoryMb).DefaultIfEmpty(0).Average();
            var peakMem = results.Select(r => r.PeakMemoryMb).DefaultIfEmpty(0).Max();
            
            sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0},{1:F2},{2},{3},{4},{5},{6:F2},{7:F2},{8:F2},{9:F2},{10:F2}",
                profile, successRate, detectedCount, missedCount, falsePositives, benign.Count,
                avgLatency, avgCpu, peakCpu, avgMem, peakMem));
        }
        
        File.WriteAllText(outputPath, sb.ToString());
    }

    public static void GenerateJson(Dictionary<ConfigurationProfile, List<EvaluationResult>> resultsByProfile, string outputPath)
    {
        var comparisonModel = resultsByProfile.ToDictionary(
            k => k.Key.ToString(),
            v => v.Value
        );
        var json = JsonSerializer.Serialize(comparisonModel, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(outputPath, json);
    }
    
    private static string GetInterpretation(ConfigurationProfile profile)
    {
        return profile switch
        {
            ConfigurationProfile.Balanced => "Baseline trade-off.",
            ConfigurationProfile.Sensitive => "Detects faster, higher FP risk.",
            ConfigurationProfile.LowResource => "Lower overhead, slower detection.",
            ConfigurationProfile.Conservative => "Fewer FPs, may miss very slow malware.",
            _ => "-"
        };
    }
}
