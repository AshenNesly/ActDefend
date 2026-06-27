# Configuration Reference

All tunable parameters are centralised in `src/Detector.App/appsettings.json` under the `"ActDefend"` key and bound to `ActDefendOptions` at startup.

Configuration is validated with `DataAnnotations` on startup (`.ValidateDataAnnotations().ValidateOnStart()`). An out-of-range value crashes the host with a clear error before any ETW session is opened.

> **Reload at runtime is not supported.** Changes require an application restart.

---

## Full appsettings.json Structure

```json
{
  "ActDefend": {
    "Logging": { ... },
    "Storage": { ... },
    "Collector": { ... },
    "Features": { ... },
    "Stage1": {
      "Weights": { ... },
      "Thresholds": { ... }
    },
    "Stage2": { ... },
    "TrustedProcesses": { ... },
    "Simulator": { ... }
  }
}
```

---

## Logging

| Key | Default | Range / Values | Description |
|---|---|---|---|
| `Logging.LogDirectory` | `"logs"` | non-empty string | Directory for rolling JSON log files. Relative paths resolve from the executable directory. |
| `Logging.RollingInterval` | `"Day"` | `Hour`, `Day`, `Month`, `Year`, `Infinite` | How often the log file rolls to a new file. |
| `Logging.RetainedFileCountLimit` | `30` | 1–365 | Number of log files retained before the oldest is deleted. |
| `Logging.MinimumLevel` | `"Information"` | `Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal` | Minimum log event level written to both sinks. |

---

## Storage

| Key | Default | Description |
|---|---|---|
| `Storage.DatabasePath` | `"actdefend.db"` | Path to the SQLite database file. Relative paths resolve beside the executable. The file is created automatically on first run. |

---

## Collector

| Key | Default | Range | Description |
|---|---|---|---|
| `Collector.EventQueueCapacity` | `4096` | 1 024–1 000 000 | Bounded channel capacity between the ETW callback and the downstream processing pipeline. Fully wired to `EtwEventCollector` — changing this value and restarting takes effect immediately. Decrease for low-resource environments; increase if `TotalEventsDropped` is climbing. |
| `Collector.EventQueueTimeoutMs` | `5` | 1–1 000 | Reserved for a future alternative backpressure strategy. The channel currently uses `DropWrite` mode (events are dropped immediately when full). |

---

## Dashboard Tuning (Settings / Tuning Tab)

The **SETTINGS / TUNING** dashboard tab provides a safe, GUI-driven interface for adjusting the key parameters below without directly editing `appsettings.json`.

### Preset Profiles

Four preset profiles are provided. Selecting a profile populates all exposed fields with a tested, safe set of values:

| Profile | Sensitivity | Resources | Notes |
|---|---|---|---|
| **Balanced** | Medium | Medium | Default recommended. Good balance between accuracy and false positives. |
| **Sensitive** | High | Medium-High | Detects earlier. May increase false positives. |
| **Low Resource** | Low-Medium | Low | Reduces CPU/memory pressure. May detect slightly slower. |
| **Conservative** | Low | Medium | Reduces false positives. May miss very slow ransomware-like behaviour. |

### Safe Tuning Ranges (Dashboard Validation)

The dashboard enforces the following safe ranges. Values outside these bounds will show a validation error and **cannot be saved**:

| Parameter | Safe Min | Safe Max |
|---|---|---|
| `Stage1.SuspicionThreshold` | 40 | 90 |
| `Stage2.EntropyThreshold` | 6.5 | 8.0 |
| `Stage2.ConfirmationMinFiles` | 1 | 10 |
| `Stage2.MaxFilesToSample` | 1 | 50 |
| `Features.PrimaryWindowSeconds` | 2 | 15 |
| `Features.ContextWindowSeconds` | 5 | 60 |
| `Features.EmitIntervalSeconds` | 1 | 10 |
| `Features.InactivityExpirySeconds` | 30 | 600 |
| `Collector.EventQueueCapacity` | 1 024 | 100 000 |
| Each Stage 1 Weight | 0 | 50 |
| Total Stage 1 Weights | — | 100 |

> **Restart required.** Settings saved through the dashboard are written to `appsettings.json`. A restart of ActDefend is required for the changes to take effect.

## Features (Sliding Windows)

| Key | Default | Range | Description |
|---|---|---|---|
| `Features.PrimaryWindowSeconds` | `5` | 1–60 | Short burst-detection window. All six Stage 1 metrics are computed over this window. |
| `Features.ContextWindowSeconds` | `15` | 5–300 | Wider stabilisation window. Events older than this are pruned from memory. Must be ≥ `PrimaryWindowSeconds`. |
| `Features.EmitIntervalSeconds` | `2` | 1–60 | How often the orchestration tick fires (i.e. how often Stage 1 is evaluated). |
| `Features.InactivityExpirySeconds` | `120` | 10–3 600 | A PID with no file events for this long has its `ProcessState` evicted from memory. |

