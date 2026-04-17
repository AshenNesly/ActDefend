# End-to-End Validation

The validation mapping operates against real `ActDefend.Simulator` executable runs executing inside `Detector.IntegrationTests`.

## Headless Pipeline Architecture (Phase 7)
True End-to-End mappings against an Enterprise Detection and Response tool require validating the exact ETW ingress paths. Instead of just running the simulator dryly, `EndToEndValidationTests.cs` instantiates a **UI-Less Headless Pipeline** utilizing `Host.CreateDefaultBuilder`.

This spins up the same components used in `ActDefend.App`:
- `Detector.Collector` (ETW ingress)
- `Detector.Features` (Sliding window snapshots)
- `Detector.Detection` (Stage 1 Scoring)
- `Detector.Storage` (SQLite persistent alerts)

## Execution Bounds

The unit tests mapped internally verify mathematically the limits surrounding structural payload injections:
- Attempting to bypass limits against real directories using randomized structures.
- Confirming Simulator limits push natively into the SQLite Database bounds tracking outputs cleanly back through the C# Application orchestrator loops safely validating alerts.

`EndToEndValidationTests` skips natively if run from non-elevated shells. Administrator privileges are actively required binding the ETW hook safely, preventing CI runner blocks dynamically restricting the overall test execution context automatically.
