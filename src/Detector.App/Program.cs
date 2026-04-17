using ActDefend.App.Services;
using ActDefend.Collector;
using ActDefend.Core.Configuration;
using ActDefend.Core.Interfaces;
using ActDefend.Detection;
using ActDefend.Entropy;
using ActDefend.Features;
using ActDefend.Logging;
using ActDefend.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

// ── 1. Bootstrap logger — captures elevation check messages before host is built ──
Log.Logger = LoggingSetup.CreateBootstrapLogger();

try
{
    Log.Information("ActDefend starting up. Version={Version}",
        typeof(Program).Assembly.GetName().Version);

    // ── 2. Elevation check ────────────────────────────────────────────────────
    //
    // The app.manifest already requests requireAdministrator, so Windows
    // typically handles UAC automatically. This check is a runtime guard
    // for edge cases where the process was invoked without ShellExecute.
    /*
    if (!ActDefend.Core.Elevation.ElevationHelper.IsElevated())
    {
        Log.Warning("Process is not elevated. Attempting UAC relaunch...");
        bool relaunched = ActDefend.Core.Elevation.ElevationHelper.TryRelaunchElevated(args);
        if (relaunched)
        {
            Log.Information("UAC relaunch initiated. Terminating non-elevated instance.");
            return 0;
        }
        else
        {
            Log.Error(
                "UAC elevation was denied or relaunch failed. " +
                "ActDefend requires Administrator privileges for ETW monitoring. " +
                "The application will start but monitoring will be unavailable.");
        }
    }
    */

    // ── 3. Build generic host ─────────────────────────────────────────────────
    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog(LoggingSetup.ConfigureFromOptions)
        .ConfigureServices((ctx, services) =>
        {
            // Bind and strictly validate all configuration sections.
            services.AddOptions<ActDefendOptions>()
                .Bind(ctx.Configuration.GetSection(ActDefendOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // Register subsystems via their extension methods.
            services
                .AddCollector()
                .AddFeatures()
                .AddDetection()
                .AddEntropy()
                .AddStorage();

            // Monitoring status (singleton shared by pipeline and GUI).
            services.AddSingleton<MonitoringStatusService>();
            services.AddSingleton<IMonitoringStatus>(
                sp => sp.GetRequiredService<MonitoringStatusService>());

            // Pipeline background service.
            services.AddHostedService<PipelineHostService>();

            // GUI hosted service.
            services.AddHostedService<ActDefend.GUI.WpfHostedService>();
        })
        .Build();

    await host.RunAsync();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "ActDefend terminated unexpectedly: {Message}", ex.Message);
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
