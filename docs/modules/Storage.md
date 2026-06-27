# Storage Layer (`Detector.Storage`)

The `Detector.Storage` project provides two persistence/publishing components:
- `AlertRepository` — durable SQLite storage for confirmed detection alerts.
- `TrustedProcessRepository` — in-memory allow-list of trusted process names.
- `AlertPublisher` — in-process event bus that notifies the GUI when an alert is raised.

---

## AlertRepository

**Implements:** `IAlertRepository`

**Backend:** `Microsoft.Data.Sqlite` (no ORM — raw SQL for minimum overhead).

### Schema

The database is created automatically on first use (if the file does not exist). WAL mode is enabled at initialisation for concurrent read/write access between the GUI and the pipeline.

```sql
CREATE TABLE IF NOT EXISTS Alerts (
    AlertId          TEXT PRIMARY KEY,
    Timestamp        TEXT NOT NULL,         -- ISO 8601 UTC (DateTimeOffset "O" format)
    ProcessId        INTEGER NOT NULL,
    ProcessName      TEXT NOT NULL,
    ProcessPath      TEXT,                  -- nullable; currently always NULL
    Severity         INTEGER NOT NULL,      -- AlertSeverity enum: 0=Low,1=Medium,2=High,3=Critical
    AffectedFileCount INTEGER NOT NULL,
    Summary          TEXT NOT NULL,
    IsAcknowledged   INTEGER NOT NULL DEFAULT 0,  -- 0=false, 1=true
    Stage1Score      REAL NOT NULL,
    Stage2Entropy    REAL NOT NULL,
    CorrelationId    TEXT NOT NULL,

    -- Rich Evidence Columns (added via migration for backwards compatibility)
    SuspicionScore          REAL DEFAULT 0,
    Stage1TopReasons        TEXT DEFAULT '',  -- comma-separated top-3 feature names
    Stage1ThresholdUsed     REAL DEFAULT 0,
    FirstSuspiciousAtUtc    TEXT,            -- nullable ISO 8601
    ConfirmedAtUtc          TEXT,            -- ISO 8601
    DetectionLatencyMs      REAL DEFAULT 0,  -- ms from first suspicion to Stage 2 confirmation
    HighEntropyFileCount    INTEGER DEFAULT 0,
    EntropyValuesJson       TEXT DEFAULT '', -- JSON array of {FilePath, ShannonEntropy, ExceedsThreshold}
    Stage2EntropyThresholdUsed REAL DEFAULT 0,
    Stage2MinFilesUsed      INTEGER DEFAULT 0
);
CREATE INDEX IF NOT EXISTS IDX_Alerts_Timestamp ON Alerts(Timestamp DESC);
```

> **Schema Migration:** The evidence columns are added to existing databases via `ALTER TABLE ... ADD COLUMN` at startup. Each migration is wrapped in a `try-catch(SqliteException)` so columns already present in a fresh database are silently skipped. Existing legacy alert rows receive `0`/empty defaults for the new columns.
>
> **Privacy:** `EntropyValuesJson` stores only file paths and entropy values — no raw bytes or file contents are ever captured.

### Thread Safety

A `Lock` object serialises all database access. A new `SqliteConnection` is opened per operation (not held open across calls). This is safe and correct for a low-frequency desktop application where alerts arrive at most a few times per minute.

### Rehydration

When alerts are read back from SQLite (`GetAllAsync`, `GetRecentAsync`), all evidence columns are rehydrated into the `DetectionAlert` record. The `Stage1Result` and `Stage2Result` sub-objects are partially reconstructed from the stored scalar columns — the full in-memory object graphs (including all feature contributions) are not persisted since the UI only requires summary values for display.

### Operations

| Method | Behaviour |
|---|---|
| `SaveAsync(alert)` | `INSERT OR UPDATE` on `AlertId` (idempotent — duplicate AlertId updates only `IsAcknowledged`). |
| `GetAllAsync()` | Returns all alerts ordered by timestamp descending. |
| `GetRecentAsync(count)` | Returns the N most recent alerts. The GUI calls this at startup to populate the alert feed history (loads the last 50). |
| `AcknowledgeAsync(alertId)` | Sets `IsAcknowledged = 1` for the given alert. |

---

## TrustedProcessRepository

**Implements:** `ITrustedProcessRepository`

**Storage:** **SQLite + Configuration.** The repository loads system defaults from `appsettings.json:ActDefend:TrustedProcesses:DefaultExclusions` and merges them with user-added entries stored in the `TrustedProcesses` SQLite table.

### Schema (TrustedProcesses)

```sql
CREATE TABLE IF NOT EXISTS TrustedProcesses (
    EntryId TEXT PRIMARY KEY,
    ProcessName TEXT NOT NULL,
    ProcessPath TEXT,
    CreatedAt TEXT NOT NULL,
    Reason TEXT NOT NULL,
    Source TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IDX_TrustedProcesses_Name ON TrustedProcesses(ProcessName);
```

Additions and removals made at runtime are persisted to the database and survive application restarts. Default exclusions are read-only; attempts to remove them are prevented by both the repository (`RemoveAsync` ignores requests for default items) and the GUI (which hides the Remove action and shows a PROTECTED badge).

### IsTrusted Matching

`IsTrusted(processId, processName, processPath)` returns `true` if any `TrustedProcessEntry` in the list has a matching `ProcessName` (case-insensitive) and matching `ProcessPath` (case-insensitive, or null = wildcard). The current default entries match by name only (all `ProcessPath` values are null).

### Note on Orchestrator Integration

`IsTrusted` is exposed via the interface and is **called by `DetectionOrchestrator.TickAsync()`**. Trusted processes are skipped entirely during scoring, reducing false positives for known benign applications. Default exclusions additionally act as an ETW noise filter (via `DefaultExclusions` loaded at startup).

---

## AlertPublisher

**Implements:** `IAlertPublisher`

An in-process event (`EventHandler<DetectionAlert> AlertRaised`) fired synchronously by `DetectionOrchestrator` after a confirmed alert is saved. `MainWindow.xaml.cs` subscribes to trigger tray balloon notifications; `MainWindowViewModel` subscribes to prepend the alert to the feed.

This is a simple, correct design for a single-process WPF desktop application. A more decoupled mechanism (e.g. `Channel<DetectionAlert>` or `IObservable`) would be needed for multi-process or service deployments.

---

## Configuration Reference

| Key | Default | Description |
|---|---|---|
| `Storage.DatabasePath` | `"actdefend.db"` | SQLite file path. Relative path resolves beside the executable. |
