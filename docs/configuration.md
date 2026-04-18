# Configuration Guide

ActDefend operates heavily on flexible configuration intervals to support wide-scaling Evaluation metrics cleanly without source recompilation.

All configurations are strictly checked using `.ValidateDataAnnotations().ValidateOnStart()` upon launching natively. Breaking an internal configuration bound prevents `Detector.App` from initiating ETW hooks safely blocking false evaluations gracefully.

## `appsettings.json` Target Block

```json
{
  "ActDefend": {
    "Logging": {
      "LogDirectory": "logs",
      "RollingInterval": "Day",
      "RetainedFileCountLimit": 30,
      "MinimumLevel": "Information"
    },
    "Storage": {
      "DatabasePath": "actdefend.db"
    },
    ...
  }
}
```

## Option Breakdowns

### Collector Options
- **`EventQueueCapacity`**: `[Range(1024, 1000000)]` - Total items held before internal events drop safely mapping backpressure.
- **`EventQueueTimeoutMs`**: `[Range(1, 1000)]` - Maximum hook wait duration holding Kernel metrics internally before yielding gracefully.

### Feature Options
- **`PrimaryWindowSeconds`**: `[Range(1, 60)]` - Short burst metrics validating stage 1 scores cleanly.
- **`ContextWindowSeconds`**: `[Range(5, 300)]` - Long stabilization tracks mapping process histories perfectly.
- **`EmitIntervalSeconds`**: `[Range(1, 60)]` - Interval frequency dumping sliding window outputs synchronously triggering Stage 1 rules natively across decoupled processes.
- **`InactivityExpirySeconds`**: `[Range(10, 3600)]` - Garbage collection rules natively expiring unused states cleanly limiting cache sizes.

### Stage 1 (Scoring) Options
- **`SuspicionThreshold`**: `[Range(1.0, 1000.0)]` - Flat baseline triggering Stage 2 natively. Default: `60.0`.
- **`Weights` & `Thresholds`**: Mapped logic dictating explicitly mathematically validating `WriteRate`, `UniqueFilesWritten`, `RenameRate`, `DirectorySpread`, `WriteReadRatio`, and `PreExistingModifyRate`. All bounds use `[Range(0.0, 100.0)]` multipliers structurally.

### Stage 2 (Entropy) Options
- **`EntropyThreshold`**: `[Range(0.0, 8.0)]` - Flat bounds determining encryption hooks cleanly. Default `7.2`.
- **`SampleBytesLimit`**: `[Range(1024, 1048576)]` - Block chunks safely extracted cleanly.
- **`MaxFilesToSample`**: `[Range(1, 100)]` - Max files analyzed internally per suspicion track ensuring CPU constraints are enforced structurally.
