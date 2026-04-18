# Stage 2 Confirmation Engine (Entropy)

The `Detector.Entropy` project acts as the definitive ransomware discriminator in the system architecture. It triggers *only* when the `Detector.Detection` Stage 1 engine flags a process as anomalous, saving CPU and Disk I/O continuously.

## Core Concepts

### Shannon Entropy Mathematics
Ransomware actively encrypts user data, destroying language structure and injecting pure mathematical randomness. The confirmation engine calculates Shannon entropy:

```
H = -Σ p(b) * log2(p(b))
```

A perfectly compressed or encrypted byte array sits near `8.0`. Standard plaintext sits lower (around `4.0–5.0`). ActDefend considers anything over `EntropyThreshold` (default `7.2`) to be highly suspicious.

### Candidate File Discovery — Rename-Aware Probing (Phase 8 Fix)

**Root cause fixed:** The original implementation only sampled files from `RecentWrittenFiles`. Real ransomware (and the simulator) follows a write-then-rename pattern: a file is written with high-entropy bytes, then immediately renamed to a ransomware extension (`.locked`, `.encrypted`, etc.). By the time Stage 2 ran, the original paths no longer existed, so `TrySampleFile` failed silently for all candidates and always returned `IsConfirmed = false`.

**Fix:** `AnalyseAsync` now merges two candidate lists before sampling:

1. `RecentWrittenFiles` — paths of files with recent write events (may have been renamed away)
2. `RecentRenamedSourceFiles` — source paths from recent rename events (original name before extension substitution)

Both lists are deduplicated before sampling. For each candidate path, `TrySampleFile` first tries the original path as-is, then — if the original is not readable — probes 5 common ransomware extensions appended to that path:

| Probe | Covers |
|---|---|
| `""` (original path) | file still at its original location |
| `.locked` | simulator + common ransomware |
| `.encrypted` | common ransomware |
| `.enc` | common ransomware |
| `.crypto` | common ransomware |
| `.crypted` | common ransomware |

The first readable probe wins; remaining probes are skipped. A `LogTrace` entry is emitted when a renamed variant (non-empty extension) is successfully found.


### Safe Live I/O Bounds
1. **Passive Access:** `FileShare.ReadWrite | FileShare.Delete` allows passive analysis without blocking the active process.
2. **Strict Bounds:** Read capped at `64 KB` per file. Maximum `5` files sampled per trigger.

### High-Entropy Format Exclusion (False Positive Mitigation)
A critical feature in filtering safe high-entropy writes vs actual ransomware payloading. Benign extensions representing heavily compressed data structures (`.dll`, `.exe`, `.pack`, `.zip`, `.png`, `.jpg`, `.cache`) naturally measure at >7.5 Shannon entropy. 
When sampled directly, they generate massive false positives against background Installers or system extraction mechanisms. 
- **Methodology:** Stage 2 maintains a strict check against `KnownBenignHighEntropyExtensions`. Matches safely skip the file sample calculation entirely.
- **Why this does not blind the system:** Rename-based ransomware overwrites extensions (`.locked`, `.crypto`) bypassing the check because `.locked` isn't within the hash set and properly reads the mathematical entropy. Furthermore, standard target targets like `.docx` and `.xlsx` are not excluded, ensuring in-place modification of Office Documents is structurally caught.

### Cooldown Fuses
A per-PID cooldown map (default `10 seconds`) prevents Stage 2 from being re-triggered for the same process while it is on cooldown, protecting CPU stability under rapid burst conditions.

## Integration with the Orchestrator

When the engine confirms that `highEntropyCount >= ConfirmationMinFiles` (default `2`), it generates a true `EntropyResult.IsConfirmed = true`. The orchestrator then merges Stage 1 scoring data with Stage 2 confirmation to emit a structured `DetectionAlert`.

If Stage 2 finds no readable files at all (all paths missing and no extension variant found), it returns `IsConfirmed = false` with a diagnostic explanation in the result, logged at `Debug` level.

## Configuration Reference

| Key | Default | Description |
|---|---|---|
| `Stage2.EntropyThreshold` | `7.2` | Minimum entropy (bits/byte) to flag a file as high-entropy |
| `Stage2.SampleBytesLimit` | `65536` | Max bytes read per file sample |
| `Stage2.MaxFilesToSample` | `5` | Max candidate files sampled per Stage 2 trigger |
| `Stage2.ConfirmationMinFiles` | `2` | Min high-entropy files required for confirmation |
| `Stage2.CooldownSeconds` | `10` | Per-process Stage 2 re-trigger cooldown |

## Testing

Unit tests in `tests/Detector.UnitTests/Entropy/EntropySamplingEngineTests.cs` cover:

- Shannon entropy calculation correctness (zero, uniform, random data)
- Cooldown tracking
- Empty candidate list bypass
- **Stage 2 confirms when original file is renamed to `.locked`** (root cause regression test)
- **Deduplication of merged candidate lists**
- **Non-confirmation when `.locked` file has low entropy** (false-positive guard)
