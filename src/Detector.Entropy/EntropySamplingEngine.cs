using ActDefend.Core.Configuration;
using ActDefend.Core.Interfaces;
using ActDefend.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActDefend.Entropy;

/// <summary>
/// Placeholder Stage 2 entropy sampling engine for Phase 1.
///
/// Phase 5 replaces this with the real implementation:
/// - Open recently-written files (bounded by MaxFilesToSample)
/// - Read up to SampleBytesLimit bytes per file
/// - Compute Shannon entropy: H = -Σ p(b) * log2(p(b))
/// - Count files exceeding EntropyThreshold
/// - Confirm if ConfirmationMinFiles threshold is met
/// - Track per-process cooldown to prevent CPU spikes
///
/// This placeholder always returns IsConfirmed=false so the pipeline
/// compiles and starts without triggering false alerts during Phase 1.
/// </summary>
internal sealed class EntropySamplingEngine : IEntropyEngine
{
    private readonly ILogger<EntropySamplingEngine> _logger;
    private readonly Stage2Options _options;

    // Per-process cooldown tracking: processId → last check time.
    private readonly Dictionary<int, DateTimeOffset> _cooldowns = new();

    public EntropySamplingEngine(
        ILogger<EntropySamplingEngine> logger,
        IOptions<ActDefendOptions> options)
    {
        _logger  = logger;
        _options = options.Value.Stage2;
    }

    /// <inheritdoc />
    public bool IsReady(int processId)
    {
        if (!_cooldowns.TryGetValue(processId, out var lastCheck))
            return true;

        return (DateTimeOffset.UtcNow - lastCheck).TotalSeconds >= _options.CooldownSeconds;
    }

    /// <inheritdoc />
    public Task<EntropyResult> AnalyseAsync(
        ScoringResult result,
        CancellationToken cancellationToken)
    {
        // Record cooldown timestamp for this process.
        _cooldowns[result.Snapshot.ProcessId] = DateTimeOffset.UtcNow;

        _logger.LogDebug(
            "[PLACEHOLDER] EntropySamplingEngine.AnalyseAsync — PID={Pid}. " +
            "Real entropy sampling implemented in Phase 5. Returning not-confirmed.",
            result.Snapshot.ProcessId);

        // Phase 5: perform real bounded file I/O and compute Shannon entropy here.
        return Task.FromResult(new EntropyResult
        {
            Timestamp           = DateTimeOffset.UtcNow,
            ProcessId           = result.Snapshot.ProcessId,
            ProcessName         = result.Snapshot.ProcessName,
            IsConfirmed         = false,
            Samples             = [],
            AverageEntropy      = 0.0,
            HighEntropyFileCount = 0,
            Explanation         = "Phase 1 placeholder — entropy sampling not yet implemented.",
            TriggerResult       = result
        });
    }
}
