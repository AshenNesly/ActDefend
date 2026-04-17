# Event Collector Module (`Detector.Collector`)

The `Detector.Collector` project is the foundation layer responsible for interfacing safely with the OS Kernel to intercept file interactions linearly and piping them up into the generic decoupled backend.

## Architectural Bounds
The Collector exclusively uses **Event Tracing for Windows (ETW)** natively (via `Microsoft.Diagnostics.Tracing.TraceEvent`). It focuses purely on User-Space File I/O tracking and deliberately avoids writing custom Mini-Filter Kernel Drivers. This provides high stability across end-user environments.

### Required Hooks
Event subscriptions are natively mapped to:
- `FileIOCreate`
- `FileIOReadWrite`
- `FileIODirEnum`
- `FileIOInfo` (mapped for Renames & Deletes)

*Note:* Opening ETW Kernel Sessions explicitly requires **Administrator Privileges**.

## Telemetry Flow and Backpressure
1. **Raw Ingestion**: ETW pushes events at extreme speeds directly into an asynchronous listening loop.
2. **Filtration / Noise Reduction**: Events are sanitized directly evaluating: 
   - Known noise paths (`\Windows\`, `\obj\`, `\node_modules\`, Temp directories)
   - Invalid PIDs (ignoring IDs <= 4)
3. **Decoupled Output**: Clean metrics are mapped to a `FileSystemEvent` struct and pushed to a `System.Threading.Channels` construct.
4. **Backpressure Strategy**: The Channel holds a strictly defined capacity (`EventQueueCapacity` — default `4096`). If this max limit is breached during aggressive bursts, the Collector begins dropping raw ETW payloads natively and increments `EventsDropped`. This successfully prevents the GC from crashing entirely holding raw structs.

## Safety & Hardening (Phase 8)
- ETW exceptions inside the `ReadEventsAsync` bounds gracefully exit resolving deadlocks.
- The ETW session safely detaches on pipeline cancellation avoiding 'Phantom Sessions' requiring manual rebooting natively.
