# ActDefend Implementation Status

## Current Phase Summary

All planned pipeline phases are complete. The system is fully operational end-to-end: ETW events flow through the pipeline, suspicious processes are scored, entropy is confirmed, alerts are persisted to SQLite, and the WPF dashboard displays live status.

---

## Phase Completion Table

| Phase | Focus | Status | Details |
|:--|:--|:--|:--|
| **Phase 1** | Foundation Architecture | ✅ Complete | Application skeleton, `ActDefend.slnx` layout, domain models (`FileSystemEvent`, `DetectionAlert`, `FeatureSnapshot`), DI container, Serilog bootstrap, UAC elevation relaunch, WPF shell start, unit test skeletons. |
| **Phase 2** | ETW Collection | ✅ Complete | `EtwEventCollector` using `Microsoft.Diagnostics.Tracing.TraceEvent`, FileIO kernel provider, bounded channel backpressure, process-name cache. |
| **Phase 3** | Feature Extraction | ✅ Complete | Per-PID `ProcessState`, dual sliding windows (primary 5 s / context 15 s), six burst features, inline memory pruning. |
| **Phase 4** | Stage 1 Scoring | ✅ Complete | `LightweightScoringEngine` — six weighted, normalised features, configurable suspicion threshold, top-3 explainability. |
| **Phase 5** | Stage 2 Entropy Confirmation | ✅ Complete | `EntropySamplingEngine` — Shannon entropy, bounded sampling, per-process cooldown. |
| **Phase 6** | WPF Dashboard & Tray | ✅ Complete | MVVM dashboard, system-tray balloon notifications, close-to-tray, live status panels. |
| **Phase 7** | SQLite Persistence & Simulator | ✅ Complete | `AlertRepository` with WAL-mode SQLite; `Detector.Simulator` with benign and ransomware workloads; headless integration tests. |
| **Phase 8** | Hardening & Documentation | ✅ Complete | DataAnnotations config validation, ETW crash protection, orphaned-session recovery, full documentation set. |
| **Phase 8b** | UX Refinement | ✅ Complete | Shield tray icon, severity-aware balloon titles, Events Dropped panel, Uptime panel, alert count label, colour-coded severity badges, `AlertRowViewModel`. |
| **Phase 8c** | Simulator Rerun Fix | ✅ Complete | `SimulatorRunner.ResetWorkspace()` clears workspace before every run, preventing `IOException` on repeated ransomware runs. Logic extracted to static `SimulatorRunner` class for testability. 39 tests passing. |
| **Phase 8d** | Runtime Detection Fix | ✅ Complete | Fixed 4 runtime bugs: Stage 2 always failed for simulator (write-then-rename blind spot); rename candidates not tracked; live counters displayed 0; dropped events not propagated to UI. 41 tests passing. |
| **Phase 8e** | False Positive Reduction | ✅ Complete | Added `PreExistingModifyRate` feature (25-pt weight) to distinguish pre-existing file modification from new-file creation. `WriteReadRatio` set to 0.0 when reads = 0 (penalises pure downloaders less). Stage 2 skips known high-entropy-but-benign extensions (`.dll`, `.exe`, `.zip`, `.png`, etc.). 42 tests passing. |

---

## Known Remaining Gaps

| Area | Gap | Risk |
|---|---|---|
| `EventQueueCapacity` config | The `Collector.EventQueueCapacity` option is defined in config but the channel is created with a hardcoded capacity of 8 192. The config value is not wired to the channel. | Low — hardcoded value is 2× the config default; no functional impact unless tuning is needed. |
| `EventQueueTimeoutMs` config | Defined in config but unused. The channel uses `DropWrite` mode (immediate drop, no timeout). | Low — no functional impact. |
| Trusted-process persistence | `TrustedProcessRepository` loads defaults from config into memory. Runtime additions are not persisted to SQLite and are lost on restart. | Medium — users must re-add exclusions after restart. |
| Stage 1 threshold calibration | Default thresholds calibrated against simulator workloads; heavy benign writes (disk backups, large IDE builds) may still cross the threshold under sustained load. | Medium |
| ETW rename destination | Rename events carry only the source path; the new filename is unavailable via `FileIOInfoTraceData`. Stage 2 works around this via extension probing. | Low (mitigated) |
| ProcessPath resolution | Full executable path is `null` for all processes. ETW FileIO events do not carry it, and an explicit `Process.MainModule.FileName` lookup per event was excluded for performance. | Low |
| Extension probe coverage | The fixed set of 6 ransomware extensions may not cover novel ransomware using unusual extensions. Written-file paths are the primary fallback when no probe succeeds. | Low–Medium |

---

## Next Technical Steps

1. **Calibration run** — run the simulator at various speeds and measure true/false positive rates against the current thresholds to verify the detection chain works end-to-end at runtime.
2. **Benign workload validation** — run IDE builds, disk backups, and large unzips while monitoring to confirm Stage 1 does not false-alert on known-safe processes.
3. **Evaluation report** — produce initial detection-latency and accuracy measurements for the academic evaluation section.
4. **Trusted-process SQLite persistence** — wire `TrustedProcessRepository` to the SQLite database so user-added exclusions survive restarts.
5. **Wired collector config** — plumb `Collector.EventQueueCapacity` into `EtwEventCollector` so the config option takes effect.
