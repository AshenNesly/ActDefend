using System;

namespace ActDefend.Evaluation;

public enum WorkloadType
{
    Ransomware,
    Benign
}

public class ScenarioDefinition
{
    public string Name { get; set; } = string.Empty;
    public WorkloadType WorkloadType { get; set; }
    public int FileCount { get; set; }
    public int DelayMs { get; set; }
    public int DirectoryDepth { get; set; }
    public bool ExpectedAlert { get; set; }
}

public class EvaluationResult
{
    // ── Scenario identity ────────────────────────────────────────────────────
    public string ScenarioName { get; set; } = string.Empty;
    public WorkloadType WorkloadType { get; set; }
    public int FileCount { get; set; }
    public int DelayMs { get; set; }
    public int DirectoryDepth { get; set; }

    // ── Timing ───────────────────────────────────────────────────────────────
    public DateTimeOffset StartTimeUtc { get; set; }
    public DateTimeOffset EndTimeUtc { get; set; }

    // ── Detection outcome ────────────────────────────────────────────────────
    public bool AlertRaised { get; set; }
    public int AlertCount { get; set; }
    public DateTimeOffset? FirstAlertTimeUtc { get; set; }
    public double? DetectionLatencyMs { get; set; }
    public double? InternalDetectorLatencyMs { get; set; }

    // ── Pass / Fail ──────────────────────────────────────────────────────────
    public bool Pass { get; set; }
    public string FailureReason { get; set; } = string.Empty;

    // ── Performance ──────────────────────────────────────────────────────────
    public double AverageCpuUsagePercent { get; set; }
    public double PeakCpuUsagePercent { get; set; }
    public double AverageMemoryMb { get; set; }
    public double PeakMemoryMb { get; set; }

    // ── Pipeline counters ────────────────────────────────────────────────────
    public long EventsProcessed { get; set; }
    public long EventsDropped { get; set; }
    public long EventsProcessedDelta { get; set; }
    public long EventsDroppedDelta { get; set; }

    // ── Alert evidence (from first alert) ────────────────────────────────────
    public double? SuspicionScore { get; set; }
    public string? Stage1TopReasons { get; set; }
    public int? HighEntropyFileCount { get; set; }
    public string? EntropyValues { get; set; }

    // ── Diagnostics (printed to console, included in JSON) ───────────────────
    public bool DiagCollectorStarted { get; set; }
    public long DiagEventsProcessedBefore { get; set; }
    public long DiagEventsProcessedAfter { get; set; }
    public long DiagEventsDroppedBefore { get; set; }
    public long DiagEventsDroppedAfter { get; set; }
    public string DiagDatabasePathUsedByPipeline { get; set; } = string.Empty;
    public string DiagDatabasePathReadByEvaluator { get; set; } = string.Empty;
    public int DiagSimulatorExitCode { get; set; }
    public DateTimeOffset DiagSimulatorStartUtc { get; set; }
    public DateTimeOffset DiagSimulatorEndUtc { get; set; }
    public DateTimeOffset DiagAlertQueryStartUtc { get; set; }
    public DateTimeOffset DiagAlertQueryEndUtc { get; set; }
    public int DiagRawAlertCount { get; set; }
    public int DiagFilteredAlertCount { get; set; }
}
