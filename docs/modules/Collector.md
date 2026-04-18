# Event Collector Module (`Detector.Collector`)

The `Detector.Collector` project is the system's ingress layer. It opens a Windows ETW kernel session, subscribes to file I/O events, normalises them into structured `FileSystemEvent` records, and pushes them into a bounded channel that decouples event collection from feature extraction.

## Implementation

**Class:** `EtwEventCollector` (implements `IEventCollector`, `IDisposable`)

**ETW session name:** `ActDefend-Monitor-Session`

**Library:** `Microsoft.Diagnostics.Tracing.TraceEvent` (NuGet).

The ETW processing loop (`_session.Source.Process()`) is a blocking call. It runs on a dedicated background thread created with `TaskCreationOptions.LongRunning` to avoid occupying a thread-pool thread indefinitely.

---

## Elevation Requirement

Opening an ETW kernel provider session requires **Administrator privileges**. The collector throws `UnauthorizedAccessException` if run without elevation. `PipelineHostService` catches this and halts the pipeline, setting `IsCollectorRunning = false` in the UI.

---

## Subscribed ETW Events

| ETW Callback | Maps to | Notes |
|---|---|---|
| `parser.FileIOCreate` | `FileSystemEventType.Create` | Only for disposition codes `SUPERSEDE` (0), `CREATE_NEW` (2), `CREATE_ALWAYS` (5). Other dispositions (open-existing, open-always) are dropped to avoid inflating write metrics with passive opens. |
| `parser.FileIOWrite` | `FileSystemEventType.Write` | All write events. |
| `parser.FileIORead` | `FileSystemEventType.Read` | All read events. |
| `parser.FileIORename` | `FileSystemEventType.Rename` | Source path only — ETW does not expose the rename destination via `FileIOInfoTraceData`. |
| `parser.FileIODelete` | `FileSystemEventType.Delete` | All delete events. |

`FileIODirEnum` (directory enumeration) is **not** subscribed.

---

## Noise Filtering

Before publishing to the channel, each event is filtered:

| Filter condition | Reason |
|---|---|
| `processId <= 4` | System idle process (PID 0) and System process (PID 4) — not useful for per-process analysis. |
| `string.IsNullOrWhiteSpace(filePath)` | Incomplete ETW payload — drop. |
| Path starts with `C:\Windows\` | High-volume Windows system noise. |
| Path ends with `.TMP` (case-insensitive) | Transient temp-file churn unrelated to ransomware targeting. |

> **Note:** Node module directories, build output directories (`\obj\`), and other developer noise paths are **not currently filtered** at the collector level. Relevant processes (e.g. `node.exe`, `MSBuild.exe`) may appear in Stage 1 under heavy workloads; they are controlled via the trusted-process allow-list or by Stage 1/Stage 2 thresholds.

---

## Bounded Channel & Backpressure

```csharp
_channel = Channel.CreateBounded<FileSystemEvent>(
    new BoundedChannelOptions(8192)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true,
        SingleWriter = true
    });
```

- Capacity: **8 192 events** (hardcoded; the `Collector.EventQueueCapacity` config option is not yet wired to this value — see [configuration.md](../configuration.md)).
- Full-mode: **`DropWrite`** — events are discarded immediately when the channel is full; no blocking. The drop count is incremented atomically and exposed via `DroppedEventCount`.
- `SingleWriter = true`: the ETW processing loop is the only writer, running single-threaded.
- `SingleReader = true`: `PipelineHostService.RunEventReadLoopAsync` is the only consumer.

---

## Process-Name Resolution

ETW FileIO events carry a PID but not a process name. Names are resolved lazily via `Process.GetProcessById()` and cached in a `ConcurrentDictionary<int, string>`. The cache is capped at 5 000 entries and cleared (not evicted selectively) when the cap is reached. Processes that have already terminated return `"Unknown"`.

Full executable path (`ProcessPath`) is **not resolved** — the per-event overhead was judged too high for the hot path. It is `null` throughout the pipeline.

---

## Orphaned Session Recovery

If a previous crash left the `ActDefend-Monitor-Session` ETW session active, the collector detects it via `TraceEventSession.GetActiveSessionNames()` and stops the orphan before creating a new session. This prevents a `Session already exists` exception on relaunch.

---

## Data Flow

```
ETW Kernel → FileIO callback → filter noise → normalize → TryWrite to Channel
                                                                   │
                                                         DropWrite if full (increment DroppedEventCount)
                                                                   │
PipelineHostService ─────────────── ReadAllAsync ◀────────────────┘
```

---

## Interface Contract

```csharp
public interface IEventCollector
{
    bool IsRunning          { get; }
    long DroppedEventCount  { get; }
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    IAsyncEnumerable<FileSystemEvent> ReadEventsAsync(CancellationToken cancellationToken);
}
```

---

## Known Limitations

- `Collector.EventQueueCapacity` config option is defined but not wired to the channel (hardcoded 8 192).
- `Collector.EventQueueTimeoutMs` config option is defined but unused (channel uses `DropWrite`, not a timed wait).
- Rename destination path is unavailable via ETW; only the source path is captured. Stage 2 compensates via extension probing.
- Process full path is always `null`.
