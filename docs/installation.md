# Installation Guide

ActDefend compiles directly using native .NET 10 conventions mapped strictly into portable execution environments (WPF + Background Daemon).

## Compile Steps
Developers can execute raw `.slnx` build requests or standard CLI mappings:

```powershell
dotnet build "ActDefend.slnx" -c Release
```

## Running the EDR Platform

Because `Detector.Collector` utilizes `Microsoft.Diagnostics.Tracing.TraceEvent` over Kernel-bound File IO blocks, the tool MUST be invoked using explicitly elevated limits.

### Automatic Execution Checks
1. The WPF Host maps an internal check against `IsElevated()`.
2. If executed explicitly backwards (from standard shells purely as non-admin user), the system seamlessly relaunches an identical elevated command wrapper triggering the native Windows UAC prompt for Administrative powers.
3. The original unprivileged process safely terminates. If the user Denies elevation, standard Windows denial events pass natively.

## Local Dependency Requirements
- **SQLite 3**: Native bounds deployed recursively via EntityFrameworkCore hooks gracefully built natively upon initial load inside `actdefend.db`.
- **Admin Tokens**: Without ETW mapping, Phase 1 logs will throw clear drops. There are no manual kernel driver installations required.
