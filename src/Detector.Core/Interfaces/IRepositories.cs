using ActDefend.Core.Models;

namespace ActDefend.Core.Interfaces;

/// <summary>
/// Contract for the detection alert repository (SQLite persistence layer).
/// Stores confirmed detection alerts for GUI display and offline analysis.
/// </summary>
public interface IAlertRepository
{
    /// <summary>Persists a new detection alert. Idempotent on AlertId.</summary>
    Task SaveAsync(DetectionAlert alert, CancellationToken cancellationToken = default);

    /// <summary>Returns all alerts, newest first.</summary>
    Task<IReadOnlyList<DetectionAlert>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the N most recent alerts.</summary>
    Task<IReadOnlyList<DetectionAlert>> GetRecentAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>Marks an alert as acknowledged.</summary>
    Task AcknowledgeAsync(Guid alertId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Contract for the trusted-process allow-list repository.
/// </summary>
public interface ITrustedProcessRepository
{
    /// <summary>Returns all current trusted-process entries.</summary>
    Task<IReadOnlyList<TrustedProcessEntry>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Adds a new trusted-process rule. Logs the addition.</summary>
    Task AddAsync(TrustedProcessEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Removes a trusted-process rule by ID. Logs the removal.</summary>
    Task RemoveAsync(Guid entryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if the given process matches any trust rule.
    /// Used in the hot scoring path — must be fast (in-memory cache expected).
    /// </summary>
    bool IsTrusted(int processId, string processName, string? processPath);
}
