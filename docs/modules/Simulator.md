# Simulator Module (`Detector.Simulator`)

The `Detector.Simulator` is a strictly defensive, evaluation-only executable that mimics ransomware-like file behaviors inside an explicitly bounded workspace. It contains no network access, no stealth, no persistence, and no self-propagation.

## Safety Boundaries

The simulator refuses to run unless the target workspace folder name is exactly one of:
- `simulator-workspace`
- `test-workspace`

Any other path causes an immediate abort with a clear console error. This check is enforced by `SimulatorRunner.IsWorkspaceSafe()` and is case-insensitive.

## Workspace Reset (Rerun Safety Fix)

Before every run, the simulator calls `SimulatorRunner.ResetWorkspace()` which:
1. Deletes all files and subdirectories **inside** the workspace (not the root itself).
2. Recreates the root directory.
3. Prints how many files were removed, so the operator can see that a reset occurred.

This was implemented to fix a rerun crash:
```
System.IO.IOException: Cannot create a file when that file already exists.
```
The crash occurred on `File.Move(file, file + ".locked")` when `.locked` files from a previous run still existed. The reset approach is the cleanest solution — it makes each run deterministic and idempotent.

## CLI Usage

```
ActDefend.Simulator --benign     <workspace-path> [options]
ActDefend.Simulator --ransomware <workspace-path> [options]
```

`workspace-path` **must** be named `simulator-workspace` or `test-workspace`.

### Options

| Flag | Description | Benign default | Ransomware default |
|---|---|---|---|
| `--delay-ms <ms>` | Delay between file operations | 500 | 10 |
| `--file-count <n>` | Number of files to create | 5 | 30 |
| `--dir-depth <d>` | Directory tree depth for spread | 1 | 3 |

### Examples

```powershell
# Benign workload (validates no false-positive alerts)
dotnet run --project src\Detector.Simulator -- --benign .\simulator-workspace

# Ransomware workload (validates detection triggers)
dotnet run --project src\Detector.Simulator -- --ransomware .\simulator-workspace --file-count 50 --delay-ms 0 --dir-depth 5
```

## Workloads

### Benign (`--benign`)
- Writes low-entropy UTF-8 text files at a controlled rate.
- Designed to produce **no alerts** — validates true-negative behavior.

### Ransomware (`--ransomware`)
- **Phase 1:** Creates N victim `.txt` files spread across a nested directory tree.
- Pauses briefly (1 second) to let the ETW collector establish a baseline.
- **Phase 2:** Overwrites each file with 8 KiB of random (high-entropy) bytes, then immediately renames it to `.locked`.
- This triggers all five Stage 1 signals: WriteRate, UniqueFilesWritten, RenameRate, DirectorySpread, WriteReadRatio.

## Code Structure

| File | Role |
|---|---|
| `Program.cs` | CLI entry point — parses args, prints output |
| `SimulatorRunner.cs` | Pure logic — testable static class with no console I/O |

`SimulatorRunner` is separated from `Program` specifically so the logic can be covered by unit tests in `Detector.UnitTests`.

## Tests

`tests/Detector.UnitTests/Simulator/SimulatorRunnerTests.cs` covers:
- Safety boundary acceptance and rejection cases
- Workspace reset clears files and subdirectories
- Ransomware workload runs twice without IOException
- Ransomware workload runs 5× consecutively without crash
- Post-run state: only `.locked` files remain, no `.txt` originals
- Directory spread creates subdirectories
- Benign workload produces only plain `.txt` files
