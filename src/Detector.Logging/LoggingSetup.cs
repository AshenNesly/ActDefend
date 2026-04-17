using ActDefend.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace ActDefend.Logging;

/// <summary>
/// Configures Serilog as the structured logging backend.
///
/// Output:
/// - Console: human-readable text (debug/dev use)
/// - Rolling JSON file: machine-readable compact JSON for evaluation and debugging
///
/// Rolling file uses daily rotation by default (configurable).
/// File path and retention are controlled by LoggingOptions in appsettings.json.
/// </summary>
public static class LoggingSetup
{
    /// <summary>
    /// Builds and returns a configured Serilog logger.
    /// Call this BEFORE the generic host is created so early startup
    /// messages (e.g. elevation check) are captured.
    /// </summary>
    public static ILogger CreateBootstrapLogger()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateBootstrapLogger();
    }

    /// <summary>
    /// Configures Serilog from application configuration.
    /// Registered via UseSerilog() on the generic host builder.
    /// </summary>
    public static void ConfigureFromOptions(
        Microsoft.Extensions.Hosting.HostBuilderContext context,
        IServiceProvider    services,
        LoggerConfiguration loggerConfig)
    {
        var opts = new LoggingOptions();
        context.Configuration.GetSection($"{ActDefendOptions.SectionName}:Logging").Bind(opts);

        var minLevel = opts.MinimumLevel.ToUpperInvariant() switch
        {
            "VERBOSE"     => LogEventLevel.Verbose,
            "DEBUG"       => LogEventLevel.Debug,
            "WARNING"     => LogEventLevel.Warning,
            "ERROR"       => LogEventLevel.Error,
            "FATAL"       => LogEventLevel.Fatal,
            _             => LogEventLevel.Information
        };

        var rollingInterval = opts.RollingInterval.ToUpperInvariant() switch
        {
            "HOUR"     => Serilog.RollingInterval.Hour,
            "MONTH"    => Serilog.RollingInterval.Month,
            "YEAR"     => Serilog.RollingInterval.Year,
            "INFINITE" => Serilog.RollingInterval.Infinite,
            _          => Serilog.RollingInterval.Day
        };

        // Ensure log directory exists.
        var logDir = Path.IsPathRooted(opts.LogDirectory)
            ? opts.LogDirectory
            : Path.Combine(AppContext.BaseDirectory, opts.LogDirectory);
        Directory.CreateDirectory(logDir);

        var logFilePath = Path.Combine(logDir, "actdefend-.json");

        loggerConfig
            .MinimumLevel.Is(minLevel)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System",    LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "ActDefend")
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                restrictedToMinimumLevel: LogEventLevel.Debug)
            .WriteTo.File(
                formatter: new CompactJsonFormatter(),
                path: logFilePath,
                rollingInterval: rollingInterval,
                retainedFileCountLimit: opts.RetainedFileCountLimit > 0
                    ? opts.RetainedFileCountLimit
                    : (int?)null,
                shared: false);
    }
}
