# Logging Module (`Detector.Logging`)

The `Detector.Logging` project serves as the centralized infrastructure logging capability supporting runtime analytics natively decoupling output implementations structurally.

## Implementation Standard
It wraps Serilog cleanly underneath the standard generic `.NET ` ILogger constraints ensuring DI graphs function completely natively without tied hardcoded variables natively.

## Output Targets

1. **Console**: Structured text outputs mapped explicitly for debugging development runs smoothly across terminal sessions.
2. **Rolling JSON**: All alerts, errors, and trace maps are automatically parsed into Compact JSON streams locally inside the `/logs` directory natively.

## Configuration & Expiration

Configurations exist inside `appsettings.json`:
- Logs are strictly constrained utilizing `RollingInterval.Day`.
- Old logs silently auto-expire mapped by `RetainedFileCountLimit` (default `30` days).
- Baseline outputs hold at `Information` cleanly filtering debugging IO metrics dynamically protecting raw disk bounds completely correctly avoiding self-triggering feedback loops against the ETW collector hooks locally natively.
