# Simulator Module

The `Detector.Simulator` is an internal strictly bounded testing executable designed to mimic malicious cryptographic locks and high-entropy payload bursts testing the specific boundaries defined structurally in ActDefend's EDR Kernel mapping. In Phase 7, the simulator was upgraded to be highly configurable.

## Explicit Safety Bounds
Any engineering team executing the simulator must explicitly configure internal directory paths when launching. The simulator guards against destructive runs by validating the target working directory explicitly equals `"simulator-workspace"` or `"test-workspace"`.

Example:
`ActDefend.Simulator.exe --ransomware "C:\temp\simulator-workspace"`

## Workloads

- **Benign:** Mimics simple log writing. Designed cleanly for validating `True Negative` assumptions checking limits against false UI Alert deployments.
- **Ransomware:** Executes massive arrays rapidly dumping randomly generated mathematical byte pools resolving heavily against `.locked` payload limits dropping over Stage 1 mapping hooks explicitly validating alert chains natively in milli-seconds. Includes **directory spread** capabilities.

## Configuration Options

As of Phase 7, the simulator is tightly configurable to measure detection latency and test evasion techniques:
- `--delay-ms <ms>` : Configurable delay between file operations to test Stage 1 timing limits.
- `--file-count <count>` : Generates exactly N dummy files to validate burst detection constraints.
- `--dir-depth <depth>` : Deploys payloads across deep nested subdirectories validating directory-spreading rule heuristics.
