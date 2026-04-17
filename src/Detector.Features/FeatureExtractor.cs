using System.Collections.Concurrent;
using ActDefend.Core.Configuration;
using ActDefend.Core.Interfaces;
using ActDefend.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActDefend.Features;

/// <summary>
/// Real Feature Extractor tracking bounded transient state per-process.
/// Computes Sliding Time Window burst ratios mapping direct ETW File modifications
/// against FeatureSnapshots used by Stage 1 Scoring.
/// </summary>
public sealed class FeatureExtractor : IFeatureExtractor
{
    private readonly ILogger<FeatureExtractor> _logger;
    private readonly FeaturesOptions _options;

    // Concurrency-safe mapping of PID to transient file activity
    private readonly ConcurrentDictionary<int, ProcessState> _processStates = new();

    public FeatureExtractor(ILogger<FeatureExtractor> logger, IOptions<ActDefendOptions> options)
    {
        _logger = logger;
        _options = options.Value.Features;
    }

    /// <inheritdoc />
    public int ActiveProcessCount => _processStates.Count;

    /// <inheritdoc />
    public void ProcessEvent(FileSystemEvent evt)
    {
        var contextWindow = TimeSpan.FromSeconds(_options.ContextWindowSeconds);

        var state = _processStates.GetOrAdd(evt.ProcessId, pid =>
            new ProcessState(pid, evt.ProcessName, evt.ProcessPath));

        state.AddEvent(evt, contextWindow);
    }

    /// <inheritdoc />
    public IReadOnlyList<FeatureSnapshot> Emit()
    {
        var now = DateTimeOffset.UtcNow;
        var contextWindow = TimeSpan.FromSeconds(_options.ContextWindowSeconds);
        var primaryWindow = TimeSpan.FromSeconds(_options.PrimaryWindowSeconds);
        var contextCutoff = now - contextWindow;
        var primaryCutoff = now - primaryWindow;

        var snapshots = new List<FeatureSnapshot>(_processStates.Count);

        foreach (var state in _processStates.Values)
        {
            // Prune out-of-context events and securely slice memory snapshot
            var activeEvents = state.GetEventsAndPrune(contextCutoff);

            if (activeEvents.Count == 0)
                continue;

            // 1. Context Window Aggregations (Full wide window)
            int totalWrites = 0;
            int totalRenames = 0;
            
            // 2. Primary Window Aggregations (Short recent burst window)
            int primaryWrites = 0;
            int primaryReads = 0;
            int primaryRenames = 0;
            var primaryUniqueFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var primaryUniqueDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            var recentWrittenFilesQueue = new List<string>(5);

            foreach (var evt in activeEvents)
            {
                // Accumulate wide context metrics
                if (evt.EventType == FileSystemEventType.Write)
                    totalWrites++;
                else if (evt.EventType == FileSystemEventType.Rename)
                    totalRenames++;

                // If event resides within closer Primary Window, compute burst traits
                if (evt.Timestamp >= primaryCutoff)
                {
                    if (evt.EventType == FileSystemEventType.Write)
                    {
                        primaryWrites++;
                        primaryUniqueFiles.Add(evt.FilePath);
                        AddUniqueDirSafely(evt.FilePath, primaryUniqueDirs);
                        
                        // Push to recent queue for sampling (Stage 2)
                        recentWrittenFilesQueue.Add(evt.FilePath);
                        if (recentWrittenFilesQueue.Count > 5)
                            recentWrittenFilesQueue.RemoveAt(0); // Very naive but functional queue maintaining last 5
                    }
                    else if (evt.EventType == FileSystemEventType.Read)
                    {
                        primaryReads++;
                        AddUniqueDirSafely(evt.FilePath, primaryUniqueDirs);
                    }
                    else if (evt.EventType == FileSystemEventType.Rename)
                    {
                        primaryRenames++;
                        primaryUniqueFiles.Add(evt.FilePath);
                        AddUniqueDirSafely(evt.FilePath, primaryUniqueDirs);
                    }
                }
            }

            // Skip emit if nothing functionally occurred in the recent short burst
            if (primaryWrites == 0 && primaryRenames == 0)
                continue;

            // Calculate burst velocity and ratios
            var durationSecs = _options.PrimaryWindowSeconds > 0 ? _options.PrimaryWindowSeconds : 1.0;
            var writeRate = primaryWrites / durationSecs;
            var renameRate = primaryRenames / durationSecs;

            double ratio = 0;
            if (primaryWrites > 0)
            {
                ratio = primaryReads > 0 
                    ? (double)primaryWrites / primaryReads 
                    : double.MaxValue;
            }

            snapshots.Add(new FeatureSnapshot
            {
                Timestamp = now,
                ProcessId = state.ProcessId,
                ProcessName = state.ProcessName,
                ProcessPath = state.ProcessPath,

                WriteRatePerSec = writeRate,
                UniqueFilesWritten = primaryUniqueFiles.Count,
                RenameRatePerSec = renameRate,
                UniqueDirectoriesTouched = primaryUniqueDirs.Count,
                WriteReadRatio = ratio,

                TotalWritesInContext = totalWrites,
                TotalRenamesInContext = totalRenames,

                PrimaryWindowDuration = primaryWindow,
                ContextWindowDuration = contextWindow,
                RecentWrittenFiles = recentWrittenFilesQueue
            });
        }

        return snapshots;
    }

    /// <inheritdoc />
    public void ExpireInactiveState()
    {
        var expiryThreshold = DateTimeOffset.UtcNow - TimeSpan.FromSeconds(_options.InactivityExpirySeconds);
        var expiredPids = new List<int>();

        foreach (var kvp in _processStates)
        {
            if (kvp.Value.LastEventUtc < expiryThreshold)
            {
                expiredPids.Add(kvp.Key);
            }
        }

        foreach (var pid in expiredPids)
        {
            if (_processStates.TryRemove(pid, out var removed))
            {
                _logger.LogTrace("Expired inactive Process state tracking for PID = {Pid} ({Name})", 
                    removed.ProcessId, removed.ProcessName);
            }
        }
    }

    private static void AddUniqueDirSafely(string filePath, HashSet<string> hashSet)
    {
        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                hashSet.Add(dir);
            }
        }
        catch
        {
            // Path structure was invalid, ignore Directory derivation.
        }
    }
}
