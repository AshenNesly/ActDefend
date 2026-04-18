# User Guide

ActDefend runs as a background monitoring daemon with a WPF dashboard and a Windows system-tray icon. The monitoring pipeline starts automatically when the application launches (Administrator elevation required).

---

## Starting the Application

Launch `Detector.App.exe` (or `dotnet run --project src/Detector.App`). Because the application requests Administrator privileges through its app manifest, Windows will show a UAC prompt if you are not already running as Administrator.

Once elevated, the application starts the ETW session and the WPF dashboard opens. If elevation is denied, the dashboard appears but the COLLECTOR panel shows **Stopped** and no monitoring occurs.

---

## Dashboard Layout

The left sidebar shows six live status cards:

| Panel | Colour | Meaning |
|---|---|---|
| **ELEVATION** | 🟢 Green "Administrator" | Running as Administrator — ETW session can start. |
| **ELEVATION** | 🔴 Red "Not Elevated ⚠" | Not elevated — monitoring will not run. |
| **COLLECTOR** | 🟢 Green "Running ●" | ETW session is active and ingesting file events. |
| **COLLECTOR** | 🔴 Red "Stopped ✕" | ETW session failed or was not started. |
| **EVENTS PROCESSED** | (counter) | Total file I/O events ingested from ETW since the session started. |
| **TRACKED PROCESSES** | (counter) | Number of distinct PIDs currently tracked by the feature extractor. |
| **EVENTS DROPPED** | ⚪ Grey (0) / 🟡 Amber (> 0) | Events discarded because the internal channel was full. Amber indicates pipeline backpressure — consider reducing workload or increasing the channel capacity. |
| **UPTIME** | (timer) | Time since the ETW session started (format: `Xm YYs` or `Xh YYm`). |

Counters refresh automatically via a 3-second timer in the ViewModel. No manual refresh is required.

---

## Alert Feed

The central area shows confirmed detection alerts, newest at the top (maximum 100 rows displayed; capped in memory). Each row shows:
- **Severity badge** — colour-coded: CRITICAL (bright red), HIGH (red), MEDIUM (amber), LOW (grey).
- **Process name** — image name of the flagged process (e.g. `suspicious.exe`).
- **PID** — process ID at the time of detection.
- **Summary** — includes Stage 1 score and average entropy (e.g. `suspicious.exe (PID 4432) — S1=82.1 S2=confirmed AvgEntropy=7.91`).
- **Timestamp** — local time of the alert.

On startup, the last 50 persisted alerts are loaded from the SQLite database (`actdefend.db`), so the alert feed survives application restarts.

---

## Tray Icon

The shield icon in the Windows system notification area indicates that ActDefend is running. It is visible whether the window is open or minimised to tray.

### Close to Tray

Clicking the window's **X** button does **not** terminate the application. The window is hidden and a balloon notification appears confirming monitoring continues in the background. The ETW session and pipeline keep running.

To restore the window: **double-click the tray icon**.

To fully exit the application: right-click the tray icon (if configured) or terminate the process from Task Manager. Note that terminating the process without a clean shutdown may leave the `ActDefend-Monitor-Session` ETW session active; it will be cleaned up automatically on next launch.

---

## Tray Balloon Notifications

When a confirmed alert is raised, a Windows balloon notification appears:

| Severity | Balloon title |
|---|---|
| Critical | ⚠ CRITICAL — Ransomware Detected |
| High | ⚠ HIGH — Suspicious Activity |
| Medium | ⚑ MEDIUM — Elevated Activity |
| Low | ℹ LOW — Suspicious Signal |

The notification body shows the process name, PID, and summary. Notifications fire even if the dashboard window is hidden.

---

## Trusted Process Exclusions (Current Limitations)

The following system processes are excluded from scoring by default (loaded from `appsettings.json`):
`System`, `smss.exe`, `csrss.exe`, `wininit.exe`, `winlogon.exe`, `services.exe`, `lsass.exe`, `svchost.exe`, `MsMpEng.exe`, `SearchIndexer.exe`.

**Currently, there is no UI for managing the trusted-process list.** To add a custom exclusion:
1. Edit `appsettings.json` and add the process image name to `ActDefend.TrustedProcesses.DefaultExclusions`.
2. Restart the application.

Runtime additions via code (`TrustedProcessRepository.AddAsync`) are in-memory only and lost on restart.

---

## Reading the Logs

Structured JSON logs are written to the `logs/` directory beside the application. Each line is a JSON object with `@t` (timestamp), `@l` (level), `@m` (message), and structured properties. Useful for:
- Reviewing why a specific alert was raised (`Stage1Score`, `Explanation`).
- Checking for ETW session errors or dropped-event warnings.
- Verifying the pipeline is processing events (`TotalEventsProcessed` at Information level).

---

## Using the Simulator for Testing

Run `Detector.Simulator` from a separate terminal while `Detector.App` is running:

```powershell
# Triggers detection (ransomware workload)
dotnet run --project src\Detector.Simulator -- --ransomware .\simulator-workspace --file-count 30 --delay-ms 10 --dir-depth 3

# Validates no false alert (benign workload)
dotnet run --project src\Detector.Simulator -- --benign .\simulator-workspace
```

The simulator resets the workspace before each run. Watch the EVENTS PROCESSED counter climb and the alert feed for new entries. See [modules/Simulator.md](modules/Simulator.md) for full options.
