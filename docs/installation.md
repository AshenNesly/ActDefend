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
1. The WPF Host will map `app.manifest` requiring administrative limits cleanly up front.
2. If executed explicitly backwards (from standard shells), the system checks `IsElevated()`.
3. If negative, ActDefend intentionally suppresses crashing loops gracefully exiting ETW configuration streams while raising visual indicators inside the UI Tray.

## Local Dependency Requirements
- **SQLite 3**: Native bounds deployed recursively via EntityFrameworkCore hooks gracefully built natively upon initial load inside `actdefend.db`.
- **Admin Tokens**: Without ETW mapping, Phase 1 logs will throw clear drops. There are no manual kernel driver installations required.
