using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using ActDefend.Core.Interfaces;
using ActDefend.Core.Models;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Extensions.Logging;

namespace ActDefend.Collector;

/// <summary>
/// Real ETW collector implementation using Microsoft.Diagnostics.Tracing.TraceEvent.
///
/// Subscribes to Kernel FileIO events to capture file modifications.
/// Runs the heavy ETW processing loop on a background thread and shunts
/// normalized FileSystemEvents into a bounded channel to provide backpressure
/// and decouple collection from feature extraction.
/// </summary>
public sealed class EtwEventCollector : IEventCollector, IDisposable
{
    private const string SessionName = "ActDefend-Monitor-Session";
    
    private readonly ILogger<EtwEventCollector> _logger;
    private Channel<FileSystemEvent>? _channel;
    
    private TraceEventSession? _session;
    private Task? _processingTask;
    private volatile bool _running;
    private long _droppedEvents = 0;

    // Cache to avoid hitting Process.GetProcessById constantly.
    // In a full implementation, we would subscribe to Kernel Process events to maintain this natively,
    // but a sliding/lazy cache is lightweight enough for Phase 2.
    private readonly ConcurrentDictionary<int, string> _processNameCache = new();

    public EtwEventCollector(ILogger<EtwEventCollector> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsRunning => _running;

    /// <inheritdoc />
    public long DroppedEventCount => Interlocked.Read(ref _droppedEvents);

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_running)
            throw new InvalidOperationException("Collector is already running.");

        _logger.LogInformation("Starting ETW Event Collector initialization...");

        _channel = Channel.CreateBounded<FileSystemEvent>(
            new BoundedChannelOptions(8192)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = true // Processing loop runs single-threaded from ETW Source
            });

        try
        {
            // Note: If a previous crash left the session active, we must dispose it before recreating.
            // Avoid using standard Kernel logger as it is heavily contended. Using a named session avoids conflicts.
            // Note: Accessing Kernel events inside a named session requires Windows 8+.
            if (TraceEventSession.GetActiveSessionNames().Contains(SessionName))
            {
                _logger.LogWarning("Found orphaned ETW session '{SessionName}', stopping it...", SessionName);
                using var orphan = new TraceEventSession(SessionName);
                orphan.Stop(noThrow: true);
            }

            _session = new TraceEventSession(SessionName);
            
            // Subscribe to Kernel File IO events.
            _session.EnableKernelProvider(
                KernelTraceEventParser.Keywords.FileIOInit |
                KernelTraceEventParser.Keywords.FileIO);

            var parser = _session.Source.Kernel;
            
            // Map relevant ETW file operations
            parser.FileIOCreate += HandleFileCreate;
            parser.FileIOWrite += HandleFileWrite;
            parser.FileIORename += HandleFileRename;
            parser.FileIODelete += HandleFileDelete;
            parser.FileIORead += HandleFileRead;

            _running = true;

            // Start the blocking ETW processing loop on a dedicated background thread.
            _processingTask = Task.Factory.StartNew(() =>
            {
                _logger.LogInformation("ETW TraceEventSession processing loop started.");
                try
                {
                    _session.Source.Process();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ETW TraceEventSession processing loop threw an exception.");
                }
                _logger.LogInformation("ETW TraceEventSession processing loop stopped.");
            }, TaskCreationOptions.LongRunning);

            _logger.LogInformation("ETW Monitor Session '{SessionName}' successfully started.", SessionName);
            return Task.CompletedTask;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogCritical(ex, "Access denied creating ETW session. You MUST run as Administrator.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to start ETW session.");
            throw;
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (!_running) return Task.CompletedTask;
        _logger.LogInformation("Stopping ETW Event Collector...");

        _running = false;
        
        if (_session != null)
        {
            _session.Source.StopProcessing();
            _session.Dispose();
            _session = null;
        }

        _channel?.Writer.TryComplete();
        _processNameCache.Clear();

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<FileSystemEvent> ReadEventsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_channel is null)
        {
            _logger.LogError("ReadEventsAsync called before StartAsync.");
            yield break;
        }

        await foreach (var evt in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return evt;
        }
    }

    public void Dispose()
    {
        if (_running)
        {
            _session?.Dispose();
        }
    }

    // --- ETW Event Handlers ---

    private void HandleFileCreate(FileIOCreateTraceData data)
    {
        PublishEvent(FileSystemEventType.Write, data.ProcessID, data.FileName, null);
    }

    private void HandleFileWrite(FileIOReadWriteTraceData data)
    {
        PublishEvent(FileSystemEventType.Write, data.ProcessID, data.FileName, null);
    }

    private void HandleFileRead(FileIOReadWriteTraceData data)
    {
        PublishEvent(FileSystemEventType.Read, data.ProcessID, data.FileName, null);
    }

    private void HandleFileRename(FileIOInfoTraceData data)
    {
        // ETW FileRename often tracks old name and new name. 
        // In TraceEvent, FileIORenameTraceData gives FileName (old) and full data mapping.
        // For simplicity in Phase 2, we map FileName as the source.
        PublishEvent(FileSystemEventType.Rename, data.ProcessID, data.FileName, null); 
    }

    private void HandleFileDelete(FileIOInfoTraceData data)
    {
        PublishEvent(FileSystemEventType.Delete, data.ProcessID, data.FileName, null);
    }

    /// <summary>
    /// Normalizes the raw ETW data and pushes it to the bounded channel.
    /// Drops noisy system files and events lacking a valid PID/Path.
    /// </summary>
    private void PublishEvent(FileSystemEventType eventType, int processId, string? filePath, string? oldPath)
    {
        if (!_running || _channel == null) return;

        // Filter out obvious noise or incomplete ETW payloads
        if (processId <= 4 || string.IsNullOrWhiteSpace(filePath)) return;
        
        // Very basic noise filtering
        if (filePath.StartsWith(@"C:\Windows\") || filePath.EndsWith(".TMP", StringComparison.OrdinalIgnoreCase)) return;

        var processName = GetProcessName(processId);

        var normalizedEvent = new FileSystemEvent(
            Timestamp: DateTimeOffset.UtcNow,
            ProcessId: processId,
            ProcessName: processName,
            ProcessPath: null, // Full path lookup is expensive, left null for Phase 2
            EventType: eventType,
            FilePath: filePath,
            OldFilePath: oldPath
        );

        if (!_channel.Writer.TryWrite(normalizedEvent))
        {
            Interlocked.Increment(ref _droppedEvents);
        }
    }

    /// <summary>
    /// Attempts to resolve the process name. Uses a cache to avoid Process.GetProcessById thrashing.
    /// </summary>
    private string GetProcessName(int processId)
    {
        if (_processNameCache.TryGetValue(processId, out var name))
        {
            return name;
        }

        try
        {
            using var proc = Process.GetProcessById(processId);
            name = proc.ProcessName;
        }
        catch
        {
            name = "Unknown";
        }

        // Cap cache size defensively
        if (_processNameCache.Count > 5000)
        {
            _processNameCache.Clear();
        }

        _processNameCache[processId] = name;
        return name;
    }
}
