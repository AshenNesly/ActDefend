# Technical Design

ActDefend uses a heavily decoupled architectural approach executing smoothly within a standard `.NET Generic Host`.

The central pipeline operates using cross-threaded bounds managing backpressure and decoupling logic seamlessly.

## Mandatory Architecture

```
Windows Event Source (User Space, ETW)
 → Monitor / Event Collector         [Detector.Collector]
 → Feature Extractor                 [Detector.Features]
 → Detection Engine
    → Stage 1: Lightweight Scoring   [Detector.Detection]
    → Stage 2: Entropy Confirmation  [Detector.Entropy]
 → Outputs
    → GUI / Tray Alerts              [Detector.GUI]
    → Local Logs                     [Detector.Logging]
    → Local Database                 [Detector.Storage]
```

## Bounded Pipeline Approach

1. **Source Hook (`Detector.Collector`):** `EtwEventCollector` opens an explicit ETW Kernel Session and pushes valid File IO metrics into an internal `BoundedChannel` (default capacity 8 192). Events are dropped and the drop counter incremented when the channel is full (backpressure protection).

2. **Snapshot Logic (`Detector.Features`):** `FeatureExtractor` maintains per-PID `ProcessState` objects and computes `FeatureSnapshot`s on each orchestration tick. Events outside the context window are pruned inline. Idle PIDs expire after `InactivityExpirySeconds`. Each snapshot carries two bounded candidate lists for Stage 2:
   - `RecentWrittenFiles` — paths of recent write-event targets (last ≤ 20)
   - `RecentRenamedSourceFiles` — source paths of recent rename events (last ≤ 20)

3. **Detection Core (`Detector.Detection`):** `DetectionOrchestrator` calls `LightweightScoringEngine.Score()` per snapshot. Processes scoring ≥ `SuspicionThreshold` (default 60) proceed to Stage 2 if the per-process cooldown has expired.

4. **Entropy Engine (`Detector.Entropy`):** Stage 2 merges both candidate lists (deduplicated), then calls `TrySampleFile` for each. If the original path is missing (write-then-rename pattern), the engine probes common ransomware extensions (`.locked`, `.encrypted`, `.enc`, `.crypto`, `.crypted`). Confirmation requires `ConfirmationMinFiles` (default 2) files to exceed `EntropyThreshold` (default 7.2 bits/byte). Per-process cooldown (default 10 s) limits re-trigger frequency.

5. **UI Output (`Detector.GUI` & `Detector.Storage`):** `IAlertPublisher.AlertRaised` is subscribed in `MainWindowViewModel`, which prepends new `AlertRowViewModel` entries on the Dispatcher thread. Live counters (Events Processed, Tracked Processes, Events Dropped, Uptime) are refreshed via:
   - `MonitoringStatusService.StatusChanged` event (fires on collector state change and every ~2 s via `SetActiveProcessCount`)
   - A `DispatcherTimer` (3 s interval) in `MainWindowViewModel` for high-frequency counters

## Key Design Decisions

### Stage 2 Write-Then-Rename Awareness (Phase 8d)
The original Stage 2 implementation assumed that files written during the primary window would still exist at their original paths when Stage 2 ran. Real ransomware (and the simulator) follows a write-then-rename pattern: bytes are written to `file.txt`, then the file is moved to `file.txt.locked`. Stage 2 now probes renamed variants when the original path is unreadable. This is implemented purely in the entropy engine — no data model coupling between the rename destination and a live filesystem state was required.

### Sliding Window Sizes
- Primary window: 5 s (burst detection)
- Context window: 15 s (stabilisation + memory bound)
- Emit interval: 2 s (orchestration tick cadence)

These values are configurable without code changes via `appsettings.json`.

### Trusted Process Allow-List
System processes (`svchost.exe`, `MsMpEng.exe`, etc.) are excluded by the allow-list stored in `appsettings.json` and the SQLite database. User-added exclusions persist across restarts.
