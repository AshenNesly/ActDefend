using ActDefend.Core.Interfaces;
using ActDefend.Core.Models;
using Microsoft.Extensions.Logging;

namespace ActDefend.Features;

/// <summary>
/// Placeholder feature extractor for Phase 1.
///
/// Phase 3 replaces this with real sliding-window aggregation:
/// - per-process bounded state (ConcurrentDictionary)  
/// - primary window (write/rename burst detection)
/// - context window (stabilisation)
/// - inactivity expiry
///
/// For now this simply satisfies the IFeatureExtractor contract so the
/// DI graph and early pipeline tests compile cleanly.
/// </summary>
internal sealed class FeatureExtractor : IFeatureExtractor
{
    private readonly ILogger<FeatureExtractor> _logger;

    public FeatureExtractor(ILogger<FeatureExtractor> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public int ActiveProcessCount => 0;

    /// <inheritdoc />
    public void ProcessEvent(FileSystemEvent evt)
    {
        // Phase 3: update per-process sliding window state.
        _logger.LogTrace("[PLACEHOLDER] ProcessEvent PID={Pid} type={Type} path={Path}",
            evt.ProcessId, evt.EventType, evt.FilePath);
    }

    /// <inheritdoc />
    public IReadOnlyList<FeatureSnapshot> Emit()
    {
        // Phase 3: produce FeatureSnapshot per active process.
        return [];
    }

    /// <inheritdoc />
    public void ExpireInactiveState()
    {
        // Phase 3: remove stale process contexts.
    }
}
