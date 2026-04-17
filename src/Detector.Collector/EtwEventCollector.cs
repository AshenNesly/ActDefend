using System.Runtime.CompilerServices;
using System.Threading.Channels;
using ActDefend.Core.Interfaces;
using ActDefend.Core.Models;
using Microsoft.Extensions.Logging;

namespace ActDefend.Collector;

/// <summary>
/// Placeholder ETW collector implementation for Phase 1.
///
/// This class satisfies the IEventCollector interface contract so the
/// full DI graph compiles and the application host starts correctly.
///
/// Phase 2 will replace ReadEventsAsync with a real ETW session reading
/// from a bounded Channel written by the ETW callback.
///
/// The bounded Channel is wired up here already (Phase 1 scaffolding)
/// so Phase 2 only needs to populate it from the ETW callback.
/// </summary>
internal sealed class EtwEventCollector : IEventCollector
{
    private readonly ILogger<EtwEventCollector> _logger;

    // Bounded channel — provides backpressure between ETW callback and consumer.
    // Phase 2 will write into this from the real ETW session callback.
    private Channel<FileSystemEvent>? _channel;
    private volatile bool _running;
    private long _droppedEvents = 0;           // incremented on channel-full drops

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

        // Create bounded channel — capacity comes from config in Phase 2.
        // Using a fixed capacity here keeps Phase 1 self-contained.
        _channel = Channel.CreateBounded<FileSystemEvent>(
            new BoundedChannelOptions(4096)
            {
                FullMode     = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = false   // Phase 2: ETW callback may be multi-threaded
            });

        _logger.LogWarning(
            "[PLACEHOLDER] EtwEventCollector.StartAsync — " +
            "Real ETW session will be implemented in Phase 2. No events will be emitted.");

        _running = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _running = false;
        _channel?.Writer.TryComplete();
        _logger.LogInformation("[PLACEHOLDER] EtwEventCollector stopped.");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<FileSystemEvent> ReadEventsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_channel is null)
        {
            _logger.LogError("ReadEventsAsync called before StartAsync. No events will flow.");
            yield break;
        }

        // Phase 2: the real ETW callback will write into _channel.Writer.
        // Phase 1: channel is never written to, so we block until cancellation.
        await foreach (var evt in _channel.Reader.ReadAllAsync(cancellationToken)
                                                  .ConfigureAwait(false))
        {
            yield return evt;
        }
    }
}
