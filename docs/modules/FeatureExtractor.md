# Feature Extractor Module

The `Detector.Features` project hosts the `FeatureExtractor` responsible for translating raw high-velocity Event Tracing for Windows (ETW) telemetry into analyzable, time-bounded metadata (FeatureSnapshots) required by the Stage 1 Detection Engine and Stage 2 Entropy Engine.

## Core Concepts

### Time Windows (Sliding Rolling Aggregation)
Ransomware encryption is defined structurally by speed and intensity (bursts). To provide early detection without waiting for a massive timeline, the Extractor relies on a Dual-Window sliding bounds approach driven by Central Configuration (`ActDefendOptions.Features`).

1. **Primary Window (The Burst Frame)**
   - Default: `5 seconds`
   - Governs severe indicators: `WriteRatePerSec`, `RenameRatePerSec`, `UniqueFilesWritten`, `WriteReadRatio`
2. **Context Window (The Horizon Frame)**
   - Default: `15 seconds`
   - Stores the raw telemetry array. Prevents amnesia about a file created 6 seconds ago; prevents total memory explosion.

### Per-Process Isolation Mapping
A single `ConcurrentDictionary<int, ProcessState>` maps each PID to an isolated `ProcessState` with its own lock boundary and internal event list. Locking at PID granularity keeps blocking contention minimal during burst conditions.

### Memory Pruning & Bound Growth
1. **Emit() Pruning:** Events older than `UtcNow - ContextWindow` are removed during pipeline tick.
2. **ExpireInactiveState() Collection:** Periodically clears orphaned dictionary entries for PIDs silent for > `120 s`.

---

## Feature Explanations

All metrics derived from the *Primary Burst Window*:

| Feature | Formula |
|---|---|
| `WriteRatePerSec` | `PrimaryWrites / PrimaryWindowSeconds` |
| `RenameRatePerSec` | `PrimaryRenames / PrimaryWindowSeconds` |
| `UniqueFilesWritten` | Count of distinct write-event file paths in primary window |
| `UniqueDirectoriesTouched` | Count of distinct parent directories from any event type |
| `WriteReadRatio` | `Writes / Reads` |

*Note on `WriteReadRatio`:* When reads are exactly 0, the ratio evaluates to `0.0`. Pure write-bursts are distinctly characteristic of safe extraction utilities/download streams, and ransomware necessarily requires reading original structure sequentially. Inflating zero-reads removes penalty bounds across innocuous payloads.*

---

## Stage 2 Candidate Tracking (Phase 8 Fix)

**Root cause fixed:** Previously, `RecentWrittenFiles` captured only the last **5** write-event paths via a naive queue. Under a ransomware write-then-rename pattern, all those paths were renamed away before Stage 2 ran, so `TrySampleFile` found nothing and returned `IsConfirmed = false` for every simulator run.

**Fixes applied:**

1. **Write queue widened to 20 entries** — provides more candidates for fast-burst scenarios.

2. **`RecentRenamedSourceFiles` added** — a new parallel queue (also bounded to 20) that tracks the *source path* of each rename event in the primary window. These are the original file names before extension substitution (e.g. `document_0.txt` before being renamed to `document_0.txt.locked`).

Both lists are populated in `Emit()` and included in `FeatureSnapshot`. Stage 2 merges them and probes for renamed variants when the original path cannot be read. See `docs/modules/EntropyEngine.md` for probe details.

---

## FeatureSnapshot Fields

| Field | Source |
|---|---|
| `WriteRatePerSec` | Primary window write count |
| `UniqueFilesWritten` | Primary window write HashSet |
| `RenameRatePerSec` | Primary window rename count |
| `UniqueDirectoriesTouched` | Primary window all-event HashSet |
| `WriteReadRatio` | Primary window write/read division |
| `TotalWritesInContext` | Full context window write count |
| `TotalRenamesInContext` | Full context window rename count |
| `RecentWrittenFiles` | Last ≤ 20 written file paths from primary window |
| `RecentRenamedSourceFiles` | Last ≤ 20 rename-source paths from primary window |

## Configuration Reference

| Key | Default | Description |
|---|---|---|
| `Features.PrimaryWindowSeconds` | `5` | Short burst detection window |
| `Features.ContextWindowSeconds` | `15` | Wider stabilisation window |
| `Features.EmitIntervalSeconds` | `2` | How often orchestrator ticks |
| `Features.InactivityExpirySeconds` | `120` | Idle PID state expiry |

## Testing

Unit tests in `tests/Detector.UnitTests/Features/FeatureExtractorTests.cs` cover:

- Ratio calculation correctness
- `double.MaxValue` ratio when no reads
- Reads-only produces no snapshot (no score contribution)
- State expiry logic
- **`RecentRenamedSourceFiles` populated from rename events** (root cause regression test)
- **`RecentWrittenFiles` bounded at ≤ 20 entries** (queue bound validation)
