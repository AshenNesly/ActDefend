using System.Diagnostics.CodeAnalysis;
using ActDefend.Core.Configuration;
using ActDefend.Core.Interfaces;
using ActDefend.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActDefend.Entropy;

/// <summary>
/// Real Stage 2 entropy sampling engine.
///
/// Implements safe bounded Shannon Entropy profiling based off suspicious triggers.
/// Operates under strict cooldown boundaries per processes limiting excessive IO.
/// Extracts safe bounded snapshots directly from target File locations.
/// </summary>
public sealed class EntropySamplingEngine : IEntropyEngine
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
    public async Task<EntropyResult> AnalyseAsync(
        ScoringResult result,
        CancellationToken cancellationToken)
    {
        // Record cooldown timestamp for this process.
        _cooldowns[result.Snapshot.ProcessId] = DateTimeOffset.UtcNow;

        var sampleList = new List<FileSample>();
        var candidates = result.Snapshot.RecentWrittenFiles
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(_options.MaxFilesToSample)
            .ToList();

        if (candidates.Count == 0)
        {
            return BuildResult(result, false, sampleList, "No valid file targets available for entropy sampling.");
        }

        foreach (var path in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TrySampleFile(path, out var sample))
                continue;

            sampleList.Add(sample);
        }

        if (sampleList.Count == 0)
        {
            return BuildResult(result, false, sampleList, "Failed to read any assigned targets (possibly locked or deleted).");
        }

        var averageEntropy = sampleList.Average(s => s.ShannonEntropy);
        var highEntropyCount = sampleList.Count(s => s.ExceedsThreshold);
        
        var isConfirmed = highEntropyCount >= _options.ConfirmationMinFiles;

        var explanation = $"Evaluated {sampleList.Count} files. " +
                          $"High entropy count: {highEntropyCount} (Threshold: {_options.ConfirmationMinFiles}).";

        var finalResult = BuildResult(result, isConfirmed, sampleList, explanation);
        
        // Use property-based mutation (since it's an init-only property set via BuildResult, need to build it explicitly)
        return finalResult with 
        { 
            AverageEntropy = averageEntropy, 
            HighEntropyFileCount = highEntropyCount 
        };
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
    private bool TrySampleFile(string filePath, out FileSample sample)
    {
        sample = default!;
        
        try
        {
            // Use maximum permissiveness allowing files still open by the suspected ransomware to be passively analyzed
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            
            var lengthToRead = Math.Min(stream.Length, _options.SampleBytesLimit);
            if (lengthToRead <= 0)
                return false;

            var buffer = new byte[lengthToRead];
            var bytesRead = stream.Read(buffer, 0, (int)lengthToRead);

            if (bytesRead <= 0)
                return false;

            var entropy = CalculateShannonEntropy(buffer.AsSpan(0, bytesRead));
            var exceeds = entropy >= _options.EntropyThreshold;

            sample = new FileSample(filePath, bytesRead, entropy, exceeds);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Entropy Engine failed to read payload: {File}", filePath);
            return false; // Safely absorb read-locks or transient deletions common in malware.
        }
    }

    /// <summary>
    /// Computes Shannon entropy (H) bounds exactly natively. 
    /// </summary>
    public static double CalculateShannonEntropy(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0) return 0.0;

        Span<int> frequencies = stackalloc int[256];
        foreach (var b in data)
        {
            frequencies[b]++;
        }

        double entropy = 0;
        double dataLength = data.Length;

        foreach (var count in frequencies)
        {
            if (count == 0) continue;
            var p = count / dataLength;
            entropy -= p * Math.Log2(p);
        }

        return entropy;
    }

    private static EntropyResult BuildResult(ScoringResult trigger, bool confirmed, List<FileSample> samples, string reason)
    {
        return new EntropyResult
        {
            Timestamp = DateTimeOffset.UtcNow,
            ProcessId = trigger.Snapshot.ProcessId,
            ProcessName = trigger.Snapshot.ProcessName,
            IsConfirmed = confirmed,
            Samples = samples,
            AverageEntropy = 0,
            HighEntropyFileCount = 0,
            Explanation = reason,
            TriggerResult = trigger
        };
    }
}
