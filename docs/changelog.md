# Changelog

Changes are recorded per development phase. Newest first.

---

## Phase 8e — False Positive Reduction

**Problem:** After Phase 8d activated the full detection pipeline, heavy benign workloads (software installers, `npm install`, unzippers, IDE builds) occasionally triggered false positive alerts.

**Root Causes Identified & Fixed:**

### RC1 — Stage 2: High-entropy benign file formats falsely confirmed
**Cause:** Compressed/compiled formats (`.dll`, `.exe`, `.zip`, `.png`, `.pack`) have Shannon entropy naturally above 7.2, identical to encrypted data. When Stage 1 flagged a build tool, Stage 2 measured the `.dll`s it was writing and confirmed them as ransomware.

**Fix:** Introduced `KnownBenignHighEntropyExtensions` in `EntropySamplingEngine`. Files whose extension is on this list are skipped during entropy measurement; the engine moves to the next candidate.

**Why this is safe:** Ransomware encrypts user documents (`.docx`, `.pdf`, `.xls`, `.txt`) which are not on the exclusion list. The write-then-rename pattern substitutes extensions (e.g. `.locked`) which are also not on the list, so renamed files are correctly sampled.

### RC2 — Stage 1: Pure-write workloads over-penalised by WriteReadRatio
**Cause:** Downloaders and network-stream installers write files without reading local disk files, so `primaryReads` was 0. The code assigned `double.MaxValue` as the ratio and the scoring engine capped it at full weight (10 pts), penalising all installers maximally.

**Fix:** In `FeatureExtractor.Emit()`, `WriteReadRatio` is set to `0.0` when `primaryReads == 0`. Pure write-only processes receive 0 score contribution from this feature instead of the maximum.

**Why this is safe:** Ransomware performing in-place encryption reads the original file before re-writing it, producing a non-zero reads count and a meaningful ratio. The simulator loses ~10 points on this axis but scores 75+ on other axes, comfortably above the 60-point threshold.

### RC3 — Stage 1: New-file installs penalised identically to pre-existing-file attacks
**Cause:** The original scoring treated all write events equally. A compiler writing thousands of new `.o` files scored the same as ransomware overwriting thousands of existing user files.

**Fix:** Introduced `PreExistingModifyRatePerSec` as a dedicated feature (weight 25 pts). `ProcessState` tracks newly-created file paths in a `HashSet<string>`. Writes/renames/deletes on paths _not_ in that set count as pre-existing modifications. Installers that create new files score `0` on this axis. The generic `WriteRate` weight was reduced to 10 pts to compensate.

**Tests:** 1 new unit test (42 total, all passing). Validation performed: simulator ransomware workload remained above threshold (score ~75–85); benign installer-pattern workloads dropped below threshold.

**Docs updated:** `docs/false-positive-reduction.md`, `docs/modules/EntropyEngine.md`, `docs/modules/FeatureExtractor.md`, `docs/modules/ScoringEngine.md`, `docs/configuration.md`.

---

## Phase 8d — Runtime Diagnostic & Detection Fix

**Problem:** Simulator workloads did not trigger detection alerts even under heavy load. Dashboard counters (Events Processed, Tracked Processes, Events Dropped, Uptime) permanently displayed 0.

**Root Causes Identified & Fixed:**

### RC1 — CRITICAL: Stage 2 always failed for the simulator (primary detection failure)
**Cause:** `EntropySamplingEngine.AnalyseAsync()` only sampled paths from `RecentWrittenFiles`. The simulator writes high-entropy bytes to `document_N.txt` then immediately renames it to `document_N.txt.locked`. By the time the orchestration tick fired (~2 s later), the original paths no longer existed. `TrySampleFile` silently failed and always returned `IsConfirmed = false`.

**Fix:** `AnalyseAsync` merges both `RecentWrittenFiles` and `RecentRenamedSourceFiles` before sampling. `TrySampleFile` probes 5 common ransomware extensions (`.locked`, `.encrypted`, `.enc`, `.crypto`, `.crypted`) when the original path is unreadable.

### RC2 — STRUCTURAL: Feature extractor had no rename-source tracking
**Cause:** `RecentWrittenFiles` was capped at 5 entries. Rename events were not tracked as Stage 2 candidates at all.

**Fix:** Write queue widened to 20 entries. New `RecentRenamedSourceFiles` queue (bounded to 20) populated from Rename events in `Emit()`. Both fields added to `FeatureSnapshot`.

### RC3 — UI: Live counters permanently displayed 0
**Cause:** `MonitoringStatusService.SetActiveProcessCount()` never called `RaiseChanged()`, so `StatusChanged` never fired for counter changes. `MainWindowViewModel` only re-read values on `StatusChanged`.

**Fix:** `SetActiveProcessCount()` now calls `RaiseChanged()` (fires every ~2 s via orchestration tick). `MainWindowViewModel` adds a `DispatcherTimer` (3-second interval) that raises `PropertyChanged` for high-frequency counters.

