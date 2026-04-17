# GUI Module (`Detector.GUI`)

**Phase 8 UX revision** — tray icon, severity-aware alert rows, and dashboard improvements.

## Architecture

The GUI layer uses **MVVM** cleanly separated into three files:

| File | Role |
|---|---|
| `MainWindow.xaml` | View — XAML layout only, no logic |
| `MainWindow.xaml.cs` | Code-behind — event wiring only (tray notification, close-to-tray) |
| `MainWindowViewModel.cs` | ViewModel — all data-binding logic |

Started and hosted by `WpfHostedService` (in `Detector.App`) on a dedicated STA thread so the generic host controls lifetime.

## Tray Icon

`shield.ico` is embedded as a WPF `<Resource>` inside `Detector.GUI.csproj` and referenced via a **pack URI**:

```xml
IconSource="/ActDefend.GUI;component/Images/shield.ico"
```

This guarantees the icon resource resolves from the assembly at runtime regardless of the working directory. It will be visible in the system tray whenever the application is running.

## Balloon Notifications

When `IAlertPublisher.AlertRaised` fires, `MainWindow.xaml.cs` invokes `TaskbarIcon.ShowBalloonTip` from the UI thread. The balloon title reflects severity:

| Severity | Balloon title |
|---|---|
| Critical | ⚠ CRITICAL — Ransomware Detected |
| High | ⚠ HIGH — Suspicious Activity |
| Medium | ⚑ MEDIUM — Elevated Activity |
| Low | ℹ LOW — Suspicious Signal |

Notifications work whether the window is visible or minimized to tray.

## Dashboard Status Panels

The left sidebar shows six live status cards, all bound to `MainWindowViewModel` properties:

| Panel | Property | Notes |
|---|---|---|
| ELEVATION | `ElevationText` / `ElevationBrush` | Green = Admin, Red = not elevated |
| COLLECTOR | `CollectorText` / `CollectorBrush` | Green = running, Red = stopped |
| EVENTS PROCESSED | `EventsProcessed` | Formatted with thousands separator |
| TRACKED PROCESSES | `TrackedProcesses` | Active ETW-tracked process count |
| EVENTS DROPPED | `EventsDropped` / `DroppedBrush` | Amber when non-zero (backpressure signal) |
| UPTIME | `UptimeText` | Derived from `IMonitoringStatus.StartedAt` |

## Alert Feed

Each alert row is wrapped in `AlertRowViewModel` which exposes:
- `SeverityBrush` — colour-coded left border (red/amber/grey)
- `SeverityLabel` — pill badge (CRITICAL / HIGH / MEDIUM / LOW)
- `ProcessName`, `PidText`, `Summary`, `TimestampText`

Alerts are prepended (newest first) and the list is capped at 100 entries.

## Close-to-Tray Behaviour

`OnClosing` is cancelled (`e.Cancel = true`) and the window is hidden. A balloon tip confirms monitoring continues in background. Double-clicking the tray icon restores the window.

## Live Counter Refresh (Phase 8 Fix)

**Root cause fixed:** `Events Processed`, `Tracked Processes`, `Events Dropped`, and `Uptime` were displaying `0` in the dashboard even while alerts were being raised and events were flowing. The cause was twofold:

1. `MonitoringStatusService.IncrementEventsProcessed()` and `SetActiveProcessCount()` never called `RaiseChanged()`, so `StatusChanged` was never fired for counter changes — only for collector start/stop.
2. `MainWindowViewModel` only re-read counter values on `StatusChanged`, so values were permanently stale after startup.

**Fixes applied:**

- `MonitoringStatusService.SetActiveProcessCount()` now calls `RaiseChanged()`. This fires every `~2 seconds` (on each orchestration tick), giving the VM a regular push to update `TrackedProcesses`.
- `MainWindowViewModel` now starts a `DispatcherTimer` (3-second interval) that manually raises `PropertyChanged` for `EventsProcessed`, `EventsDropped`, `DroppedBrush`, `UptimeText`, and `StatusBarText`. This covers high-frequency counters that change between status-change events.
- `PipelineHostService` now correctly propagates collector drop-count deltas to `MonitoringStatusService.IncrementEventsDropped()` on each tick, so `Events Dropped` in the UI reflects real backpressure.

**Counter refresh cadence after fix:**

| Counter | Refresh trigger |
|---|---|
| Events Processed | DispatcherTimer every 3 s |
| Tracked Processes | `StatusChanged` via `SetActiveProcessCount` every ~2 s |
| Events Dropped | `StatusChanged` (collector state) + DispatcherTimer every 3 s |
| Uptime | DispatcherTimer every 3 s |

