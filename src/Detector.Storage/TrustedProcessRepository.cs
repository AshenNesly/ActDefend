using System.Data;
using ActDefend.Core.Configuration;
using ActDefend.Core.Interfaces;
using ActDefend.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActDefend.Storage;

/// <summary>
/// Persists trusted-process additions/removals to SQLite and hot-reloads the in-memory cache,
/// along with default exclusions from configuration.
/// </summary>
internal sealed class TrustedProcessRepository : ITrustedProcessRepository
{
    private readonly ILogger<TrustedProcessRepository> _logger;
    private readonly string _connectionString;
    private readonly List<TrustedProcessEntry> _entries = [];
    private readonly Lock _lock = new();
    private readonly Lock _dbLock = new();

    public TrustedProcessRepository(
        ILogger<TrustedProcessRepository> logger,
        IOptions<ActDefendOptions> options)
    {
        _logger = logger;
        _connectionString = $"Data Source={options.Value.Storage.DatabasePath}";
        
        InitializeDatabase();
        LoadDefaults(options.Value.TrustedProcesses.DefaultExclusions);
        LoadFromDatabase();
    }

    private void InitializeDatabase()
    {
        lock (_dbLock)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // Enable WAL for better concurrency and speed with minimal locking
            using (var walCommand = connection.CreateCommand())
            {
                walCommand.CommandText = "PRAGMA journal_mode = 'wal';";
                walCommand.ExecuteNonQuery();
            }

            using var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS TrustedProcesses (
                    EntryId TEXT PRIMARY KEY,
                    ProcessName TEXT,
                    ProcessPath TEXT,
                    CreatedAt TEXT NOT NULL,
                    Reason TEXT NOT NULL,
                    Source TEXT NOT NULL
                );
            ";
            command.ExecuteNonQuery();
        }
    }

    private void LoadDefaults(IReadOnlyList<string> defaults)
    {
        lock (_lock)
        {
            foreach (var name in defaults)
            {
                if (!_entries.Any(e => string.Equals(e.ProcessName, name, StringComparison.OrdinalIgnoreCase) && e.IsDefault))
                {
                    _entries.Add(new TrustedProcessEntry
                    {
                        EntryId     = Guid.NewGuid(),
                        ProcessName = name,
                        CreatedAt   = DateTimeOffset.UtcNow,
                        Reason      = "Default system exclusion from configuration.",
                        IsDefault   = true,
                        Source      = "DefaultConfig"
                    });
                }
            }
        }
        _logger.LogInformation("Loaded {Count} default trusted-process exclusions.", defaults.Count);
    }

    private void LoadFromDatabase()
    {
        lock (_dbLock)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM TrustedProcesses";
            
            using var reader = command.ExecuteReader();
            int count = 0;
            
            lock (_lock)
            {
                while (reader.Read())
                {
                    var entry = new TrustedProcessEntry
                    {
                        EntryId = Guid.Parse(reader.GetString(reader.GetOrdinal("EntryId"))),
                        ProcessName = reader.IsDBNull(reader.GetOrdinal("ProcessName")) ? null : reader.GetString(reader.GetOrdinal("ProcessName")),
                        ProcessPath = reader.IsDBNull(reader.GetOrdinal("ProcessPath")) ? null : reader.GetString(reader.GetOrdinal("ProcessPath")),
                        CreatedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("CreatedAt"))),
                        Reason = reader.GetString(reader.GetOrdinal("Reason")),
                        Source = reader.GetString(reader.GetOrdinal("Source")),
                        IsDefault = false
                    };
                    
                    if (!_entries.Any(e => string.Equals(e.ProcessName, entry.ProcessName, StringComparison.OrdinalIgnoreCase) && 
                                           string.Equals(e.ProcessPath, entry.ProcessPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        _entries.Add(entry);
                        count++;
                    }
                }
            }
            _logger.LogInformation("Loaded {Count} persisted trusted-process exclusions from SQLite.", count);
        }
    }

    public Task<IReadOnlyList<TrustedProcessEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            IReadOnlyList<TrustedProcessEntry> r = [.. _entries.OrderByDescending(e => e.CreatedAt)];
            return Task.FromResult(r);
        }
    }

    public Task AddAsync(TrustedProcessEntry entry, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_entries.Any(e => string.Equals(e.ProcessName, entry.ProcessName, StringComparison.OrdinalIgnoreCase) && 
                                  string.Equals(e.ProcessPath, entry.ProcessPath, StringComparison.OrdinalIgnoreCase)))
            {
                return Task.CompletedTask;
            }
            _entries.Add(entry);
        }

        lock (_dbLock)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO TrustedProcesses (EntryId, ProcessName, ProcessPath, CreatedAt, Reason, Source) 
                VALUES ($EntryId, $ProcessName, $ProcessPath, $CreatedAt, $Reason, $Source)
                ON CONFLICT(EntryId) DO NOTHING;
            ";
            command.Parameters.AddWithValue("$EntryId", entry.EntryId.ToString());
            command.Parameters.AddWithValue("$ProcessName", entry.ProcessName ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$ProcessPath", entry.ProcessPath ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$CreatedAt", entry.CreatedAt.ToString("O"));
            command.Parameters.AddWithValue("$Reason", entry.Reason);
            command.Parameters.AddWithValue("$Source", entry.Source);
            command.ExecuteNonQuery();
        }

        _logger.LogInformation("Trusted process added: {Name} (ID={Id}) Reason={Reason} Source={Source}",
            entry.ProcessName, entry.EntryId, entry.Reason, entry.Source);
            
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        lock (_lock) 
        { 
            _entries.RemoveAll(e => e.EntryId == entryId && !e.IsDefault); 
        }

        lock (_dbLock)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM TrustedProcesses WHERE EntryId = $EntryId";
            command.Parameters.AddWithValue("$EntryId", entryId.ToString());
            command.ExecuteNonQuery();
        }

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
