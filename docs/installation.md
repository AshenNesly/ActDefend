# Installation & Run Guide

## Prerequisites

| Requirement | Details |
|---|---|
| **OS** | Windows 10 / 11 (64-bit) |
| **.NET SDK** | .NET 10 (see `global.json` for exact version) |
| **Privileges** | Administrator — required for ETW kernel file-I/O sessions |
| **SQLite** | No separate installation required. The database file (`actdefend.db`) is created automatically by `Microsoft.Data.Sqlite` on first run. |

> **Note:** ActDefend does **not** use Entity Framework Core. All database access is via `Microsoft.Data.Sqlite` with raw SQL.

---

## Build

```powershell
# From the repository root
dotnet build ActDefend.slnx -c Release
```

Or build a specific project:
```powershell
dotnet build src\Detector.App -c Release
```

---

## Run the Main Application (`Detector.App`)

```powershell
dotnet run --project src\Detector.App
```

Or run the compiled binary directly (must be launched as Administrator):
```powershell
# from the output directory
.\Detector.App.exe
```

### Elevation Handling

The application manifest (`app.manifest`) requests `requireAdministrator`, so Windows normally shows a UAC prompt when you launch it. If the process somehow starts without elevation (e.g. invoked from a non-ShellExecute context), the runtime elevation check in `Program.cs` detects this and attempts to relaunch via `ShellExecute("runas")`. If the user denies the UAC prompt, the app starts in a degraded mode: the GUI appears but the collector does not start and the COLLECTOR panel shows "Stopped".

---

## Run the Simulator (`Detector.Simulator`)

The simulator is a separate CLI executable. The workspace path **must** be named `simulator-workspace` or `test-workspace`.

```powershell
# Ransomware workload (triggers detection)
dotnet run --project src\Detector.Simulator -- --ransomware .\simulator-workspace

# Ransomware workload with custom options
dotnet run --project src\Detector.Simulator -- --ransomware .\simulator-workspace --file-count 50 --delay-ms 0 --dir-depth 5

# Benign workload (validates no false-positive alerts)
dotnet run --project src\Detector.Simulator -- --benign .\simulator-workspace
```

The simulator always resets the workspace before running. Run `Detector.App` first (as Administrator), then run the simulator from a second terminal to observe detection alerts appear in the dashboard.

---

## Run Tests

```powershell
# Unit tests (no elevation required)
dotnet test tests\Detector.UnitTests

# Integration tests (requires elevation — ETW kernel session)
# Run from an elevated PowerShell prompt
dotnet test tests\Detector.IntegrationTests
```

Integration tests auto-skip if run without Administrator privileges.

---

## Configuration

No configuration changes are needed for a basic run. To tune detection sensitivity, edit `src\Detector.App\appsettings.json` before building and running.

See [configuration.md](configuration.md) for all available options.

---

## Logs

Log files are written to the `logs/` directory beside the executable (or beside `src\Detector.App\` when using `dotnet run`). Files roll daily and are named `actdefend-YYYYMMDD.json`.

---

## Database Location

The SQLite database file (`actdefend.db`) is created in the working directory. When running via `dotnet run`, this is typically `src\Detector.App\`. When running the compiled binary, it is beside the `.exe`.
