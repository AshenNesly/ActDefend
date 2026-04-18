# Testing & Evaluation

## Current Test Coverage

### Unit Tests (`tests/Detector.UnitTests`)

42 unit tests, all passing. No elevation required. Covers:

| Test class | Area covered |
|---|---|
| `FeatureExtractorTests` | Sliding-window metrics, ratio calculation, `WriteReadRatio = 0.0` when reads = 0, rename-source tracking, write-queue bound (≤ 20), state expiry |
| `EntropySamplingEngineTests` | Shannon entropy calculation, cooldown tracking, empty candidate list, Stage 2 confirmation via `.locked` extension probe, candidate deduplication, non-confirmation when `.locked` file has low entropy |
| `ScoringEngineTests` | Score calculation per feature, threshold cap (100), `IsSuspicious` flag, score = 0 for empty snapshot |
| `SimulatorRunnerTests` | Workspace safety checks, reset behaviour, ransomware workload runs 5× without crash, post-run state (only `.locked` files remain), directory spread, benign workload produces only `.txt` files |
| `CoreModelTests` | Model construction, defaults, immutability |

### Integration Tests (`tests/Detector.IntegrationTests`)

End-to-end tests that require Administrator elevation. Auto-skip when run without elevation.

`EndToEndValidationTests.cs` spins up a headless pipeline using `Host.CreateDefaultBuilder`:
- ETW collector (`Detector.Collector`)
- Feature extractor (`Detector.Features`)
- Stage 1 + Stage 2 scoring (`Detector.Detection`, `Detector.Entropy`)
- SQLite alert storage (`Detector.Storage`)

The simulator is invoked against a dedicated test workspace. Tests assert that:
- Ransomware workloads produce confirmed alerts saved to SQLite.
- The end-to-end latency (simulator start → first alert in SQLite) is within the expected window (~2× `EmitIntervalSeconds`).

---

## What the Simulator Tests

The safe simulator (`Detector.Simulator`) generates controlled file-system workloads that mimic ransomware patterns without any actual malicious intent:

### `--ransomware` workload
1. Creates N victim `.txt` files spread across a configurable directory tree depth.
2. Pauses 1 second (lets the collector establish a baseline).
3. Overwrites each file with 8 KiB of random bytes (high entropy).
4. Renames each file to `.locked`.

This pattern exercises all five major Stage 1 signals simultaneously:
- High `WriteRate` and `RenameRate`
- High `UniqueFilesWritten` and `DirectorySpread`
- High `PreExistingModifyRate` (the `.txt` files existed before Phase 2)

The write-then-rename pattern also validates the Stage 2 extension-probe fix: the original paths no longer exist when Stage 2 runs, so the engine must find the `.locked` renamed variants.

### `--benign` workload
Writes low-entropy text files at a slow rate across a single directory. Designed to produce no Stage 1 alerts. Used to validate true-negative behaviour.

---

## Known Measurement Gaps

The current test suite validates detection correctness (does the pipeline detect the simulator?) and non-detection correctness (does the benign workload not fire?). The following evaluation dimensions are not yet formally measured:

| Metric | Status |
|---|---|
| **Time to Detect (TTD)** | Measured informally (first alert appears within 2–4 s). No automated assertion on latency. |
| **False Positive Rate under sustained benign load** | Not formally measured. IDE builds, npm install, and disk backup have been tested manually but not in a repeatable automated harness. |
| **Memory overhead under sustained monitoring** | Not measured. `ProcessState` growth is bounded by `ContextWindowSeconds` and `InactivityExpirySeconds`. |
| **CPU overhead** | Not measured. ETW callback and scoring are designed to be lightweight, but no profiling data exists. |
| **Receiver Operating Characteristic (ROC)** | Not measured. Would require a systematic sweep of `SuspicionThreshold` values. |

---

## Evaluation Strategy (Planned)

To produce the academic evaluation metrics, the recommended approach is:

1. **Calibration sweep:** Run the ransomware simulator at `--delay-ms 0`, `--delay-ms 10`, `--delay-ms 50`, `--delay-ms 200` (slow encryption). Record the first alert timestamp relative to simulator start to get TTD at each speed.

2. **False-positive battery:** Run the following benign workloads while monitoring and assert no alerts:
   - `npm install` in a large project
   - `dotnet build` in a large solution
   - 7-Zip extracting a large archive
   - `robocopy` copying a large directory tree

3. **Threshold sweep:** Vary `SuspicionThreshold` from 40 to 80 in steps of 5 and record TP/FP counts. Plot as ROC.

4. **Memory profile:** Run the ransomware simulator at `--file-count 500 --delay-ms 0` and record peak `ProcessState` count and total managed heap.
