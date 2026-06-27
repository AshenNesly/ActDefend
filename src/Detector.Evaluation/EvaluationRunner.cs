using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ActDefend.App.Services;
using ActDefend.Collector;
using ActDefend.Core.Configuration;
using ActDefend.Core.Interfaces;
using ActDefend.Detection;
using ActDefend.Entropy;
using ActDefend.Features;
using ActDefend.Storage;

namespace ActDefend.Evaluation;

public class EvaluationRunner
{
    private readonly string _outputDir;
    private readonly string _simulatorPath;
    private readonly ConfigurationProfile _profile;

    public EvaluationRunner(string outputDir, ConfigurationProfile profile)
    {
        _profile = profile;
        _outputDir = Path.GetFullPath(outputDir);
        Directory.CreateDirectory(_outputDir);

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var configName = baseDir.Contains("Release") ? "Release" : "Debug";

        // Walk up from baseDir (which may be …/win-x64/ due to RID) to find src/Detector.Simulator/bin/
        var possibleSrcDirs = new[]
        {
            Path.GetFullPath(Path.Combine(baseDir, "../../../../")),
            Path.GetFullPath(Path.Combine(baseDir, "../../../../../")),
            Path.GetFullPath(Path.Combine(baseDir, "../../../../../../"))
        };

        foreach (var dir in possibleSrcDirs)
        {
            var candidate = Path.Combine(dir, "Detector.Simulator", "bin", configName, "net10.0-windows", "ActDefend.Simulator.exe");
            if (File.Exists(candidate))
            {
                _simulatorPath = candidate;
                break;
            }
        }

        if (string.IsNullOrEmpty(_simulatorPath) || !File.Exists(_simulatorPath))
        {
            // fallback for published / side-by-side layout
            _simulatorPath = Path.Combine(baseDir, "ActDefend.Simulator.exe");
        }
    }

    public async Task<List<EvaluationResult>> RunAllAsync(List<ScenarioDefinition> scenarios)
    {
        var results = new List<EvaluationResult>();

        foreach (var scenario in scenarios)
        {
            Console.WriteLine($"\n=======================================================");
            Console.WriteLine($"Running Scenario: {scenario.Name}");
            Console.WriteLine($"=======================================================");

            var result = await RunScenarioAsync(scenario);
            results.Add(result);

            var pass = result.Pass ? "PASS ✓" : "FAIL ✗";
            Console.WriteLine($"Result: {pass}  Reason: {result.FailureReason}");
            Console.WriteLine($"Alerts: {result.AlertCount} | Latency: {result.DetectionLatencyMs}ms | Events: +{result.EventsProcessedDelta} processed / +{result.EventsDroppedDelta} dropped");
            Console.WriteLine($"CPU avg {result.AverageCpuUsagePercent:F1}% peak {result.PeakCpuUsagePercent:F1}% | Mem avg {result.AverageMemoryMb:F1}MB peak {result.PeakMemoryMb:F1}MB");
            Console.WriteLine($"DB path (pipeline): {result.DiagDatabasePathUsedByPipeline}");
        }

        return results;
    }

