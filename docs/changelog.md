# Changelog

This project tracks deliverables mapped iteratively across the implementation block.

### Phase 8d — Runtime Diagnostic & Detection Fix (Current)

**Problem:** Simulator workloads did not trigger detection alerts even under heavy load. Dashboard counters (Events Processed, Tracked Processes, Events Dropped, Uptime) displayed 0 while alerts were appearing from other processes.

**Root Causes Identified & Fixed:**

#### RC1 — CRITICAL: Stage 2 always failed for the simulator (primary detection failure)
- **Cause:** `EntropySamplingEngine.AnalyseAsync()` only sampled paths from `RecentWrittenFiles`. The simulator writes high-entropy bytes to `document_N.txt` then immediately renames it to `document_N.txt.locked`. By the time the orchestration tick fired (~2 s), the original paths no longer existed. `TrySampleFile` silently failed on all candidates and returned `IsConfirmed = false`.
- **Fix:** `AnalyseAsync` now merges both `RecentWrittenFiles` and the new `RecentRenamedSourceFiles` lists before sampling. `TrySampleFile` probes 5 common ransomware extensions (`.locked`, `.encrypted`, `.enc`, `.crypto`, `.crypted`) when the original path is not readable. A `LogTrace` entry records which renamed variant was found.

#### RC2 — STRUCTURAL: Feature Extractor had no rename-source tracking and a too-small write queue
- **Cause:** `RecentWrittenFiles` was capped at 5 entries (naive queue). Rename events were not tracked as Stage 2 candidates at all.
- **Fix:** Write queue widened to 20 entries. New `RecentRenamedSourceFiles` queue (bounded to 20) populated from Rename events in `Emit()`. Both fields added to `FeatureSnapshot`.

#### RC3 — UI: Live counters permanently displayed 0
- **Cause:** `MonitoringStatusService.SetActiveProcessCount()` never called `RaiseChanged()`, so `StatusChanged` never fired for counter changes. `MainWindowViewModel` only re-read values on `StatusChanged`, leaving all live counters stale after startup.
- **Fix:** `SetActiveProcessCount()` now calls `RaiseChanged()` (fires every ~2 s via orchestration tick). `MainWindowViewModel` adds a `DispatcherTimer` (3 s interval) that explicitly raises `PropertyChanged` for `EventsProcessed`, `EventsDropped`, `UptimeText`, and `StatusBarText`.

#### RC4 — Minor: Events Dropped always displayed 0
- **Cause:** `PipelineHostService` logged but never propagated the collector's drop count to `MonitoringStatusService`. The `IncrementEventsDropped()` method was never called.
- **Fix:** Orchestration tick now computes the delta between the collector's cumulative drop count and the last reported drop count, and calls `IncrementEventsDropped()` for the difference.

**Tests:** 5 new unit tests covering: (a) Stage 2 confirms via `.locked` extension probe, (b) candidate list deduplication, (c) low-entropy renamed file correctly rejected, (d) `RecentRenamedSourceFiles` populated from rename events, (e) write queue bounded at ≤ 20. Total test count: 41 (all pass, 0 warnings).

**Docs updated:** `docs/modules/EntropyEngine.md`, `docs/modules/FeatureExtractor.md`, `docs/modules/GUI.md`, `docs/technical-design.md`, `docs/changelog.md`.

---

### Phase 8c - Simulator Rerun Fix

- **Bug Fixed:** `File.Move → IOException` crash on repeated ransomware workload runs caused by stale `.locked` files from previous runs.
- **Fix:** `SimulatorRunner.ResetWorkspace()` now clears all files/subdirs inside the workspace before every run. The workspace root is preserved; only its contents are removed.
- **Refactor:** Core simulator logic extracted to `SimulatorRunner` (static, no console I/O) for testability. `Program.cs` is now a thin CLI shell.
- **Tests:** 17 new unit tests in `SimulatorRunnerTests` covering safety checks, reset behavior, repeated runs (5×), post-run state, and directory spread. Total test count: 39 (all pass).
- **Docs:** `docs/modules/Simulator.md` fully rewritten.

### Phase 8b - UX / Tray Refinement (Current)
- **Tray Icon:** Generated `shield.ico` and embedded it as a WPF `<Resource>` in `Detector.GUI.csproj`. Resolved via pack URI — no missing-resource crash on launch.
- **Balloon Notifications:** Severity-aware titles (CRITICAL / HIGH / MEDIUM / LOW) with process name and PID in each balloon tip.
- **Dashboard:** Added EVENTS DROPPED panel (amber when non-zero), UPTIME panel, alert count label, and status bar that reflects live collector state. Alert rows now show a colour-coded severity pill badge and formatted timestamps.
- **AlertRowViewModel:** Introduced a thin wrapper around `DetectionAlert` for cleaner per-row bindings in XAML.
- **Elevation Restore:** Reinstated the UAC elevation relaunch that was temporarily commented out for debugging.
- **Docs:** Updated `docs/modules/GUI.md`.

### Phase 8 - Evaluation Readiness & Hardening
- **Hardening:** Added `System.ComponentModel.DataAnnotations` validating `ActDefendOptions` at startup ensuring graceful startup crashes cleanly avoiding silent bad metrics safely.
- **ETW Safety:** Internal updates gracefully handling unexpected heavy processing loop drops directly within `EtwEventCollector` protecting the UI pipeline from zombie state mappings linearly natively.
- **Docs:** Fully populated all core documentation requirements.

### Phase 7 - Safe Simulator & End-to-End Validation
- **Simulator Configs:** Updated `Detector.Simulator` correctly mapping configurable file outputs, depth arrays mimicking clean `DirectorySpread` configurations locally.
- **Headless Tests:** Configured robust decoupled integration loops spinning up native decoupled UI-Less testing matrices asserting strict False-Positive bounding natively within SQLite Database endpoints reliably.

### Phase 6 - GUI Dashboard 
- **WPF Implementation:** Delivered bounded Taskbar execution mapped nicely with internal status properties over a `MainWindow` UI perfectly matching `IMonitoringStatus` limits nicely without breaking core limits recursively cleanly.

### Phase 5 - Entropy Analysis (Stage 2)
### Phase 4 - Detection Engine (Stage 1)
### Phase 3 - Feature Extractor
### Phase 2 - Event Collector
### Phase 1 - Solution Foundations
