using ActDefend.Core.Models;

namespace ActDefend.Features;

/// <summary>
/// Encapsulates thread-safe transient state for a single process.
/// Stores file system events up to the length of the Context Window.
/// </summary>
internal sealed class ProcessState
{
    public int ProcessId { get; }
    public string ProcessName { get; }
    public string? ProcessPath { get; }

    /// <summary>
    /// Threading lock for event list manipulation.
    /// In a massive enterprise system, we might use a lock-free buffer,
    /// but for a localized lightweight desktop ransomware monitor processing < 10k evts/sec,
    /// a simple lock performs exceptionally well with lowest overhead.
    /// </summary>
    private readonly object _syncLock = new();
    
    // Store events in chronological order
    private readonly List<FileSystemEvent> _events = new(1024);

    // Track recently created files to distinguish Pre-Existing modification vs New File modification.
    // Use an LRU mechanism or capped bound.
    private readonly HashSet<string> _newlyCreatedFiles = new(StringComparer.OrdinalIgnoreCase);

    public DateTimeOffset LastEventUtc { get; private set; }

    public ProcessState(int processId, string processName, string? processPath)
    {
        ProcessId = processId;
        ProcessName = processName;
        ProcessPath = processPath;
        LastEventUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Appends a new event and updates the activity timestamp.
    /// Optionally prunes extremely old events inline to bound memory.
    /// </summary>
    public void AddEvent(FileSystemEvent evt, TimeSpan contextWindow)
    {
        lock (_syncLock)
        {
            if (evt.EventType == FileSystemEventType.Create)
            {
                _newlyCreatedFiles.Add(evt.FilePath);
                // Safety bound to prevent memory leak from unconstrained installers.
                if (_newlyCreatedFiles.Count > 15000)
                {
                    _newlyCreatedFiles.Clear();
                }
            }
            else
            {
                _events.Add(evt);
            }

            var now = evt.Timestamp;
            LastEventUtc = now > LastEventUtc ? now : LastEventUtc;
            
            // Inline prune to ensure stable memory bound under massive bursts, 
            // taking everything older than the context window.
            var cutoff = now - contextWindow;
            _events.RemoveAll(e => e.Timestamp < cutoff);
        }
    }

    /// <summary>
    /// Checks if a file was inherently created by this process recently.
    /// </summary>
    public bool IsNewlyCreated(string filePath)
    {
        lock (_syncLock)
        {
            return _newlyCreatedFiles.Contains(filePath);
        }
    }

    /// <summary>
    /// Produces a thread-safe snapshot array of events remaining inside the supplied bounds.
    /// Also prunes stale events that fall outside the full Context contextWindow.
    /// </summary>
    public IReadOnlyList<FileSystemEvent> GetEventsAndPrune(DateTimeOffset cutoff)
    {
        lock (_syncLock)
        {
            _events.RemoveAll(e => e.Timestamp < cutoff);
            
            // To prevent caller from locking or cloning continuously, 
            // return a materialized array copy.
            return _events.ToArray();
        }
    }
}