    private async Task<EvaluationResult> RunScenarioAsync(ScenarioDefinition scenario)
    {
        // ── Isolated per-scenario paths ────────────────────────────────────────
        // Use a FULLY QUALIFIED absolute path so no component can resolve it
        // relative to CWD and silently write to the wrong file.
        var dbPath    = Path.GetFullPath(Path.Combine(_outputDir, $"evaluation_{scenario.Name}.db"));
        var workspace = Path.GetFullPath(Path.Combine(_outputDir, "simulator-workspace"));

        if (File.Exists(dbPath)) File.Delete(dbPath);
        if (Directory.Exists(workspace))
            try { Directory.Delete(workspace, true); } catch { /* transient lock */ }
        Directory.CreateDirectory(workspace);

        var result = new EvaluationResult
        {
            ScenarioName   = scenario.Name,
            WorkloadType   = scenario.WorkloadType,
            FileCount      = scenario.FileCount,
            DelayMs        = scenario.DelayMs,
            DirectoryDepth = scenario.DirectoryDepth,
            DiagDatabasePathUsedByPipeline  = dbPath,
            DiagDatabasePathReadByEvaluator = dbPath
        };

        Console.WriteLine($"  [DIAG] DB path: {dbPath}");
        Console.WriteLine($"  [DIAG] Workspace: {workspace}");
        Console.WriteLine($"  [DIAG] Simulator: {_simulatorPath}");

        // ── Build headless host with the ABSOLUTE db path ─────────────────────
        using var host = CreateHeadlessHost(dbPath);

        // ── Performance monitor (sampling THIS process) ───────────────────────
        using var perfMonitor = new PerformanceMonitor(Environment.ProcessId);
        perfMonitor.Start();

        // ── Start the host (PipelineHostService will spin up ETW) ─────────────
        await host.StartAsync();

        // ── Wait until the ETW collector reports IsRunning = true ─────────────
        var statusSvc = host.Services.GetRequiredService<MonitoringStatusService>();
        var collectorReady = await WaitForCollectorAsync(statusSvc, timeoutMs: 8000);
        result.DiagCollectorStarted = collectorReady;

        if (!collectorReady)
        {
            Console.WriteLine("  [DIAG] ETW collector did NOT start within timeout — aborting scenario.");
            result.Pass          = false;
            result.FailureReason = "ETW collector failed to start.";
            await host.StopAsync();
            return result;
        }

        Console.WriteLine("  [DIAG] ETW collector confirmed running.");

        // ── Snapshot event counts BEFORE simulator ────────────────────────────
        var eventsProcessedBefore = statusSvc.TotalEventsProcessed;
        var eventsDroppedBefore   = statusSvc.TotalEventsDropped;
        result.DiagEventsProcessedBefore = eventsProcessedBefore;
        result.DiagEventsDroppedBefore   = eventsDroppedBefore;

        // ── Run simulator ──────────────────────────────────────────────────────
        result.StartTimeUtc = DateTimeOffset.UtcNow;
        var simStart = DateTimeOffset.UtcNow;

        Console.WriteLine($"  [DIAG] Simulator start: {simStart:O}");
        var (simExit, simOutput) = await RunSimulatorAsync(scenario, workspace);
        var simEnd = DateTimeOffset.UtcNow;

        result.DiagSimulatorExitCode  = simExit;
        result.DiagSimulatorStartUtc  = simStart;
        result.DiagSimulatorEndUtc    = simEnd;

        Console.WriteLine($"  [DIAG] Simulator end: {simEnd:O}  exitCode={simExit}");
        Console.WriteLine($"  [DIAG] Simulator output: {simOutput.Trim()}");

        // ── Snapshot events AFTER simulator ──────────────────────────────────
        var eventsProcessedAfter = statusSvc.TotalEventsProcessed;
        var eventsDroppedAfter   = statusSvc.TotalEventsDropped;
        result.DiagEventsProcessedAfter = eventsProcessedAfter;
        result.DiagEventsDroppedAfter   = eventsDroppedAfter;
        result.EventsProcessedDelta     = eventsProcessedAfter - eventsProcessedBefore;
        result.EventsDroppedDelta       = eventsDroppedAfter   - eventsDroppedBefore;

        Console.WriteLine($"  [DIAG] Events processed delta: +{result.EventsProcessedDelta}  dropped delta: +{result.EventsDroppedDelta}");

        // ── Wait for pipeline to process remaining events + emit a tick ────────
        // EmitIntervalSeconds=1, PrimaryWindowSeconds=2  → max 3s to see alert
        // Add 5 s padding to absorb Stage 2 entropy sampling latency.
        int postSimWait = scenario.ExpectedAlert ? 8000 : 4000;
        Console.WriteLine($"  [DIAG] Waiting {postSimWait}ms for pipeline to process events...");
        await Task.Delay(postSimWait);

        result.EndTimeUtc = DateTimeOffset.UtcNow;

        // ── Events AFTER the wait ─────────────────────────────────────────────
        var eventsProcessedFinal = statusSvc.TotalEventsProcessed;
        result.EventsProcessed = eventsProcessedFinal;
        result.EventsDropped   = statusSvc.TotalEventsDropped;

        Console.WriteLine($"  [DIAG] Events total after wait: {eventsProcessedFinal}");

        perfMonitor.Stop();

        // ── Query alerts from the SAME singleton IAlertRepository ─────────────
        // Do NOT use CreateScope — all storage services are Singleton.
        // Scoped resolution would create a new repository pointing to the correct DB,
        // but historically caused confusion. Use root container directly.
        var alertRepo = host.Services.GetRequiredService<IAlertRepository>();

        var queryStart = DateTimeOffset.UtcNow;
        var alerts = await alertRepo.GetRecentAsync(200);
        var queryEnd = DateTimeOffset.UtcNow;

        result.DiagAlertQueryStartUtc = queryStart;
        result.DiagAlertQueryEndUtc   = queryEnd;
        result.DiagRawAlertCount      = alerts.Count;
        result.DiagFilteredAlertCount = alerts.Count; // No filter currently applied

        result.AlertCount  = alerts.Count;
        result.AlertRaised = result.AlertCount > 0;

        Console.WriteLine($"  [DIAG] Alerts found in DB: {alerts.Count}");

        if (result.AlertRaised)
        {
            var first = alerts.OrderBy(a => a.Timestamp).First();
            result.FirstAlertTimeUtc          = first.Timestamp;
            result.DetectionLatencyMs         = (first.Timestamp - result.StartTimeUtc).TotalMilliseconds;
            result.InternalDetectorLatencyMs  = first.DetectionLatencyMs;
            result.SuspicionScore             = first.SuspicionScore;
            result.Stage1TopReasons           = first.Stage1TopReasons;
            result.HighEntropyFileCount       = first.HighEntropyFileCount;
            result.EntropyValues              = first.EntropyValuesJson;

            Console.WriteLine($"  [DIAG] First alert at: {first.Timestamp:O}  Process: {first.ProcessName}  Score: {first.SuspicionScore:F1}");
        }

        // ── Stop host AFTER reading alerts ────────────────────────────────────
        await host.StopAsync();

        // ── Collect performance metrics ───────────────────────────────────────
        result.AverageCpuUsagePercent = perfMonitor.AverageCpuUsagePercent;
        result.PeakCpuUsagePercent    = perfMonitor.PeakCpuUsagePercent;
        result.AverageMemoryMb        = perfMonitor.AverageMemoryMb;
        result.PeakMemoryMb           = perfMonitor.PeakMemoryMb;

        // ── Pass / Fail ───────────────────────────────────────────────────────
        if (scenario.ExpectedAlert && !result.AlertRaised)
        {
            result.Pass          = false;
            result.FailureReason = $"Expected alert was not raised (events processed delta: {result.EventsProcessedDelta}).";
        }
        else if (!scenario.ExpectedAlert && result.AlertRaised)
        {
            result.Pass          = false;
            result.FailureReason = "False positive alert raised.";
        }
        else
        {
            result.Pass = true;
        }

        return result;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static async Task<bool> WaitForCollectorAsync(MonitoringStatusService status, int timeoutMs)
    {
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (status.IsCollectorRunning) return true;
            await Task.Delay(200);
        }
        return false;
    }

