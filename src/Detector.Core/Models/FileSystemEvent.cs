namespace ActDefend.Core.Models;

/// <summary>
/// A normalized file-system event produced by the ETW Collector.
/// All ETW-specific detail is stripped; downstream components see only this model.
/// </summary>
/// <param name="Timestamp">UTC instant the event occurred.</param>
/// <param name="ProcessId">OS process ID that triggered the event.</param>
/// <param name="ProcessName">Short image name (e.g. "notepad.exe").</param>
/// <param name="ProcessPath">Full executable path if available, or null.</param>
/// <param name="EventType">Category of file-system operation.</param>
/// <param name="FilePath">Absolute path of the file affected.</param>
/// <param name="OldFilePath">Previous path for rename events; null for others.</param>
public sealed record FileSystemEvent(
    DateTimeOffset    Timestamp,
    int               ProcessId,
    string            ProcessName,
    string?           ProcessPath,
    FileSystemEventType EventType,
    string            FilePath,
    string?           OldFilePath = null);

/// <summary>
/// Categories of file-system operations relevant to ransomware detection.
/// Kept deliberately small — the collector maps raw ETW opcodes onto these buckets.
/// </summary>
public enum FileSystemEventType
{
    /// <summary>File content was written or a new file was created.</summary>
    Write,
    /// <summary>File was opened for reading only.</summary>
    Read,
    /// <summary>File or directory was renamed or moved.</summary>
    Rename,
    /// <summary>File was deleted.</summary>
    Delete,
    /// <summary>An ETW event was received but did not fit the above categories.</summary>
    Other
}
