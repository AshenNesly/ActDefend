using System.Data;
using ActDefend.Core.Configuration;
using ActDefend.Core.Interfaces;
using ActDefend.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActDefend.Storage;

internal sealed class AlertRepository : IAlertRepository
{
    private readonly ILogger<AlertRepository> _logger;
    private readonly StorageOptions _options;
    private readonly string _connectionString;

    // Use a strict locking mechanism to avoid concurrent SQLite thread locks.
    // In a massive enterprise tool we'd use WAL mode and concurrent pools,
    // but for an ultra-lightweight desktop tracker, a simple lock on a single connection prevents DB corruption flawlessly.
    private readonly Lock _dbLock = new();

    public AlertRepository(ILogger<AlertRepository> logger, IOptions<ActDefendOptions> options)
    {
        _logger = logger;
        _options = options.Value.Storage;
        _connectionString = $"Data Source={_options.DatabasePath}";
        
        InitializeDatabase();
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
                CREATE TABLE IF NOT EXISTS Alerts (
                    AlertId TEXT PRIMARY KEY,
                    Timestamp TEXT NOT NULL,
                    ProcessId INTEGER NOT NULL,
                    ProcessName TEXT NOT NULL,
                    ProcessPath TEXT,
                    Severity INTEGER NOT NULL,
                    AffectedFileCount INTEGER NOT NULL,
                    Summary TEXT NOT NULL,
                    IsAcknowledged INTEGER NOT NULL DEFAULT 0,
                    Stage1Score REAL NOT NULL,
                    Stage2Entropy REAL NOT NULL,
                    CorrelationId TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS IDX_Alerts_Timestamp ON Alerts(Timestamp DESC);
            ";
            command.ExecuteNonQuery();
        }
    }

    public async Task SaveAsync(DetectionAlert alert, CancellationToken cancellationToken = default)
    {
        // Safe lock to prevent table locking collisions
        lock (_dbLock)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Alerts (
                    AlertId, Timestamp, ProcessId, ProcessName, ProcessPath, 
                    Severity, AffectedFileCount, Summary, IsAcknowledged,
                    Stage1Score, Stage2Entropy, CorrelationId
                ) VALUES (
                    $AlertId, $Timestamp, $ProcessId, $ProcessName, $ProcessPath, 
                    $Severity, $AffectedFileCount, $Summary, $IsAcknowledged,
                    $Stage1Score, $Stage2Entropy, $CorrelationId
                )
                ON CONFLICT(AlertId) DO UPDATE SET 
                    IsAcknowledged = excluded.IsAcknowledged;
            ";

            command.Parameters.AddWithValue("$AlertId", alert.AlertId.ToString());
            command.Parameters.AddWithValue("$Timestamp", alert.Timestamp.ToString("O")); // ISO8601
            command.Parameters.AddWithValue("$ProcessId", alert.ProcessId);
            command.Parameters.AddWithValue("$ProcessName", alert.ProcessName);
            command.Parameters.AddWithValue("$ProcessPath", alert.ProcessPath ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$Severity", (int)alert.Severity);
            command.Parameters.AddWithValue("$AffectedFileCount", alert.AffectedFileCount);
            command.Parameters.AddWithValue("$Summary", alert.Summary);
            command.Parameters.AddWithValue("$IsAcknowledged", alert.IsAcknowledged ? 1 : 0);
            command.Parameters.AddWithValue("$Stage1Score", alert.Stage1Result.Score);
            command.Parameters.AddWithValue("$Stage2Entropy", alert.Stage2Result.AverageEntropy);
            command.Parameters.AddWithValue("$CorrelationId", alert.CorrelationId.ToString());

            command.ExecuteNonQuery();
        }

        _logger.LogInformation("Persisted DetectionAlert {AlertId} to SQLite", alert.AlertId);
        await Task.CompletedTask;
    }

    public async Task<IReadOnlyList<DetectionAlert>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await GetAlertsInternalAsync(null, cancellationToken);
    }

    public async Task<IReadOnlyList<DetectionAlert>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        return await GetAlertsInternalAsync(count, cancellationToken);
    }

    public Task AcknowledgeAsync(Guid alertId, CancellationToken cancellationToken = default)
    {
        lock (_dbLock)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE Alerts SET IsAcknowledged = 1 WHERE AlertId = $AlertId";
            command.Parameters.AddWithValue("$AlertId", alertId.ToString());
            command.ExecuteNonQuery();
        }
        return Task.CompletedTask;
    }

    private Task<IReadOnlyList<DetectionAlert>> GetAlertsInternalAsync(int? limit, CancellationToken token)
    {
        var alerts = new List<DetectionAlert>();

        lock (_dbLock)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Alerts ORDER BY Timestamp DESC";
            
            if (limit.HasValue)
            {
                command.CommandText += " LIMIT $Limit";
                command.Parameters.AddWithValue("$Limit", limit.Value);
            }

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                // Rehydrate the core attributes used by the UI (full payload tracing remains complex so we rebuild the summary views).
                // In a massive app we'd map full json serialization per column.
                // For this lightweight desktop, we reconstruct the essential Data grid components natively.
                alerts.Add(new DetectionAlert
                {
                    AlertId = Guid.Parse(reader.GetString(reader.GetOrdinal("AlertId"))),
                    Timestamp = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("Timestamp"))),
                    ProcessId = reader.GetInt32(reader.GetOrdinal("ProcessId")),
                    ProcessName = reader.GetString(reader.GetOrdinal("ProcessName")),
                    ProcessPath = reader.IsDBNull(reader.GetOrdinal("ProcessPath")) ? null : reader.GetString(reader.GetOrdinal("ProcessPath")),
                    Severity = (AlertSeverity)reader.GetInt32(reader.GetOrdinal("Severity")),
                    AffectedFileCount = reader.GetInt32(reader.GetOrdinal("AffectedFileCount")),
                    Summary = reader.GetString(reader.GetOrdinal("Summary")),
                    IsAcknowledged = reader.GetInt32(reader.GetOrdinal("IsAcknowledged")) != 0,
                    CorrelationId = Guid.Parse(reader.GetString(reader.GetOrdinal("CorrelationId"))),
                    
                    // The UI primarily reads the Stage properties inside the Alert directly so we mock a subset 
                    // since we aren't joining heavy JSON strings in this phase.
                    Stage1Result = new ScoringResult 
                    { 
                        Timestamp = DateTimeOffset.UtcNow, 
                        Score = reader.GetDouble(reader.GetOrdinal("Stage1Score")), 
                        IsSuspicious = true,
                        Snapshot = new FeatureSnapshot { Timestamp = DateTimeOffset.UtcNow, ProcessId = 0, ProcessName = "" }
                    },
                    Stage2Result = new EntropyResult 
                    { 
                        Timestamp = DateTimeOffset.UtcNow, 
                        AverageEntropy = reader.GetDouble(reader.GetOrdinal("Stage2Entropy")),
                        ProcessId = 0, ProcessName = "", IsConfirmed = true, TriggerResult = null!
                    }
                });
            }
        }
        return Task.FromResult<IReadOnlyList<DetectionAlert>>(alerts);
    }
}
