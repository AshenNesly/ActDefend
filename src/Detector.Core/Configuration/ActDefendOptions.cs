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

    public LoggingOptions  Logging         { get; init; } = new();
    public StorageOptions  Storage         { get; init; } = new();
    public CollectorOptions Collector      { get; init; } = new();
    public FeaturesOptions Features        { get; init; } = new();
    public Stage1Options   Stage1          { get; init; } = new();
    public Stage2Options   Stage2          { get; init; } = new();
    public TrustedProcessOptions TrustedProcesses { get; init; } = new();
    public SimulatorOptions Simulator      { get; init; } = new();
}

// ── Logging ──────────────────────────────────────────────────────────────────

public sealed class LoggingOptions
{
    public string LogDirectory           { get; init; } = "logs";
    public string RollingInterval        { get; init; } = "Day";
    public int    RetainedFileCountLimit { get; init; } = 30;
    public string MinimumLevel           { get; init; } = "Information";
}

// ── Storage ───────────────────────────────────────────────────────────────────

public sealed class StorageOptions
{
    public string DatabasePath { get; init; } = "actdefend.db";
}

// ── Collector ─────────────────────────────────────────────────────────────────

public sealed class CollectorOptions
{
    /// <summary>Bounded channel capacity between ETW callback and downstream pipeline.</summary>
    public int EventQueueCapacity  { get; init; } = 4096;
    /// <summary>Milliseconds to wait for queue space before dropping an event.</summary>
    public int EventQueueTimeoutMs { get; init; } = 5;
}

// ── Features ──────────────────────────────────────────────────────────────────

public sealed class FeaturesOptions
{
    /// <summary>Short burst detection window in seconds.</summary>
    public int PrimaryWindowSeconds    { get; init; } = 5;
    /// <summary>Wider stabilisation window in seconds.</summary>
    public int ContextWindowSeconds    { get; init; } = 15;
    /// <summary>How often the extractor emits snapshots (seconds).</summary>
    public int EmitIntervalSeconds     { get; init; } = 2;
    /// <summary>Expire process state after this many seconds of inactivity.</summary>
    public int InactivityExpirySeconds { get; init; } = 120;
}

// ── Stage 1 ───────────────────────────────────────────────────────────────────

public sealed class Stage1Options
{
    public double         SuspicionThreshold { get; init; } = 60.0;
    public Stage1Weights  Weights            { get; init; } = new();
    public Stage1Thresholds Thresholds       { get; init; } = new();
}

public sealed class Stage1Weights
{
    public double WriteRate          { get; init; } = 20.0;
    public double UniqueFilesWritten { get; init; } = 20.0;
    public double RenameRate         { get; init; } = 25.0;
    public double DirectorySpread    { get; init; } = 20.0;
    public double WriteReadRatio     { get; init; } = 15.0;
}

public sealed class Stage1Thresholds
{
    public double WriteRatePerSec             { get; init; } = 10.0;
    public int    UniqueFilesPerWindow        { get; init; } = 30;
    public double RenameRatePerSec            { get; init; } = 5.0;
    public int    UniqueDirectoriesPerWindow  { get; init; } = 10;
    public double WriteReadRatioMax           { get; init; } = 5.0;
}

// ── Stage 2 ───────────────────────────────────────────────────────────────────

public sealed class Stage2Options
{
    public double EntropyThreshold     { get; init; } = 7.2;
    public int    SampleBytesLimit     { get; init; } = 65536;
    public int    MaxFilesToSample     { get; init; } = 5;
    public int    ConfirmationMinFiles { get; init; } = 2;
    public int    CooldownSeconds      { get; init; } = 10;
}

// ── Trusted Processes ─────────────────────────────────────────────────────────

public sealed class TrustedProcessOptions
{
    public IReadOnlyList<string> DefaultExclusions { get; init; } = [];
}

// ── Simulator ─────────────────────────────────────────────────────────────────

public sealed class SimulatorOptions
{
    public string WorkspaceDirectory { get; init; } = "";
    public int    FileCount          { get; init; } = 100;
    public int    RenameIntervalMs   { get; init; } = 50;
}
