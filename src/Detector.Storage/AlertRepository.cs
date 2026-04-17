using ActDefend.Core.Interfaces;
using ActDefend.Core.Models;
using Microsoft.Extensions.Logging;

namespace ActDefend.Storage;

/// <summary>
/// Placeholder alert repository for Phase 1.
/// Phase 7 replaces this with a real SQLite-backed implementation using
/// Microsoft.Data.Sqlite with hand-written SQL (no heavy ORM).
/// Schema design and migration strategy are documented in docs/technical-design.md.
/// </summary>
internal sealed class AlertRepository : IAlertRepository
{
    private readonly ILogger<AlertRepository> _logger;

    // In-memory store for Phase 1. Thread-safe with lock.
    private readonly List<DetectionAlert> _store = [];
    private readonly Lock _lock = new();

    public AlertRepository(ILogger<AlertRepository> logger)
    {
        _logger = logger;
    }

    public Task SaveAsync(DetectionAlert alert, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _store.RemoveAll(a => a.AlertId == alert.AlertId); // upsert
            _store.Add(alert);
        }
        _logger.LogDebug("[PLACEHOLDER] Alert saved in-memory: {Id}", alert.AlertId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DetectionAlert>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            IReadOnlyList<DetectionAlert> result = [.. _store.OrderByDescending(a => a.Timestamp)];
            return Task.FromResult(result);
        }
    }

    public Task<IReadOnlyList<DetectionAlert>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            IReadOnlyList<DetectionAlert> result = [.. _store.OrderByDescending(a => a.Timestamp).Take(count)];
            return Task.FromResult(result);
        }
    }

    public Task AcknowledgeAsync(Guid alertId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var alert = _store.FirstOrDefault(a => a.AlertId == alertId);
            if (alert is not null) alert.IsAcknowledged = true;
        }
        return Task.CompletedTask;
    }
}
