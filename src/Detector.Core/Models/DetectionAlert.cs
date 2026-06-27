namespace ActDefend.Core.Models;

/// <summary>
/// A confirmed detection alert emitted when both Stage 1 and Stage 2 agree
/// that a process is exhibiting ransomware-like behaviour.
/// Stored in the local database and displayed in the GUI/tray.
/// </summary>
public sealed record DetectionAlert
{
    /// <summary>Unique identifier for this alert (stable across restarts).</summary>
    public required Guid AlertId { get; init; }

    /// <summary>UTC time the alert was raised.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>OS process ID of the flagged process.</summary>
    public required int ProcessId { get; init; }

    /// <summary>Short image name (e.g. "encrypt_all.exe").</summary>
    public required string ProcessName { get; init; }

    /// <summary>Full executable path if resolved, or null.</summary>
    public string? ProcessPath { get; init; }

    /// <summary>Alert severity level.</summary>
    public required AlertSeverity Severity { get; init; }

    /// <summary>Stage 1 score snapshot that triggered Stage 2.</summary>
    public required ScoringResult Stage1Result { get; init; }

    /// <summary>Stage 2 entropy confirmation result.</summary>
    public required EntropyResult Stage2Result { get; init; }

    /// <summary>Total number of distinct files seen written by this process.</summary>
    public int AffectedFileCount { get; init; }

    /// <summary>Human-readable summary for display in GUI/tray and log output.</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>Unique group identifier correlating related events for this incident.</summary>
    public Guid CorrelationId { get; init; } = Guid.NewGuid();

    /// <summary>True if the user has acknowledged/dismissed this alert.</summary>
    public bool IsAcknowledged { get; set; }

    // ── Rich Evidence Fields ──────────────────────────────────────────────────

    /// <summary>Stage 1 suspicion score [0-100].</summary>
    public double SuspicionScore { get; init; }

    /// <summary>Comma-separated list of the top contributing Stage 1 reasons.</summary>
    public string Stage1TopReasons { get; init; } = string.Empty;

    /// <summary>The threshold value Stage 1 used for this detection.</summary>
    public double Stage1ThresholdUsed { get; init; }

    /// <summary>Timestamp when the process first crossed the Stage 1 threshold.</summary>
    public DateTimeOffset? FirstSuspiciousAtUtc { get; init; }

    /// <summary>Timestamp when Stage 2 confirmed the detection.</summary>
    public DateTimeOffset ConfirmedAtUtc { get; init; }

    /// <summary>Time elapsed from first suspicion to confirmation.</summary>
    public double DetectionLatencyMs { get; init; }

    /// <summary>Number of files that exceeded the entropy threshold.</summary>
    public int HighEntropyFileCount { get; init; }

    /// <summary>JSON serialized summary of sampled files and their entropy values.</summary>
    public string EntropyValuesJson { get; init; } = string.Empty;

    /// <summary>The entropy threshold Stage 2 used.</summary>
    public double Stage2EntropyThresholdUsed { get; init; }

    /// <summary>The minimum number of high-entropy files Stage 2 required for confirmation.</summary>
    public int Stage2MinFilesUsed { get; init; }
}

/// <summary>Severity levels for detection alerts.</summary>
public enum AlertSeverity
{
    /// <summary>Suspicious activity observed but not yet fully confirmed.</summary>
    Low,
    /// <summary>Stage 1 scoring is high; Stage 2 provides partial evidence.</summary>
    Medium,
    /// <summary>Both stages confirm high-confidence ransomware-like activity.</summary>
    High,
    /// <summary>Extremely high scores across all signals with full Stage 2 confirmation.</summary>
    Critical
}
