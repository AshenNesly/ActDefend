# False Positive Reduction

This document describes the targeted tuning changes made in Phase 8e to reduce false positives from heavy benign workloads (software installers, compilers, unzippers, package managers) without weakening detection of actual ransomware-like behaviour.

## Context

After fixing the simulator detection blind spot in Phase 8d, the full detection pipeline became active against all high-volume file modification. However, heavy benign workloads occasionally triggered false positive Stage 1 alerts — and in some cases Stage 2 incorrectly confirmed them.

Three independent root causes were identified and addressed.

---

## Fix 1 — Stage 2: High-Entropy Benign Formats

**Root cause:** Compressed and compiled file formats (`.dll`, `.exe`, `.zip`, `.png`, `.pak`, etc.) have Shannon entropy naturally above 7.2 bits/byte — statistically identical to encrypted data. When Stage 1 flagged a compiler or installer, Stage 2 measured the `.dll` files it was writing and incorrectly confirmed them as ransomware.

**Fix:** Introduced `KnownBenignHighEntropyExtensions` in `EntropySamplingEngine`. When `TrySampleFile` encounters a file whose extension is in this set, the file is skipped and the engine moves to the next candidate.

Current exclusion list:
- Binaries/libraries: `.dll`, `.exe`, `.sys`, `.pdb`, `.o`, `.a`, `.so`, `.dylib`, `.class`, `.jar`
- Caches/packages: `.cache`, `.pack`, `.idx`, `.nupkg`, `.npm`, `.gem`
- Media/assets: `.png`, `.jpg`, `.jpeg`, `.mp4`, `.mp3`, `.pak`, `.vpk`
- Archives: `.zip`, `.tar`, `.gz`, `.7z`, `.rar`, `.cab`, `.lz4`

**Why this is safe:**
- Ransomware targets user documents (`.docx`, `.pdf`, `.xls`, `.txt`, `.csv`) which are intentionally **not** on the exclusion list.
- The write-then-rename pattern substitutes the original extension with a ransomware extension (e.g. `.locked`, `.crypto`). These are not on the list, so the renamed file is correctly sampled.
- `photo.jpg` renamed to `photo.jpg.locked` has extension `.locked` (not `.jpg`), so it passes through and is correctly measured.

---

## Fix 2 — Stage 1: Pure Write-Only Processes Over-Penalised

**Root cause:** When a process wrote files but read no files, `primaryReads` was 0. The original code assigned `double.MaxValue` as the `WriteReadRatio`, and the scoring engine capped it at the full 10-point weight — penalising all pure-write-only workloads (network downloaders, stream extractors) at the maximum.

Ransomware performing in-place encryption *must* read the original file before overwriting it, so it produces a nonzero read count. The maximum penalty on zero-read processes was a false signal.

**Fix:** In `FeatureExtractor.Emit()`, `WriteReadRatio` is set to `0.0` when `primaryReads == 0`. Pure write-only processes receive 0 contribution from this feature.

```csharp
double ratio = 0;
if (primaryWrites > 0)
{
    ratio = primaryReads > 0
        ? (double)primaryWrites / primaryReads
        : 0.0;  // changed from double.MaxValue
}
```

**Why this is safe:**
- Installers and downloaders lose up to 10 points, which moves them away from the 60-point threshold unless they also hit near-maximum values on other axes.
- The simulator writes random bytes and then renames (not a pure-write workload — it also reads indirectly), and still scores well above 60 on RenameRate, DirectorySpread, and PreExistingModifyRate.

---

## Fix 3 — Stage 1: New-File Creation Penalised Like Pre-Existing File Attacks

**Root cause:** The original scoring gave equal weight to all write events. A compiler writing thousands of new `.o` files and ransomware overwriting thousands of existing user files looked identical to the feature extractor.

The key distinction between a benign installer and ransomware is:
- **Installer:** creates entirely new files in a destination directory — does not modify pre-existing user data.
- **Ransomware:** must modify, overwrite, or rename files that already existed before the attack began.

**Fix implemented in two parts:**

### Part A — `FileSystemEventType.Create` as a separate event type

`EtwEventCollector` now maps `FileIOCreate` events with NT disposition codes `SUPERSEDE` (0), `CREATE_NEW` (2), or `CREATE_ALWAYS` (5) to `FileSystemEventType.Create` rather than `FileSystemEventType.Write`. Pure open-existing operations are dropped.

`ProcessState.AddEvent()` intercepts `Create` events and adds the file path to a `HashSet<string> _newlyCreatedFiles`. Create events are **not** stored in the general event list — they do not contribute to write/read/rename metrics.

### Part B — `PreExistingModifyRatePerSec` feature

`FeatureExtractor` tracks a `preExistingTouches` counter. For each Write, Rename, or Delete event in the primary window, the event is counted as a pre-existing modification only if the file path is **not** in `_newlyCreatedFiles`. The `PreExistingModifyRatePerSec` feature is this counter divided by the primary window duration.

This feature carries the highest single Stage 1 weight: **25 points** (vs. 10 for the generic `WriteRate`).

**Why this is safe:**
- Installers write new files to destination directories. These files are created by the same process (`Create` event adds them to `_newlyCreatedFiles`). Subsequent writes to those same paths give 0 pre-existing contribution.
- File-copy tools (`robocopy`, `xcopy`) create destination files before writing them — same pattern, 0 contribution.
- Ransomware targets pre-existing user files (documents, photos) that were created by other processes (Word, the user). These paths are not in `_newlyCreatedFiles`, so every write/rename/delete on them increments `preExistingTouches`.

---

## Safety Bound on `_newlyCreatedFiles`

The `HashSet<string>` in `ProcessState` is capped at 15 000 entries. If a single process creates more than 15 000 files (e.g. an aggressive build tool), the entire set is cleared. This is a known edge case: after the clear, newly created files temporarily appear as "pre-existing" to the feature extractor, which could briefly inflate `PreExistingModifyRatePerSec`. In practice this is rare and self-correcting within the next context window cycle.

---

## Weight Summary After Phase 8e

| Feature | Weight | Rationale |
|---|---|---|
| `WriteRate` | 10 pts | Reduced; covered more precisely by PreExistingModifyRate |
| `UniqueFilesWritten` | 15 pts | Still useful for burst breadth |
| `RenameRate` | 20 pts | Strong ransomware signal (extension substitution) |
| `DirectorySpread` | 20 pts | Multi-folder traversal pattern |
| `WriteReadRatio` | 10 pts | Weak signal; 0 when reads = 0 |
| `PreExistingModifyRate` | **25 pts** | Highest weight — strongest discriminator |

---

## Remaining Tuning Gap

Heavy development workloads that produce temporary `.txt` log files and then delete/modify them can still cross the `PreExistingModifyRate` threshold if they are not on the trusted-process allow-list. Adding process names to `TrustedProcesses.DefaultExclusions` in `appsettings.json` is the current mitigation.
