using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace ActDefend.Core.Configuration;

/// <summary>
/// Root configuration object — bound from the "ActDefend" section of appsettings.json.
/// All tunable parameters in the system flow through these nested classes.
/// No magic numbers should exist in source code; they belong here.
/// </summary>
public sealed class ActDefendOptions
{
    /// <summary>Configuration section key used in appsettings.json.</summary>
    public const string SectionName = "ActDefend";

    [Required] public LoggingOptions  Logging  { get; set; } = new();
    [Required] public StorageOptions  Storage  { get; set; } = new();
    [Required] public CollectorOptions Collector { get; set; } = new();
    [Required] public FeaturesOptions Features   { get; set; } = new();
    [Required] public Stage1Options   Stage1     { get; set; } = new();
    [Required] public Stage2Options   Stage2     { get; set; } = new();
    [Required] public TrustedProcessOptions TrustedProcesses { get; set; } = new();
    [Required] public SimulatorOptions Simulator { get; set; } = new();
}

// ── Logging ──────────────────────────────────────────────────────────────────

public sealed class LoggingOptions
{
    [Required] [MinLength(1)] public string LogDirectory           { get; set; } = "logs";
    [Required] [MinLength(1)] public string RollingInterval        { get; set; } = "Day";
    [Range(1, 365)]           public int    RetainedFileCountLimit { get; set; } = 30;
    [Required] [MinLength(1)] public string MinimumLevel           { get; set; } = "Information";
}

// ── Storage ───────────────────────────────────────────────────────────────────

public sealed class StorageOptions
{
    [Required] [MinLength(1)] public string DatabasePath { get; set; } = "actdefend.db";
}

// ── Collector ─────────────────────────────────────────────────────────────────

public sealed class CollectorOptions
{
    /// <summary>Bounded channel capacity between ETW callback and downstream pipeline.</summary>
    [Range(1024, 1000000)] public int EventQueueCapacity  { get; set; } = 4096;
    /// <summary>Milliseconds to wait for queue space before dropping an event.</summary>
    [Range(1, 1000)]       public int EventQueueTimeoutMs { get; set; } = 5;
}

// ── Features ──────────────────────────────────────────────────────────────────

public sealed class FeaturesOptions
{
    /// <summary>Short burst detection window in seconds.</summary>
    [Range(1, 60)]   public int PrimaryWindowSeconds    { get; set; } = 5;
    /// <summary>Wider stabilisation window in seconds.</summary>
    [Range(5, 300)]  public int ContextWindowSeconds    { get; set; } = 15;
    /// <summary>How often the extractor emits snapshots (seconds).</summary>
    [Range(1, 60)]   public int EmitIntervalSeconds     { get; set; } = 2;
    /// <summary>Expire process state after this many seconds of inactivity.</summary>
    [Range(10, 3600)] public int InactivityExpirySeconds { get; set; } = 120;
}

// ── Stage 1 ───────────────────────────────────────────────────────────────────

public sealed class Stage1Options
{
    [Range(1.0, 1000.0)] public double         SuspicionThreshold { get; set; } = 60.0;
    [Required]           public Stage1Weights  Weights            { get; set; } = new();
    [Required]           public Stage1Thresholds Thresholds       { get; set; } = new();
}

public sealed class Stage1Weights
{
    [Range(0.0, 100.0)] public double WriteRate             { get; set; } = 10.0;
    [Range(0.0, 100.0)] public double UniqueFilesWritten    { get; set; } = 15.0;
    [Range(0.0, 100.0)] public double RenameRate            { get; set; } = 20.0;
    [Range(0.0, 100.0)] public double DirectorySpread       { get; set; } = 20.0;
    [Range(0.0, 100.0)] public double WriteReadRatio        { get; set; } = 10.0;
    [Range(0.0, 100.0)] public double PreExistingModifyRate { get; set; } = 25.0;
}

public sealed class Stage1Thresholds
{
    [Range(0.1, 1000.0)] public double WriteRatePerSec             { get; set; } = 10.0;
    [Range(1, 10000)]    public int    UniqueFilesPerWindow        { get; set; } = 30;
    [Range(0.1, 1000.0)] public double RenameRatePerSec            { get; set; } = 5.0;
    [Range(1, 1000)]     public int    UniqueDirectoriesPerWindow  { get; set; } = 10;
    [Range(0.1, 100.0)]  public double WriteReadRatioMax           { get; set; } = 5.0;
    [Range(0.1, 1000.0)] public double PreExistingModifyRatePerSec { get; set; } = 5.0;
}

// ── Stage 2 ───────────────────────────────────────────────────────────────────

public sealed class Stage2Options
{
    [Range(0.0, 8.0)]    public double EntropyThreshold     { get; set; } = 7.2;
    [Range(1024, 1048576)] public int    SampleBytesLimit     { get; set; } = 65536;
    [Range(1, 100)]      public int    MaxFilesToSample     { get; set; } = 5;
    [Range(1, 50)]       public int    ConfirmationMinFiles { get; set; } = 2;
    [Range(1, 3600)]     public int    CooldownSeconds      { get; set; } = 10;
}

// ── Trusted Processes ─────────────────────────────────────────────────────────

public sealed class TrustedProcessOptions
{
    [Required] public IReadOnlyList<string> DefaultExclusions { get; set; } = [];
}

// ── Simulator ─────────────────────────────────────────────────────────────────

public sealed class SimulatorOptions
{
    public string WorkspaceDirectory { get; set; } = "";
    [Range(1, 100000)] public int    FileCount          { get; set; } = 100;
    [Range(0, 10000)]  public int    RenameIntervalMs   { get; set; } = 50;
}
