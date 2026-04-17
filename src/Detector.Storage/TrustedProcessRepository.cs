using ActDefend.Core.Configuration;
using ActDefend.Core.Interfaces;
using ActDefend.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActDefend.Storage;

/// <summary>
/// Placeholder trusted-process repository for Phase 1.
/// Loads default exclusions from configuration into memory and keeps them there.
/// Phase 7 persists additions/removals to SQLite and hot-reloads the in-memory cache.
/// </summary>
internal sealed class TrustedProcessRepository : ITrustedProcessRepository
{
    private readonly ILogger<TrustedProcessRepository> _logger;
    private readonly List<TrustedProcessEntry> _entries = [];
    private readonly Lock _lock = new();

    public TrustedProcessRepository(
        ILogger<TrustedProcessRepository> logger,
        IOptions<ActDefendOptions> options)
    {
        _logger = logger;
        LoadDefaults(options.Value.TrustedProcesses.DefaultExclusions);
    }

    private void LoadDefaults(IReadOnlyList<string> defaults)
    {
        foreach (var name in defaults)
        {
            _entries.Add(new TrustedProcessEntry
            {
                EntryId     = Guid.NewGuid(),
                ProcessName = name,
                CreatedAt   = DateTimeOffset.UtcNow,
                Reason      = "Default system exclusion from configuration.",
                IsDefault   = true
            });
        }
        _logger.LogInformation("Loaded {Count} default trusted-process exclusions.", _entries.Count);
    }

    public Task<IReadOnlyList<TrustedProcessEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            IReadOnlyList<TrustedProcessEntry> r = [.. _entries];
            return Task.FromResult(r);
        }
    }

    public Task AddAsync(TrustedProcessEntry entry, CancellationToken cancellationToken = default)
    {
        lock (_lock) { _entries.Add(entry); }
        _logger.LogInformation("Trusted process added: {Name} (ID={Id}) Reason={Reason}",
            entry.ProcessName, entry.EntryId, entry.Reason);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        lock (_lock) { _entries.RemoveAll(e => e.EntryId == entryId); }
        _logger.LogInformation("Trusted process removed: ID={Id}", entryId);
        return Task.CompletedTask;
    }

    public bool IsTrusted(int processId, string processName, string? processPath)
    {
        lock (_lock)
        {
            return _entries.Any(e =>
                (e.ProcessName is null ||
                 string.Equals(e.ProcessName, processName, StringComparison.OrdinalIgnoreCase)) &&
                (e.ProcessPath is null ||
                 string.Equals(e.ProcessPath, processPath, StringComparison.OrdinalIgnoreCase)));
        }
    }
}
