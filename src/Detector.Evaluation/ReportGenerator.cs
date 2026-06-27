using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace ActDefend.Evaluation;

public static class ReportGenerator
{
    public static void GenerateCsv(IEnumerable<EvaluationResult> results, string outputPath)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("ScenarioName,WorkloadType,FileCount,DelayMs,DirDepth,Pass,FailureReason,AlertRaised,AlertCount,DetectionLatencyMs,InternalDetectorLatencyMs,AverageCpuUsagePercent,PeakCpuUsagePercent,AverageMemoryMb,PeakMemoryMb,EventsProcessed,EventsDropped,SuspicionScore,HighEntropyFileCount");
        
        foreach (var r in results)
        {
            var line = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9:F2},{10:F2},{11:F2},{12:F2},{13:F2},{14:F2},{15},{16},{17:F2},{18}",
                Escape(r.ScenarioName), r.WorkloadType, r.FileCount, r.DelayMs, r.DirectoryDepth, 
                r.Pass, Escape(r.FailureReason), r.AlertRaised, r.AlertCount, 
                r.DetectionLatencyMs, r.InternalDetectorLatencyMs, r.AverageCpuUsagePercent, r.PeakCpuUsagePercent, 
                r.AverageMemoryMb, r.PeakMemoryMb, r.EventsProcessed, r.EventsDropped, r.SuspicionScore, r.HighEntropyFileCount);
            sb.AppendLine(line);
        }
        
        File.WriteAllText(outputPath, sb.ToString());
    }
    
    public static void GenerateJson(IEnumerable<EvaluationResult> results, string outputPath)
    {
        var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(outputPath, json);
    }
    
    public static void GenerateMarkdownSummary(IEnumerable<EvaluationResult> results, string outputPath)
    {
        var ransomwareScenarios = results.Where(r => r.WorkloadType == WorkloadType.Ransomware).ToList();
        var benignScenarios = results.Where(r => r.WorkloadType == WorkloadType.Benign).ToList();
        
        var successCount = ransomwareScenarios.Count(r => r.AlertRaised);
        var successRate = ransomwareScenarios.Any() ? (double)successCount / ransomwareScenarios.Count * 100 : 0;
        
        var falsePositives = benignScenarios.Count(r => r.AlertRaised);
        
        var avgLatency = ransomwareScenarios.Where(r => r.AlertRaised).Select(r => r.DetectionLatencyMs).DefaultIfEmpty(0).Average();
        var avgCpu = results.Select(r => r.AverageCpuUsagePercent).DefaultIfEmpty(0).Average();
        var peakCpu = results.Select(r => r.PeakCpuUsagePercent).DefaultIfEmpty(0).Max();
        var avgMem = results.Select(r => r.AverageMemoryMb).DefaultIfEmpty(0).Average();
        var peakMem = results.Select(r => r.PeakMemoryMb).DefaultIfEmpty(0).Max();
        
        var sb = new StringBuilder();
        sb.AppendLine("# ActDefend Evaluation Summary");
        sb.AppendLine();
        sb.AppendLine("> **Note:** These results are generated using a controlled benign and ransomware-like simulator. No real malware was used during this evaluation.");
        sb.AppendLine();
        sb.AppendLine("## Aggregate Metrics");
        sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "- **Detection Success Rate (Ransomware):** {0:F1}% ({1}/{2})", successRate, successCount, ransomwareScenarios.Count));
        sb.AppendLine($"- **False Positives (Benign):** {falsePositives} / {benignScenarios.Count}");
        sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "- **Average Detection Latency (Ransomware):** {0:F2} ms", avgLatency));
        sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "- **Average CPU Usage:** {0:F2}%", avgCpu));
        sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "- **Peak CPU Usage:** {0:F2}%", peakCpu));
        sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "- **Average Memory Usage:** {0:F2} MB", avgMem));
        sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "- **Peak Memory Usage:** {0:F2} MB", peakMem));
        sb.AppendLine();
        
        sb.AppendLine("## Scenario Results");
        sb.AppendLine();
        sb.AppendLine("| Scenario | Result | Alert? | Latency (ms) | Avg CPU (%) | Peak RAM (MB) | Reason |");
        sb.AppendLine("|----------|--------|--------|--------------|-------------|---------------|--------|");
        
        foreach (var r in results)
        {
            var passStr = r.Pass ? "✅ PASS" : "❌ FAIL";
            var latencyStr = r.DetectionLatencyMs.HasValue ? string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F1}", r.DetectionLatencyMs.Value) : "-";
            sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "| {0} | {1} | {2} | {3} | {4:F1} | {5:F1} | {6} |",
                r.ScenarioName, passStr, r.AlertRaised, latencyStr, r.AverageCpuUsagePercent, r.PeakMemoryMb, r.FailureReason));
        }
        
        File.WriteAllText(outputPath, sb.ToString());
    }
    
    private static string Escape(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        if (input.Contains(',') || input.Contains('"') || input.Contains('\n'))
        {
            return $"\"{input.Replace("\"", "\"\"")}\"";
        }
        return input;
    }
}
