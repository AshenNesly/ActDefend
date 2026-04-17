# Storage Layer (SQLite Persistence)

The `Detector.Storage` layer translates in-memory active payload streams into structurally secure hard-disk SQLite tables, preserving Suspicion history between computer restarts.

## Implementation Details

`AlertRepository` operates functionally inside single-thread bound locking sequences targeting `Microsoft.Data.Sqlite`. Operating an EDR using heavy `Entity Framework` abstraction layers was strictly avoided intentionally because tracking massive volumes of ransomware logs underneath an already massive framework introduces cascading OutOfMemory issues.

### Schema
The system deploys one standard structural table:
`Alerts`
It tracks exactly what process triggered (via `ProcessPath`), who caused it, the Mathematical boundaries it crossed (`Stage1Score` and `Stage2Entropy`), and exactly what metric vectors drove the bounds.

### Write-Ahead Logging
During class instantiation, `AlertRepository` binds `PRAGMA journal_mode = 'wal'` allowing concurrent reading across the UI layer and writing from the Analytics layer resolving internal locked loops previously restricting rapid File I/O bursts.
