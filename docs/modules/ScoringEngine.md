# Lightweight Scoring Engine (Stage 1)

The `Detector.Detection` project encapsulates the primary suspicion calculations, functioning as a real-time heuristics scanner evaluating `FeatureSnapshots` emitted by the Stage 1 Feature Extractor.

## Core Concepts

### Suspicion Threshold
The core metric is the **Suspicion Score**, mapped on a strictly normalized `0.0` to `100.0` scale. By default, any process achieving a score `>= 60.0` triggers a positive anomaly alert, pushing the logic down into the Stage 2 (Entropy) confirmation scanner.

### The Algorithm
Unlike black-box Machine Learning pipelines, ActDefend intentionally implements an **explainable linear map**.

1. **Metrics Definition:** The engine evaluates 5 variables: `WriteRate`, `UniqueFilesWritten`, `RenameRate`, `DirectorySpread`, and the `WriteReadRatio`.
2. **Dynamic Thresholds:** Each variable has a theoretical tuning 'Limit' (e.g. `10.0 Writes Per Second`).
3. **Weights:** Each variable possesses an active Weight configuration summing to 100 max points (e.g. `20 Points` for WriteRate).

For every metric:
`Contribution Score = MIN((Actual Value / Defined Threshold), 1.0) * Max Weight`

This guarantees that a single anomaly (e.g. extremely rapid writing but zero renames) won't falsely hit a 100 score, but across enough axes, a process exhibiting ransomware patterns will reliably breach the 60 threshold limit.

### Configuration Target
The entire mathematical structure is defined in `appsettings.json` under `ActDefend:Stage1` allowing deployment teams to dynamically tighten or loosen threshold limits without recompiling the `.NET 10` application.

### Explainability
No process triggers arbitrarily. When `Score > 60` is achieved, the engine dynamically builds a human-readable statement isolating the `Top 3 Contributing Factors`. This ensures the eventual Database logs and Application GUI explicitly record *why* a process was marked malicious based on readable file indicators.
