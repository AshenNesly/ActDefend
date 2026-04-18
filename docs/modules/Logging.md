# Logging Module (`Detector.Logging`)

The `Detector.Logging` project configures **Serilog** as the structured logging backend for the entire application. All other projects receive `ILogger<T>` via standard .NET dependency injection — they have no direct Serilog dependency.

---

## Output Sinks

| Sink | Format | Minimum level | Notes |
|---|---|---|---|
| Console | Human-readable text | Debug | Template: `[HH:mm:ss LVL] Message`. Useful during development; too verbose for production. |
| Rolling JSON file | Compact JSON (Serilog `CompactJsonFormatter`) | Configured minimum level (default: Information) | Written to the `logs/` directory beside the executable. Filename pattern: `actdefend-YYYYMMDD.json`. |

Microsoft framework namespaces (`Microsoft.*`, `System.*`) are overridden to `Warning` to suppress routine host lifecycle noise.

---

## Bootstrap Logger

A lightweight bootstrap logger is created in `Program.cs` **before** the generic host is built:

```csharp
Log.Logger = LoggingSetup.CreateBootstrapLogger();
```

This captures elevation-check messages and startup exceptions that occur before Serilog is fully configured from `appsettings.json`. The bootstrap logger writes to the console only, at `Debug` minimum level.

---

## Configuration

All settings live under `ActDefend:Logging` in `appsettings.json`:

| Key | Default | Description |
|---|---|---|
| `LogDirectory` | `"logs"` | Output directory for rolling JSON files. Relative paths resolve from the executable directory. Created automatically if it does not exist. |
| `RollingInterval` | `"Day"` | Log file rotation interval (`Hour`, `Day`, `Month`, `Year`, `Infinite`). |
| `RetainedFileCountLimit` | `30` | Number of log files kept before the oldest is deleted. Set to `0` in config for unlimited (maps to `null` in the Serilog API). |
| `MinimumLevel` | `"Information"` | Minimum event level written to all sinks (`Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal`). |

---

## Important Operational Notes

### Log Directory vs ETW Self-Triggering

The logging output directory (`logs/`) is on the local filesystem. Any writes to `logs/*.json` could theoretically generate ETW FileIO events that are then ingested by the collector and scored. In practice this does not cause a feedback loop because:
1. The default minimum level is `Information`, which filters out the high-frequency `Debug`/`Trace` events from the hot path (event read loop, orchestration tick).
2. The collector filters paths starting with `C:\Windows\` but not arbitrary log directories. However, because logging is at `Information` level and occurs infrequently relative to the primary ETW burst window (5 s), the write rate contribution from logging itself is negligible and does not approach Stage 1 thresholds.

For production hardening, the `logs/` directory path could be placed outside the monitored paths (e.g. in `%PROGRAMDATA%`), but this is not required for current usage.

---

## Module Structure

| File | Role |
|---|---|
| `LoggingSetup.cs` | Static class with two methods: `CreateBootstrapLogger()` and `ConfigureFromOptions()` |

`ConfigureFromOptions` is registered via `.UseSerilog(LoggingSetup.ConfigureFromOptions)` on the `IHostBuilder`. It reads `ActDefend:Logging` from `IConfiguration` and builds the Serilog `LoggerConfiguration`.