    private async Task<(int ExitCode, string Output)> RunSimulatorAsync(ScenarioDefinition scenario, string workspace)
    {
        if (!File.Exists(_simulatorPath))
            throw new FileNotFoundException($"Simulator executable not found at: {_simulatorPath}");

        var mode     = scenario.WorkloadType == WorkloadType.Ransomware ? "--ransomware" : "--benign";
        var procArgs = $"{mode} \"{workspace}\" --file-count {scenario.FileCount} --delay-ms {scenario.DelayMs} --dir-depth {scenario.DirectoryDepth}";

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName              = _simulatorPath,
                Arguments             = procArgs,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, output);
    }

    private IHost CreateHeadlessHost(string dbPath)
    {
        // CRITICAL: We must call ClearProviders() so that appsettings.json from the CWD
        // does NOT override our absolute DatabasePath with the relative "actdefend.db".
        // Host.CreateDefaultBuilder() loads appsettings.json BEFORE AddInMemoryCollection
        // runs, and since JSON keys share the same config section the later key wins —
        // but only if we add our in-memory values AFTER JSON loads. To be safe and
        // explicit, we suppress all file-based providers and supply only what we need.
        return Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((ctx, config) =>
            {
                // Remove ALL default sources (appsettings.json, env vars that could
                // shadow our DB path, etc.) then add only a minimal in-memory set.
                config.Sources.Clear();
                var profileConfig = ConfigurationProfileHelper.GetProfileConfigValues(_profile);
                profileConfig["ActDefend:Storage:DatabasePath"] = dbPath;
                config.AddInMemoryCollection(profileConfig);
            })
            .ConfigureServices((ctx, services) =>
            {
                services.Configure<ActDefendOptions>(ctx.Configuration.GetSection(ActDefendOptions.SectionName));
                services.AddCollector().AddFeatures().AddDetection().AddEntropy().AddStorage();
                services.AddSingleton<MonitoringStatusService>();
                services.AddSingleton<IMonitoringStatus>(sp => sp.GetRequiredService<MonitoringStatusService>());
                services.AddHostedService<PipelineHostService>();
            })
            .Build();
    }
}