### RC4 — Minor: Events Dropped always displayed 0
**Cause:** `PipelineHostService` logged but never propagated the collector's drop count to `MonitoringStatusService`.

**Fix:** Orchestration tick computes the delta between the collector's cumulative drop count and the last-reported value, calling `IncrementEventsDropped()` for the difference.

**Tests:** 5 new unit tests (41 total). Docs updated: `EntropyEngine.md`, `FeatureExtractor.md`, `GUI.md`, `technical-design.md`.

---

## Phase 8c — Simulator Rerun Fix

**Bug Fixed:** `File.Move → IOException` on repeated ransomware workload runs caused by stale `.locked` files from previous runs.

**Fix:** `SimulatorRunner.ResetWorkspace()` clears all files/subdirs inside the workspace before every run. The workspace root is preserved; only its contents are removed.

**Refactor:** Core simulator logic extracted to `SimulatorRunner` (static, no console I/O) for testability. `Program.cs` is now a thin CLI shell.

**Tests:** 17 new unit tests in `SimulatorRunnerTests` covering safety checks, reset behaviour, repeated runs (5×), post-run state, and directory spread. 39 tests total.

**Docs:** `docs/modules/Simulator.md` fully rewritten.

---

## Phase 8b — UX / Tray Refinement

**Changes:**
- `shield.ico` generated and embedded as WPF `<Resource>` in `Detector.GUI.csproj`; resolved via pack URI — no missing-resource crash on launch.
- Severity-aware balloon notification titles (CRITICAL / HIGH / MEDIUM / LOW) with process name and PID.
- Dashboard: added EVENTS DROPPED panel (amber when non-zero), UPTIME panel, alert count label, status bar reflecting live collector state.
- Alert rows now show a colour-coded severity badge and formatted timestamps.
- `AlertRowViewModel` introduced as a thin wrapper around `DetectionAlert` for cleaner per-row XAML bindings.
- Reinstated the UAC elevation relaunch (temporarily commented out during debugging).

**Docs:** `docs/modules/GUI.md` updated.

---

## Phase 8 — Evaluation Readiness & Hardening

- `DataAnnotations` validation on `ActDefendOptions` at startup — invalid configuration crashes cleanly before ETW starts.
- Orphaned ETW session recovery: if a previous crash left `ActDefend-Monitor-Session` active, the collector stops it before recreating.
- `IsRunning` propagation: if the ETW processing loop crashes silently, the orchestration tick detects the `_running = false` state and updates the UI.
- Full documentation set populated.

---

## Phase 7 — Safe Simulator & End-to-End Validation

- `Detector.Simulator` with `--benign` and `--ransomware` modes, configurable file count, delay, and directory depth.
- `AlertRepository` with WAL-mode SQLite persistence using `Microsoft.Data.Sqlite` directly.
- `EndToEndValidationTests` in `Detector.IntegrationTests`: headless pipeline (`Host.CreateDefaultBuilder`) spun up with real ETW, simulator run inside, alerts asserted in SQLite. Tests self-skip when not elevated.

---

## Phase 6 — GUI Dashboard

- WPF application hosted as `IHostedService` on a dedicated STA thread via `WpfHostedService`.
- `MainWindowViewModel` bridges `IMonitoringStatus` and `IAlertPublisher` to WPF bindings.
- Close-to-tray: clicking X hides the window; monitoring continues uninterrupted.

---

## Phase 5 — Entropy Analysis (Stage 2)

- `EntropySamplingEngine` implementing Shannon entropy over bounded file samples.
- Per-process cooldown to limit re-trigger frequency.
- `IEntropyEngine` interface defined in `Detector.Core`.

---

## Phase 4 — Detection Engine (Stage 1)

- `LightweightScoringEngine` with six configurable weighted features.
- `DetectionOrchestrator` wiring Feature Extractor → Stage 1 → Stage 2 → Alert.
- Top-3 explainability in `ScoringResult.Explanation`.

---

## Phase 3 — Feature Extractor

- `FeatureExtractor` with per-PID `ProcessState`, dual sliding windows, six burst metrics.
- `ExpireInactiveState()` garbage-collecting idle PIDs.

---

## Phase 2 — ETW Event Collector

- `EtwEventCollector` using `Microsoft.Diagnostics.Tracing.TraceEvent`.
- Kernel FileIO provider subscription (Create, Write, Read, Rename, Delete).
- Bounded `System.Threading.Channel<FileSystemEvent>` with `DropWrite` backpressure.
- PID → process-name lazy cache.

---

## Phase 1 — Solution Foundations

- `ActDefend.slnx` multi-project solution.
- `Detector.Core`: domain models (`FileSystemEvent`, `DetectionAlert`, `FeatureSnapshot`, `EntropyResult`, `ScoringResult`, `TrustedProcessEntry`), interfaces, `ActDefendOptions` configuration classes.
- `.NET Generic Host` entry point in `Detector.App`.
- Serilog bootstrap logger; UAC elevation check and relaunch.
- `Directory.Build.props` / `Directory.Packages.props` for centralised NuGet management.
