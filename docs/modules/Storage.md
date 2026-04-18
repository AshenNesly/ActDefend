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
    CorrelationId    TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IDX_Alerts_Timestamp ON Alerts(Timestamp DESC);
```

### Thread Safety

A `Lock` object serialises all database access. A new `SqliteConnection` is opened per operation (not held open across calls). This is safe and correct for a low-frequency desktop application where alerts arrive at most a few times per minute.

### Rehydration Limitation

When alerts are read back from SQLite (`GetAllAsync`, `GetRecentAsync`), the `Stage1Result` and `Stage2Result` fields are partially reconstructed: only the scalar values stored in the schema (`Stage1Score`, `Stage2Entropy`) are restored. The full `ScoringResult` and `EntropyResult` object graphs (including feature contributions and file samples) are not persisted — the UI only needs the summary values for display.

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

**Storage:** **In-memory only.** Entries are loaded from `appsettings.json:ActDefend:TrustedProcesses:DefaultExclusions` at startup and held in a `List<TrustedProcessEntry>` protected by a `Lock`.

> **Important:** Runtime additions (via `AddAsync`) and removals (via `RemoveAsync`) are held in memory only and are **lost on application restart**. There is currently no SQLite persistence for trusted-process entries. Users must re-add custom exclusions after restart, or add them to `appsettings.json` so they are loaded again on next start.

### IsTrusted Matching

`IsTrusted(processId, processName, processPath)` returns `true` if any `TrustedProcessEntry` in the list has a matching `ProcessName` (case-insensitive) and matching `ProcessPath` (case-insensitive, or null = wildcard). The current default entries match by name only (all `ProcessPath` values are null).

### Note on Orchestrator Integration

`IsTrusted` is exposed via the interface but is **not currently called by `DetectionOrchestrator`**. The allow-list check has not yet been wired into the scoring hot path — it is a data repository ready for that integration. Trusted processes are excluded at the ETW noise-filter level only (via `DefaultExclusions` loaded at startup), not dynamically checked per snapshot.

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
