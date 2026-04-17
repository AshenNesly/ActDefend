namespace ActDefend.Core.Models;

/// <summary>
/// An entry in the trusted-process allow-list.
/// Processes matching a trust rule are excluded from Stage 1 scoring.
/// Rules are intentionally narrow — prefer specific matches over broad wildcards.
/// </summary>
public sealed record TrustedProcessEntry
{
    /// <summary>Unique identifier for this entry.</summary>
    public required Guid EntryId { get; init; }

    /// <summary>
    /// Short image name match (e.g. "notepad.exe").
    /// Case-insensitive comparison. Null means do not match on name alone.
    /// </summary>
    public string? ProcessName { get; init; }

    /// <summary>
    /// Full executable path match.
    /// If set, both name AND path must match for a process to be trusted.
    /// </summary>
    public string? ProcessPath { get; init; }

    /// <summary>UTC time this entry was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Human-readable reason this process was trusted.</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>True if this entry was added automatically from the default exclusion list.</summary>
    public bool IsDefault { get; init; }
}
