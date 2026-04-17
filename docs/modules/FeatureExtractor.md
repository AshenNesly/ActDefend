# Feature Extractor Module

The `Detector.Features` project hosts the `FeatureExtractor` responsible for translating raw high-velocity Event Tracing for Windows (ETW) telemetry into analyzable, time-bounded metadata (Features) required by the Stage 1 Detection Engine.

## Core Concepts

### Time Windows (Sliding Rolling Aggregation)
Ransomware encryption is defined structurally by speed and intensity (bursts). To provide early detection without waiting for a massive timeline, the Extractor relies on a Dual-Window sliding bounds approach driven by Central Configuration (`ActDefendOptions.Features`).

1. **Primary Window (The Burst Frame)**
   - Default: `5 Seconds`
   - Governs all severe indicators `(WritesPerSec, RenamePerSec, UniqueFiles, Ratio)`.
2. **Context Window (The Horizon Frame)**
   - Default: `15 Seconds`
   - Stores the raw telemetry array. It prevents the system from having amnesia about a file created 6 seconds ago if it's evaluated for a burst now. Prevents total memory explosion.

### Per-Process Isolation Mapping
A single process on the host machine acts identically, tracking an explicit `ProcessState` element.
`ConcurrentDictionary<int, ProcessState>`
- `Key`: **ProcessId**
- `Value`: **ProcessState** object housing an isolated `lock` boundary and a `List<FileSystemEvent>`. 
    - *Why a Lock inside a high-speed system?* By locking individual instances at the PID layer, total processing block rates are minimal over localized ransomware payloads against low-end CPUs, completely eliminating massive dictionary `ToArray()` cloning pauses.

### Memory Pruning & Bound Growth 
The Extractor prunes dynamically through two explicit pipeline operations handled independently of event sourcing:
1. **Emit() Pruning:** During Pipeline Tick, events older than `UtcNow - ContextWindow` are sliced manually during array extraction entirely purging stale memory.
2. **ExpireInactiveState() Collection:** Periodically evaluated, clearing out orphaned Dictionary entries (processes that exit or go dormant for > `120s`) minimizing continuous iteration loops for the core `Emit()` execution sequence.

---

## Feature Explanations
These are mathematically derived against the *Primary Window Burst Interval*:

- `WriteRatePerSec`: `Total Primary Writes / Primary Window Interval`
- `RenameRatePerSec`: `Total Primary Renames / Primary Window Interval`
- `UniqueFilesWritten`: Discards rewrite cycles against the identically targeted file path (`Count` of `HashSet<string>`).
- `WriteReadRatio`: Maps explicit encryption flows. Often encryption triggers 1 Read -> 1 Write string loops. Maxed out completely to `double.MaxValue` if writing raw files exclusively without previous reading sequences.
