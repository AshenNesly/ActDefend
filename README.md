# ActDefend — Lightweight Behavioural Ransomware Detection System

ActDefend is a Windows desktop ransomware early-warning system designed for low-resource environments (SMEs, educational labs, individual endpoints). It monitors per-process file behaviour in real time using Event Tracing for Windows (ETW), without requiring kernel drivers or heavy machine-learning infrastructure.

## How It Works

Detection runs as a two-stage pipeline:

```
ETW Kernel Events
  └─▶ EtwEventCollector       (Detector.Collector)   — normalise & buffer file I/O events
        └─▶ FeatureExtractor  (Detector.Features)    — sliding-window burst metrics per process
              └─▶ Stage 1: LightweightScoringEngine  (Detector.Detection) — weighted score [0–100]
                    └─▶ (if score ≥ 60) Stage 2: EntropySamplingEngine (Detector.Entropy)
                                        — Shannon entropy check on recently written/renamed files
                          └─▶ (if confirmed) DetectionOrchestrator raises DetectionAlert
                                              → SQLite (Detector.Storage)
                                              → WPF dashboard + tray balloon (Detector.GUI)
                                              → Rolling JSON log (Detector.Logging)
```

**Stage 1** computes six weighted behavioural features over a 5-second sliding window:
`WriteRate`, `UniqueFilesWritten`, `RenameRate`, `DirectorySpread`, `WriteReadRatio`, and `PreExistingModifyRate`.
A composite score ≥ 60 (default) flags the process as suspicious and triggers Stage 2.

**Stage 2** samples up to 5 recently written or renamed files and measures their Shannon entropy.
Entropy ≥ 7.2 bits/byte (default) on ≥ 2 files confirms the detection. If the original file was renamed
(e.g. `doc.txt → doc.txt.locked`), the engine probes common ransomware extensions automatically.

## Requirements

| Requirement | Detail |
|---|---|
| OS | Windows 10 / 11 (64-bit) |
| Runtime | .NET 10 |
| Privileges | **Administrator required** — ETW kernel file-I/O sessions cannot run unprivileged |

## Quick Start

```powershell
# Build
dotnet build ActDefend.slnx -c Release

# Run (must be elevated — the app will trigger UAC automatically if not)
dotnet run --project src/Detector.App
```

### Safe Simulator (testing without real malware)

```powershell
# Ransomware-like workload — should trigger detection
dotnet run --project src/Detector.Simulator -- --ransomware .\simulator-workspace --file-count 50 --delay-ms 0 --dir-depth 5

# Benign workload — should NOT trigger detection
dotnet run --project src/Detector.Simulator -- --benign .\simulator-workspace
```

The simulator **always resets the workspace** before running, so repeated runs are safe.

## Project Structure

```
ActDefend.slnx
├── src/
│   ├── Detector.App          — .NET Generic Host entry point, DI wiring, PipelineHostService
│   ├── Detector.Core         — Shared models, interfaces, configuration (ActDefendOptions)
│   ├── Detector.Collector    — ETW event collector (EtwEventCollector)
│   ├── Detector.Features     — Feature extractor, per-process sliding-window state
│   ├── Detector.Detection    — Stage 1 scoring engine + DetectionOrchestrator
│   ├── Detector.Entropy      — Stage 2 Shannon entropy sampling engine
│   ├── Detector.Storage      — SQLite alert persistence + in-memory trusted-process list
│   ├── Detector.GUI          — WPF dashboard + system-tray integration
│   ├── Detector.Logging      — Serilog setup (console + rolling JSON file)
│   └── Detector.Simulator    — Safe CLI ransomware-behaviour simulator (test-only)
├── tests/
│   ├── Detector.UnitTests         — 42 unit tests (scoring, entropy, features, simulator)
│   └── Detector.IntegrationTests  — Elevated end-to-end pipeline tests
└── docs/
    ├── architecture.md            — System architecture and component map
    ├── configuration.md           — All appsettings.json options with defaults & ranges
    ├── technical-design.md        — Key design decisions and pipeline internals
    ├── false-positive-reduction.md — FP tuning: PreExistingModifyRate, benign-extension exclusions
    ├── status.md                  — Phase-by-phase implementation status
    ├── changelog.md               — Detailed change log per phase
    ├── installation.md            — Build and run instructions
    ├── user-guide.md              — Dashboard and tray usage guide
    ├── testing-and-evaluation.md  — Test strategy and evaluation plan
    └── modules/
        ├── Collector.md           — ETW collector internals
        ├── FeatureExtractor.md    — Sliding-window feature extraction
        ├── ScoringEngine.md       — Stage 1 weighted scoring
        ├── EntropyEngine.md       — Stage 2 entropy confirmation
        ├── GUI.md                 — WPF dashboard and tray
        ├── Storage.md             — SQLite persistence layer
        ├── Simulator.md           — Safe ransomware simulator
        ├── Logging.md             — Serilog logging setup
        └── EndToEndValidation.md  — Integration test approach
```
