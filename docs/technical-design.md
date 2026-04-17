# Technical Design

ActDefend uses a heavily decoupled architectural approach executing smoothly within a standard `.NET Generic Host`. 

The central pipeline operates using cross-threaded bounds managing backpressure and decoupling logic seamlessly.

## Bounded Pipeline Approach
1. **Source Hook (`Detector.Collector`):** `EtwEventCollector` opens an explicit ETW Kernel Session and pushes valid File IO metrics into an internal `BoundedChannel` with a default cap of 4096 objects safely protecting the GC natively.
2. **Snapshot Logic (`Detector.Features`):** Discards native objects cleanly executing state transitions. Unused metrics silently expire resolving memory constraints over `InactivityExpirySeconds`.
3. **Detection Core (`Detector.Detection`):** `DetectionOrchestrator` maps `SuspicionThreshold` bounds locally enforcing checks natively over weighted rules (WriteRate, RenameRate, Directory Spread).
4. **Entropy Engine (`Detector.Entropy`):** Stage 2 specifically invokes bounded byte-testing metrics confirming `.locked` mathematical limits over small block buffers cleanly protecting CPU limits perfectly natively.
5. **UI Output (`Detector.GUI` & `Detector.Storage`):** An asynchronous hook pushes alerts safely to local `.db` tracking maps cleanly updating WPF UI Elements across `WpfHostedService.cs` Threaded limits natively.