---

## Stage 1 — Lightweight Scoring

### Suspicion Threshold

| Key | Default | Range | Description |
|---|---|---|---|
| `Stage1.SuspicionThreshold` | `60.0` | 1.0–1 000.0 | Minimum composite score (0–100) required to proceed to Stage 2. |

### Feature Weights

Each weight is the maximum point contribution of that feature to the composite score.
**All weights must sum to ≤ 100** to keep the score on a 0–100 scale.

| Key | Default | Range | Feature |
|---|---|---|---|
| `Stage1.Weights.WriteRate` | `10.0` | 0–100 | Write events per second in the primary window |
| `Stage1.Weights.UniqueFilesWritten` | `15.0` | 0–100 | Distinct file paths written in the primary window |
| `Stage1.Weights.RenameRate` | `20.0` | 0–100 | Rename events per second in the primary window |
| `Stage1.Weights.DirectorySpread` | `20.0` | 0–100 | Distinct directories touched in the primary window |
| `Stage1.Weights.WriteReadRatio` | `10.0` | 0–100 | Writes ÷ reads in the primary window (0.0 when reads = 0) |
| `Stage1.Weights.PreExistingModifyRate` | `25.0` | 0–100 | Writes/renames/deletes on pre-existing files per second |

### Normalisation Thresholds

The value that maps each feature to its full weight. Values above the threshold are capped at the full weight contribution.

`Contribution = min(actual / threshold, 1.0) × weight`

| Key | Default | Range | Description |
|---|---|---|---|
| `Stage1.Thresholds.WriteRatePerSec` | `10.0` | 0.1–1 000 | Writes/sec that earns full WriteRate weight |
| `Stage1.Thresholds.UniqueFilesPerWindow` | `30` | 1–10 000 | Unique files that earns full UniqueFilesWritten weight |
| `Stage1.Thresholds.RenameRatePerSec` | `5.0` | 0.1–1 000 | Renames/sec that earns full RenameRate weight |
| `Stage1.Thresholds.UniqueDirectoriesPerWindow` | `10` | 1–1 000 | Unique dirs that earns full DirectorySpread weight |
| `Stage1.Thresholds.WriteReadRatioMax` | `5.0` | 0.1–100 | Ratio that earns full WriteReadRatio weight |
| `Stage1.Thresholds.PreExistingModifyRatePerSec` | `5.0` | 0.1–1 000 | Pre-existing modify rate that earns full PreExistingModifyRate weight |

---

## Stage 2 — Entropy Sampling

| Key | Default | Range | Description |
|---|---|---|---|
| `Stage2.EntropyThreshold` | `7.2` | 0.0–8.0 | Minimum Shannon entropy (bits/byte) for a file to be counted as high-entropy. Encrypted/compressed data typically scores 7.5–8.0; plaintext 4.0–6.0. |
| `Stage2.SampleBytesLimit` | `65536` | 1 024–1 048 576 | Maximum bytes read from each file for entropy calculation (64 KiB default). |
| `Stage2.MaxFilesToSample` | `5` | 1–100 | Maximum candidate files sampled per Stage 2 trigger. |
| `Stage2.ConfirmationMinFiles` | `2` | 1–50 | Minimum number of high-entropy files required for confirmation (`IsConfirmed = true`). |
| `Stage2.CooldownSeconds` | `10` | 1–3 600 | Per-process minimum interval between Stage 2 runs. Prevents thrashing under sustained Stage 1 triggers. |

---

## Trusted Processes

| Key | Default | Description |
| `TrustedProcesses.DefaultExclusions` | *(list)* | Process names excluded from scoring at startup. Default entries are loaded alongside user-added entries persisted in the SQLite database. User additions survive restarts. |

Default exclusions: `System`, `smss.exe`, `csrss.exe`, `wininit.exe`, `winlogon.exe`, `services.exe`, `lsass.exe`, `svchost.exe`, `MsMpEng.exe`, `SearchIndexer.exe`.

---

## Simulator

These options are read only by `Detector.Simulator`. They have no effect on `Detector.App`.

| Key | Default | Range | Description |
|---|---|---|---|
| `Simulator.WorkspaceDirectory` | `""` | non-empty string | Target workspace path (must be named `simulator-workspace` or `test-workspace`). Must be set before running the simulator via configuration; CLI args override this. |
| `Simulator.FileCount` | `100` | 1–100 000 | Number of files created during a workload run. |
| `Simulator.RenameIntervalMs` | `50` | 0–10 000 | Delay between rename operations during a ransomware workload (ms). |
