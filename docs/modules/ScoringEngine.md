# Lightweight Scoring Engine — Stage 1 (`Detector.Detection`)

The `LightweightScoringEngine` performs the first-pass ransomware detection. It converts a `FeatureSnapshot` (emitted by the Feature Extractor) into a numeric suspicion score and a human-readable explanation. No file I/O occurs here; this stage is pure arithmetic.

## Classes

| Class | Role |
|---|---|
| `LightweightScoringEngine` | Implements `IScoringEngine.Score(FeatureSnapshot)` |
| `DetectionOrchestrator` | Calls the scorer on every tick; routes suspicious results to Stage 2 |

---

## Scoring Algorithm

The score is a **weighted, normalised linear sum** over six features, capped at 100.

For each feature:
```
contribution = min(actual_value / threshold, 1.0) × weight
```

Total score:
```
score = Σ contributions  (capped at 100.0)
```

A process is flagged as suspicious when `score ≥ SuspicionThreshold` (default `60.0`).

---

## Features, Weights, and Thresholds (Default Values)

| Feature | Config key | Default weight | Default threshold | Full-weight condition |
|---|---|---|---|---|
| `WriteRate` | `Weights.WriteRate` / `Thresholds.WriteRatePerSec` | **10 pts** | 10 writes/s | ≥ 10 writes/s in primary window |
| `UniqueFilesWritten` | `Weights.UniqueFilesWritten` / `Thresholds.UniqueFilesPerWindow` | **15 pts** | 30 unique files | ≥ 30 distinct files written |
| `RenameRate` | `Weights.RenameRate` / `Thresholds.RenameRatePerSec` | **20 pts** | 5 renames/s | ≥ 5 renames/s in primary window |
| `DirectorySpread` | `Weights.DirectorySpread` / `Thresholds.UniqueDirectoriesPerWindow` | **20 pts** | 10 unique dirs | ≥ 10 distinct directories touched |
| `WriteReadRatio` | `Weights.WriteReadRatio` / `Thresholds.WriteReadRatioMax` | **10 pts** | ratio of 5.0 | writes ÷ reads ≥ 5 |
| `PreExistingModifyRate` | `Weights.PreExistingModifyRate` / `Thresholds.PreExistingModifyRatePerSec` | **25 pts** | 5 modifies/s | ≥ 5 pre-existing file modifies/s |

**Total maximum: 100 pts.**

> **Important:** `PreExistingModifyRate` carries the highest single weight (25 pts). This is the primary feature separating ransomware (which modifies pre-existing user files) from benign installers (which create only new files and score 0 on this axis).

> **Important:** `WriteReadRatio` is `0.0` when `primaryReads == 0` (pure write-only process). This prevents pure downloaders and network-stream installers from receiving the maximum 10-point penalty just for writing without reading.

---

## Explainability

When `score > 0`, the engine calls `BuildExplanation()`, which:
1. Identifies the top-3 contributing features by points.
2. Formats a human-readable string: `"Score: 77.3 (Exceeds Threshold). Top contributors: RenameRate (20.0pts), DirectorySpread (18.5pts), PreExistingModifyRate (15.0pts)"`.

This explanation is stored in `ScoringResult.Explanation` and appears in:
- The structured JSON log at `Information` level.
- The `DetectionAlert.Summary` field displayed in the GUI alert feed.

---

## Severity Mapping

When both Stage 1 and Stage 2 confirm a detection, `DetectionOrchestrator.BuildAlert()` maps the Stage 1 score to a severity level:

| Score | Severity |
|---|---|
| ≥ 90 | `Critical` |
| ≥ 75 | `High` |
| ≥ 60 | `Medium` |
| < 60 | `Low` (this case is unreachable at the threshold default of 60) |

---

## Configuration Reference

All weights and thresholds are defined in `appsettings.json` under `ActDefend:Stage1`. See [configuration.md](../configuration.md) for the full list with allowed ranges.

---

## Design Rationale

- **Linear, not ML:** Every score contribution is directly traceable to a measurable file-system metric. There are no black-box weights.
- **Multi-axis confirmation:** A process that is extreme on only one axis (e.g. a single-directory file copy) will not breach the threshold. Multiple axes must fire simultaneously.
- **Configurable without recompile:** Tightening or loosening sensitivity requires only editing `appsettings.json` and restarting.

---

## Testing

Unit tests in `tests/Detector.UnitTests/Detection/ScoringEngineTests.cs` cover:
- Zero-feature snapshot produces score 0.
- Each feature independently contributes correctly.
- Score is capped at 100 when all thresholds exceeded.
- `IsSuspicious` is `false` below threshold, `true` at and above.
- `WriteReadRatio = 0.0` when reads are 0 (false-positive reduction test).
