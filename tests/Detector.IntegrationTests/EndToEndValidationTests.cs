using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using ActDefend.Core.Interfaces;
using ActDefend.Core.Configuration;
using ActDefend.Collector;
using ActDefend.Features;
using ActDefend.Detection;
using ActDefend.Entropy;
using ActDefend.Storage;
using ActDefend.App.Services;

namespace ActDefend.IntegrationTests;

public class EndToEndValidationTests : IDisposable
{
    private readonly string _workspace;
    private readonly string _simulatorPath;
    private readonly string _dbPath;

    public EndToEndValidationTests()
    {
        _workspace = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "simulator-workspace"));
        
        _simulatorPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
            "../../../../../src/Detector.Simulator/bin/Debug/net10.0-windows/ActDefend.Simulator.exe"));

        _dbPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "actdefend_test.db"));

        if (Directory.Exists(_workspace))
            Directory.Delete(_workspace, true);
        
        Directory.CreateDirectory(_workspace);

        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    private IHost BuildHeadlessHost()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((ctx, config) => {
                config.AddInMemoryCollection(new[] {
                    new System.Collections.Generic.KeyValuePair<string, string?>("ActDefend:Storage:DatabasePath", _dbPath),
                    new System.Collections.Generic.KeyValuePair<string, string?>("ActDefend:Features:PrimaryWindowSeconds", "2"),
                    new System.Collections.Generic.KeyValuePair<string, string?>("ActDefend:Features:EmitIntervalSeconds", "1")
                });
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

    [Fact]
    public async Task BenignWorkload_ShouldNotTriggerAlerts()
    {
        if (!IsAdministrator()) return;
        if (!File.Exists(_simulatorPath)) return;

        using var host = BuildHeadlessHost();
        var hostTask = host.StartAsync();

        // Let the pipeline initialize and ETW start
        await Task.Delay(2000);

        var (exitCode, output) = await RunSimulatorAsync("--benign", _workspace, "--file-count", "10", "--delay-ms", "300");

        exitCode.Should().Be(0);
        output.Should().Contain("Benign workload complete");

        // Give the pipeline time to process events and potentially alert
        await Task.Delay(3000);

        var alertRepo = host.Services.GetRequiredService<IAlertRepository>();
        var alerts = await alertRepo.GetRecentAsync(10);

        alerts.Should().BeEmpty("Benign workload should not generate alerts");

        await host.StopAsync();
    }

    [Fact]
    public async Task RansomwareWorkload_ShouldTriggerRapidEvents()
    {
        if (!IsAdministrator()) return;
        if (!File.Exists(_simulatorPath)) return;

        using var host = BuildHeadlessHost();
        var hostTask = host.StartAsync();

        // Let the pipeline initialize and ETW start
        await Task.Delay(2000);

        // Run heavy ransomware simulation with directory spread
        var (exitCode, output) = await RunSimulatorAsync("--ransomware", _workspace, "--file-count", "50", "--delay-ms", "0", "--dir-depth", "5");

        exitCode.Should().Be(0);
        output.Should().Contain("Ransomware workload complete");

        // Wait for sliding window to emit and detection to process (window is 2s, emit is 1s)
        await Task.Delay(5000);

        var alertRepo = host.Services.GetRequiredService<IAlertRepository>();
        var alerts = await alertRepo.GetRecentAsync(10);

        alerts.Should().NotBeEmpty("Ransomware simulator behavior should have triggered at least one detection alert");
        var ransomwareAlert = alerts.First();
        ransomwareAlert.ProcessName.Should().Contain("ActDefend.Simulator");
        ransomwareAlert.Severity.Should().Be(ActDefend.Core.Models.AlertSeverity.High);

        await host.StopAsync();
    }

    private async Task<(int ExitCode, string Output)> RunSimulatorAsync(string mode, string workspace, params string[] options)
    {
        var procArgs = $"{mode} \"{workspace}\" " + string.Join(" ", options);
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _simulatorPath,
                Arguments = procArgs,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, output);
    }

    private bool IsAdministrator()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspace))
        {
            try { Directory.Delete(_workspace, true); }
            catch { /* Transient lock */ }
        }
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); }
            catch { /* Transient lock */ }
        }
    }
}
