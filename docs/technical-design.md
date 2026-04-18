# Technical Design

ActDefend is built on the .NET Generic Host. All subsystems are registered as services and resolved via dependency injection. The pipeline runs as two concurrent background loops inside `PipelineHostService`.

---

## Runtime Thread Model

```
Main thread             → Host.RunAsync() (awaited)
  ├─ PipelineHostService.RunEventReadLoopAsync()
  │      reads Channel<FileSystemEvent> → calls FeatureExtractor.ProcessEvent()
  │      (async, ThreadPool)
  ├─ PipelineHostService.RunOrchestrationLoopAsync()
  │      fires every EmitIntervalSeconds → calls DetectionOrchestrator.TickAsync()
  │      (async, ThreadPool)
  ├─ EtwEventCollector processing loop
  │      _session.Source.Process() — BLOCKING — runs on LongRunning dedicated thread
  │      pushes events → Channel<FileSystemEvent>
  └─ WpfHostedService
         WPF message pump — runs on dedicated STA thread ("WPF-UI-Thread")
```

The event read loop and the orchestration tick loop run concurrently within `PipelineHostService`. They are started with `Task.WhenAll()` and share only the `FeatureExtractor` (which is thread-safe via per-PID locking in `ProcessState`).

---

## Pipeline Internals

### 1. Event Ingestion (Hot Path)

```
ETW callback (dedicated LongRunning thread)
  → PublishEvent()
      → noise filter (PID ≤ 4, C:\Windows\, .TMP)
      → GetProcessName() (cached)
      → normalize to FileSystemEvent
      → Channel.Writer.TryWrite()  ← drop if full
```

`ProcessEvent()` on `FeatureExtractor` is called from the async event-read loop, not from the ETW callback thread. The channel is the decoupling boundary.

### 2. Feature Extraction (per-PID state)

Each `ProcessState` holds:
- `List<FileSystemEvent> _events` — raw events in the context window, pruned inline by `AddEvent()`.
- `HashSet<string> _newlyCreatedFiles` — paths of files created by this process (capped at 15 000 entries, cleared not evicted when full).

`FeatureExtractor.Emit()` iterates all active `ProcessState` entries, prunes context-window-expired events, and computes six metrics over the primary window for each PID that had writes or renames in that window. PIDs with only reads, or no primary-window activity, are skipped and produce no snapshot.

### 3. Stage 1 Scoring

Each `FeatureSnapshot` is scored independently. `LightweightScoringEngine.Score()` is synchronous and allocation-light (a `Dictionary<string, double>` per call for contributions).

### 4. Stage 2 Entropy (Triggered Path)

Stage 2 only runs when:
1. `score >= SuspicionThreshold`
2. The per-process cooldown has elapsed (`IsReady(processId)`)

`EntropySamplingEngine` maintains a `Dictionary<int, DateTimeOffset>` for cooldowns (not thread-safe; only accessed from the orchestration loop which is single-threaded per tick).

Candidate files are assembled by merging `RecentWrittenFiles` + `RecentRenamedSourceFiles`, deduplicating, and taking up to `MaxFilesToSample` (default 5). `TrySampleFile` probes six extensions in order: the original path, then `.locked`, `.encrypted`, `.enc`, `.crypto`, `.crypted`. Files on `KnownBenignHighEntropyExtensions` are skipped. Files are opened with `FileShare.ReadWrite | FileShare.Delete` to allow passive sampling of files still held open by the suspect process.

### 5. Alert Building

`DetectionOrchestrator.BuildAlert()` constructs a `DetectionAlert` with:
- `Severity` derived from the Stage 1 score (60 → Medium, 75 → High, 90 → Critical)
- `Summary` = `"ProcessName (PID N) — S1=77.3 S2=confirmed AvgEntropy=7.84"`
- `AffectedFileCount` = `UniqueFilesWritten` from the snapshot
- `CorrelationId` = new `Guid` per alert (future grouping use)

The alert is saved to SQLite and then published via `IAlertPublisher.AlertRaised`. The publish fires synchronously on the orchestration tick's async continuation — the GUI Dispatcher.Invoke inside the subscriber handles marshalling to the UI thread.

---

## Live Counter Refresh Architecture

Live dashboard counters face a two-cadence design:

| Counter | Source | Refresh mechanism |
|---|---|---|
| Events Processed | `MonitoringStatusService.TotalEventsProcessed` | `DispatcherTimer` (3 s) in `MainWindowViewModel` |
| Tracked Processes | `MonitoringStatusService.ActiveProcessCount` | `StatusChanged` event, fired ~every 2 s by `SetActiveProcessCount()` |
| Events Dropped | `MonitoringStatusService.TotalEventsDropped` | `StatusChanged` + `DispatcherTimer` 3 s |
| Uptime | Derived from `StartedAt` | `DispatcherTimer` 3 s |
| Collector / Elevation | `IsCollectorRunning`, `IsElevated` | `StatusChanged` (on state change only) |

`PipelineHostService` computes a delta on drop count per tick to avoid calling `IncrementEventsDropped()` multiple times for the same event.

---

## Key Design Decisions

### Write-Then-Rename Awareness

The original Stage 2 assumed files written during the primary window would still exist at their original paths. Real ransomware (and the simulator) follows write-then-rename: bytes are written to `file.txt`, then the file is moved to `file.txt.locked`. The solution:
1. Feature extractor tracks both `RecentWrittenFiles` (write paths) and `RecentRenamedSourceFiles` (rename source paths).
2. Stage 2 merges both lists and probes common ransomware extensions when the original path is unreadable.

### PreExistingModifyRate Feature

Installers write thousands of new files without touching pre-existing user data; ransomware must modify pre-existing user files to be destructive. This feature (weight 25 pts) separates the two behaviours. `ProcessState` tracks newly-created file paths in a `HashSet<string>`. Any write, rename, or delete on a path not in that set increments the pre-existing modify counter.

### Write-Read Ratio = 0 When Reads = 0

Pure write-only processes (downloaders, network-stream extractors) have `primaryReads == 0`. Previously this was assigned `double.MaxValue`, earning the maximum WriteReadRatio contribution. It is now `0.0` — pure write-only is not penalised by this axis, because ransomware must read original file content to encrypt it.

### Sliding Window Sizes

- Primary window: 5 s (burst detection)
- Context window: 15 s (memory bound + stabilisation)
- Emit interval: 2 s (orchestration tick cadence)

These are configurable in `appsettings.json` without code changes.

### Trusted-Process Allow-List

System processes (`svchost.exe`, `MsMpEng.exe`, etc.) are excluded via `DefaultExclusions` in `appsettings.json`, loaded into `TrustedProcessRepository` at startup. The `IsTrusted()` method is available on the interface but is not yet called in the scoring hot path. Exclusion currently happens only at the static config level; dynamic per-tick checking is a planned next step.

### No ORM

`AlertRepository` uses `Microsoft.Data.Sqlite` directly with raw SQL. This avoids the startup overhead, reflection, and migration complexity of an ORM for a single table with simple CRUD operations.

### Dependency Inversion

`Detector.Core` defines all interfaces and models. All other projects depend on `Detector.Core` but not on each other. `Detector.App` is the sole composition root.
