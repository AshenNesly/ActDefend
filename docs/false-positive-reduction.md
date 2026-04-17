# False Positive Reduction (Phase 8e)

This document tracks focused, careful tuning adjustments made to reduce false positives originating from heavy benign workloads (like software installers, unzippers, and compilers) without weakening actual ransomware detection capability.

## Context

After fixing the simulator detection blind spot in Phase 8d, the detector became fully active against high-volume file modification. However, benign heavy workloads (e.g. `npm install`, downloading packages, extracting zip files) were occasionally triggering false positive alerts.

## 1. Stage 1 Root Cause: Pure-Write Penalization

**Root Cause Identified:** 
The `LightweightScoringEngine` computes a `WriteReadRatio` metric. In `FeatureExtractor`, if a burst contained writes but 0 reads, the ratio was assigned `double.MaxValue`. The scoring engine gave max points (15.0) to any ratio above `5.0`. This heavily penalized pure downloaders/installers (which generate new files from a network stream without reading local disk files). Ransomware, conversely, *must* read a local file to encode it (unless it deletes the original and writes garbage, which is wiper behaviour).

**Tuning Change Made:**
- In `FeatureExtractor.Emit()`, the `WriteReadRatio` is now explicitly set to `0.0` when `primaryReads == 0`. 

**Why it is safe:**
- Software installers naturally drop down by 15 points, moving them out of the `60.0` Suspicion block unless they hit near-maximum bounds on all other scales (writes, spread, uniqueness). 
- *Simulator testing:* The simulator generates purely new files, so it loses 15 points (down from 100 to 85) but 85 still easily bypasses the `60.0` threshold. Real ransomware performs in-place writes (Ratio ~ 1.0) and will continue to score normally.

## 2. Stage 2 Root Cause: High-Entropy Compressed Formats

**Root Cause Identified:**
Stage 2 validates exactly if a file is purely encrypted by looking at mathematically calculated Shannon Entropy. The problem is that benign compressed or compiled formats (`.dll`, `.exe`, `.pack`, `.deb`, `.zip`, `.png`) are structurally identical to encrypted data (entropy > 7.2). When Stage 1 flags a builder or unzipper, Stage 2 correctly validates the `.dll`s as random data and incorrectly confirms them as ransomware.

**Tuning Change Made:**
- Introduced `KnownBenignHighEntropyExtensions` into `EntropySamplingEngine`.
- If Stage 2 is handed a known compressed developer/system format (like `.dll`, `.exe`, `.cache`, `.zip`, `.pak`, `.jpg`), it skips entropy measurement on that file to safely prevent mathematically generated false positives.

**Why it is safe:**
- Ransomware encrypts *everything* indiscriminately. Even if Stage 2 skips over a `.jpg`, it will simultaneously capture the `.docx`, `.pdf`, `.xls`, `.txt`, `.rtf`, or `.csv` being encrypted beside it.
- Furthermore, ransomware relying on a Write-Then-Rename format substitutes standard extensions for its own (e.g., `.locked`, `.crypto`). If the algorithm samples the renamed `photo.jpg.locked`, the actual path extension is `.locked`. Since `.locked` is not in the safe-list, the picture is properly sampled and correctly acts as confirmation. 
- *We intentionally excluded `.docx` and `.xlsx` from the internal exclusion list* since they are the primary target of data destruction (even though they technically are zipped containers). They are expected to be hit immediately by ransomware, proving safety without blinding the tool.

## Validation Performed
- **Simulator Check:** The system correctly ignores the pure write loop penalty but correctly alerts on the `.locked` extensions appended to files, triggering exactly matching true positives.
- **Unit Testing:** Integrated 2 new unit tests asserting that extracting purely write metrics results in 0.0 penalization and that `.dll` probes are rejected purely at the array probe level. (Total tests passed: 42).

## Remaining Tuning Gaps
- **Trusted Executable Logic:** The system still acts cleanly by rejecting paths/extensions natively, but heavily aggressive compilers touching non-compiled `.txt` temporary logs still might cross boundaries. An eventual SQLite Trust-List API implementation is required for manual overrides on specific development environment pathways.
