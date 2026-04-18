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
| `Collector.EventQueueCapacity` | `4096` | 1 024–1 000 000 | **Defined but not currently wired to the bounded channel.** The channel is created with a hardcoded capacity of 8 192. This option is reserved for a future refactor that plumbs configuration into `EtwEventCollector`. |
| `Collector.EventQueueTimeoutMs` | `5` | 1–1 000 | **Defined but not currently used.** The channel uses `DropWrite` mode (events are dropped immediately when full — no timeout wait). This option is reserved for a future alternative backpressure strategy. |

> **Important:** The active channel capacity is hardcoded to **8 192** events. If the channel is full, events are silently dropped and the `TotalEventsDropped` counter in the UI increments.

---

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
|---|---|---|
| `TrustedProcesses.DefaultExclusions` | *(list)* | Process names (image names) excluded from scoring at startup. Entries are loaded into memory; additions at runtime are not persisted. |

Default exclusions: `System`, `smss.exe`, `csrss.exe`, `wininit.exe`, `winlogon.exe`, `services.exe`, `lsass.exe`, `svchost.exe`, `MsMpEng.exe`, `SearchIndexer.exe`.

---

## Simulator

These options are read only by `Detector.Simulator`. They have no effect on `Detector.App`.

| Key | Default | Range | Description |
|---|---|---|---|
| `Simulator.WorkspaceDirectory` | `""` | non-empty string | Target workspace path (must be named `simulator-workspace` or `test-workspace`). Must be set before running the simulator via configuration; CLI args override this. |
| `Simulator.FileCount` | `100` | 1–100 000 | Number of files created during a workload run. |
| `Simulator.RenameIntervalMs` | `50` | 0–10 000 | Delay between rename operations during a ransomware workload (ms). |
