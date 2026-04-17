using ActDefend.Core.Models;

namespace ActDefend.Core.Interfaces;

/// <summary>
/// Contract for the ETW event collector component.
/// Implementations subscribe to ETW providers, normalize raw events into
/// <see cref="FileSystemEvent"/> records, and publish them downstream.
/// 
/// Design rules (from brief §7):
/// - Do minimal work inside the hot callback path.
/// - All expensive processing happens downstream.
/// - Bounded buffering must be applied to avoid unbounded memory growth.
/// </summary>
public interface IEventCollector
{
    /// <summary>
    /// True when the collector is actively receiving and publishing ETW events.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Starts the ETW session. Must be called on an elevated (Administrator) process.
    /// Throws <see cref="InvalidOperationException"/> if already running.
    /// </summary>
    /// <param name="cancellationToken">Token to observe for shutdown.</param>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops the ETW session and releases ETW session resources.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Hot event stream. Consumers must process events quickly or use a buffered reader.
    /// The collector will drop events and log a warning when the consumer falls behind.
    /// </summary>
    IAsyncEnumerable<FileSystemEvent> ReadEventsAsync(CancellationToken cancellationToken);

    /// <summary>Number of events dropped since the last reset (backpressure indicator).</summary>
    long DroppedEventCount { get; }
}
