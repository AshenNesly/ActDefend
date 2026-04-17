# Evaluation Support Plan for ActDefend

While ActDefend includes a safe constrained `Detector.Simulator` and unit/integration testing limits evaluating raw pipeline states, true production scaling of an EDR engine requires rigorous independent Evaluation matrices safely confirming False Positive vs. True Positive capabilities over extended lifespans.

This document details the exact plan mapping test engineering implementations into the ActDefend architecture in the subsequent major product iterations.

## 1. Required Architecture Fits

The current ActDefend pipeline sits on heavily decoupled structured logs natively built for evaluation hooks:
- **AlertRepository (SQLite)**: Persists all triggers automatically against correlation markers.
- **IMonitoringStatus**: Tracks dropped metrics proving pipeline fatigue natively.

To execute evaluations, the system requires an explicit `Detector.TestRunner` executable layered gracefully **above** both `Detector.Simulator` and `Detector.App`.

## 2. What Must Be Measured

Data mappings require analyzing explicitly:
- **Time to Detect (TTD)**: Measured down to the millisecond delta resolving the difference between the Simulator injecting bytes and the Engine pushing the output Alert UI.
- **False Positive Bounds**: Operating native compiling workloads structurally tracking metrics asserting pipeline Suspicion never throws accidentally across wide developer boundaries.
- **Receiver Operating Characteristic (ROC)**: Plotting exactly when Entropy fails against plain-text compression boundaries (e.g. running 7zip internally directly triggers low boundaries missing ransomware mapping structurally).

## 3. Storage and Metric Exports (Evaluation Artifacts)

The future `TestRunner` must output raw structured JSON arrays mapping outputs safely out from SQLite metrics tracking:
1. `TestRun.Id` mapping back to unique run variables natively tracking the tested `SuspicionThreshold`.
2. A `.csv` tracking memory loops demonstrating memory limits.

## 4. End-to-End Core Validation (Phase 7 Implemented)

End-to-End validation proves the pipeline actually processes ETW effectively. As demonstrated in `EndToEndValidationTests.cs`:
- The core Pipeline executes via a UI-Less `Generic Host` pattern mirroring `Detector.App`.
- Tests safely assert that `--benign` simulators drop cleanly.
- Tests verify `--ransomware` pipelines generate high-severity alerts persistently parsed through SQLite repositories.

## 5. Workload Segregation and Evaluation Strategy

Evaluation requires strict `Test` vs `Clean` system matrices measuring:
- **Slow Encryption** variables attempting to dodge the bounds configured in `PrimaryWindowSeconds`.
- **Random Access** attempting to bypass strictly defined rename configurations dynamically.
- **Directory Spread** bypassing single-folder rule limits (mitigated successfully by evaluating structural trees).

*This plan restricts all tests against safe designated Dummy bounds exclusively.*
