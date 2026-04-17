# Stage 2 Confirmation Engine (Entropy)

The `Detector.Entropy` project acts as the definitive ransomware discriminator in the system architecture. It triggers *only* when the `Detector.Detection` Stage 1 engine flags a process as anomalous, saving CPU and Disk I/O across the enterprise.

## Core Concepts

### Shannon Entropy Mathematics
Ransomware actively encrypts user data, destroying language structure and injecting pure mathematical randomness. The confirmation engine calculates Shannon entropy.
`H = -Σ p(b) * log2(p(b))`
A perfectly compressed or encrypted byte array sits near `8.0`. Standard plaintext sits lower (around `4.0-5.0`). ActDefend considers anything over `EntropyThreshold` (default `7.2`) to be highly suspicious.

### Safe Live I/O Bounds
A major risk for EDR solutions is blocking the user's system by aggressively scanning files or crashing when ransomware locks target payloads sequentially.
1. **Passive Access Restrictions:** `EntropySamplingEngine` executes `FileShare.ReadWrite | FileShare.Delete` allowing it to passively analyze files without throwing Windows sharing violation locks against the active malware (preventing detection evasion).
2. **Strict Bounds:** It will NEVER read a full file. It strictly cuts off buffer loads at `64KB` max (`SampleBytesLimit`). It only targets a maximum of `5` newly written files per process run (`MaxFilesToSample`).

### Cooldown Fuses 
A common failure in lightweight scanners is the "Cascade Loop"—a ransomware hitting 500 files a second triggers 500 Stage 2 scans instantly, burning CPU to 100%.
The Entropy Engine tracks a strict localized Cooldown integer map per Process. Defaulting to `10 seconds`, if a PID fails confirmation, it is entirely ignored by Stage 2 for the rest of that window regardless of how many Stage 1 Suspicion boundaries it crosses, completely protecting machine stability while under attack. 

## Integration with the Orchestrator
When the engine confirms mathematically that `ExceedsThreshold` bounds match against the limits (default min 2 files), it generates a true confirmation `EntropyResult`.

The orchestrator then merges the Stage 1 vector mappings (Why it was suspicious) with the Stage 2 Entropy Confirmation (Mathematical proof), emitting a structured JSON alert into the broader application.
