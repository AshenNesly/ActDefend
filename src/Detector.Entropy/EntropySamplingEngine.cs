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

    // Known binary, media, or compressed asset extensions.
    // Defensively ignoring these prevents False Positives when benign tools (IDEs, installers, downloaders)
    // write heavy binary files that naturally exceed the entropy threshold.
    // Ransomware modifying user documents will still be caught (since we don't exclude .docx, .pdf, etc),
    // and Simulator/Rename ransomware appending '.locked' will bypass this list and be correctly sampled.
    private static readonly HashSet<string> KnownBenignHighEntropyExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Core binaries/libraries (Installers/Builders)
        ".dll", ".exe", ".sys", ".pdb", ".o", ".a", ".so", ".dylib", ".class", ".jar",
        // Caches and Package formats
        ".cache", ".pack", ".idx", ".nupkg", ".npm", ".gem",
        // Media/Assets (Extracting large asset bundles)
        ".png", ".jpg", ".jpeg", ".mp4", ".mp3", ".pak", ".vpk",
        // Known compressed archives (purely extracted/downloaded)
        ".zip", ".tar", ".gz", ".7z", ".rar", ".cab", ".lz4"
    };

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
        // Record cooldown timestamp for this process before any IO.
        _cooldowns[result.Snapshot.ProcessId] = DateTimeOffset.UtcNow;

        var sampleList = new List<FileSample>();

        // Build a merged, deduplicated candidate list from:
        //   1. Recently WRITTEN file paths (may have been renamed away since the write)
        //   2. Recently RENAMED source paths (original names before extension substitution)
        // Combining both gives Stage 2 the widest possible survivor set to probe.
        // TrySampleFile will also attempt common ransomware extensions when a path is not found.
        var candidates = result.Snapshot.RecentWrittenFiles
            .Concat(result.Snapshot.RecentRenamedSourceFiles)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(_options.MaxFilesToSample)
            .ToList();

        if (candidates.Count == 0)
        {
            _logger.LogDebug(
                "Stage 2 — PID={Pid}: no candidate files available for entropy sampling.",
                result.Snapshot.ProcessId);
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
            _logger.LogDebug(
                "Stage 2 — PID={Pid}: failed to read all {Count} target(s) — may all be renamed or deleted.",
                result.Snapshot.ProcessId, candidates.Count);
            return BuildResult(result, false, sampleList,
                $"Failed to read any of {candidates.Count} sampled target(s) — possibly renamed or deleted.");
        }

        var averageEntropy = sampleList.Average(s => s.ShannonEntropy);
        var highEntropyCount = sampleList.Count(s => s.ExceedsThreshold);

        var isConfirmed = highEntropyCount >= _options.ConfirmationMinFiles;

        var explanation = $"Evaluated {sampleList.Count} files. " +
                          $"High entropy count: {highEntropyCount} (Threshold: {_options.ConfirmationMinFiles}).";

        var finalResult = BuildResult(result, isConfirmed, sampleList, explanation);
        return finalResult with
        {
            AverageEntropy = averageEntropy,
            HighEntropyFileCount = highEntropyCount
        };
    }

    /// <summary>
    /// Tries to read and entropy-sample the file at <paramref name="filePath"/>.
    /// If the path itself cannot be opened (e.g. the file was renamed by ransomware to a new
    /// extension), probes a set of well-known ransomware extensions appended to the original path.
    /// This makes Stage 2 robust against the common write-then-rename pattern used by ransomware
    /// and reproduced by the simulator.
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
    private bool TrySampleFile(string filePath, out FileSample sample)
    {
        sample = default!;

        // Try the original path first, then fall back to renamed versions.
        // The fallback extension list covers the simulator (.locked) and common real-world patterns.
        ReadOnlySpan<string> extensionProbes =
        [
            "",           // original path as-is
            ".locked",
            ".encrypted",
            ".enc",
            ".crypto",
            ".crypted"
        ];

        foreach (var ext in extensionProbes)
        {
            var probeTarget = ext.Length == 0 ? filePath : filePath + ext;

            // Prevent benign compressed/binary files from triggering False Positives.
            var targetExt = Path.GetExtension(probeTarget);
            if (KnownBenignHighEntropyExtensions.Contains(targetExt))
            {
                continue;
            }

            try
            {
                // Use maximum permissiveness — allow files still open by the suspected process.
                using var stream = new FileStream(
                    probeTarget, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);

                var lengthToRead = Math.Min(stream.Length, _options.SampleBytesLimit);
                if (lengthToRead <= 0)
                    continue;

                var buffer = new byte[lengthToRead];
                var bytesRead = stream.Read(buffer, 0, (int)lengthToRead);

                if (bytesRead <= 0)
                    continue;

                var entropy = CalculateShannonEntropy(buffer.AsSpan(0, bytesRead));
                var exceeds  = entropy >= _options.EntropyThreshold;

                if (ext.Length > 0)
                    _logger.LogTrace(
                        "Stage 2 — sampled renamed file '{Probe}' (original: '{Original}'). Entropy={E:F2}",
                        probeTarget, filePath, entropy);

                sample = new FileSample(probeTarget, bytesRead, entropy, exceeds);
                return true;
            }
            catch (FileNotFoundException)
            {
                // Path doesn't exist with this extension — try the next probe.
                continue;
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Stage 2 — entropy probe failed for '{File}'", probeTarget);
                // Don't give up on the other extensions; only stop retrying for truly fatal errors.
                continue;
            }
        }

        return false;
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
