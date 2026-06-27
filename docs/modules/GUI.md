# GUI Module (`Detector.GUI`)

**Phase 8 UX revision** — tray icon, severity-aware alert rows, and dashboard improvements.

## Architecture

The GUI layer uses **MVVM** cleanly separated into three files:

| File | Role |
|---|---|
| `MainWindow.xaml` | View — XAML layout only, no logic |
| `MainWindow.xaml.cs` | Code-behind — event wiring only (tray notification, close-to-tray) |
| `MainWindowViewModel.cs` | ViewModel — all data-binding logic |

Started and hosted by `WpfHostedService` (in `Detector.App`) on a dedicated STA thread (`ApartmentState.STA`) so the .NET Generic Host controls the application lifetime. `WpfHostedService.StopAsync` calls `App.Dispatcher.InvokeAsync(() => app.Shutdown())`, ensuring WPF shutdown is driven from the host cancellation token, not a window close event.

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

## Dashboard Tabs (Alerts, Allowlist & Settings)

The main content area uses a `TabControl` to split focus:

### Recent Alerts
Each alert row is wrapped in `AlertRowViewModel` which exposes:
- `SeverityBrush` — colour-coded left border (red/amber/grey)
- `SeverityLabel` — pill badge (CRITICAL / HIGH / MEDIUM / LOW)
- `ProcessName`, `PidText`, `Summary`, `TimestampText`
- **Trust this process** button — styled in positive green/teal. Prompts with a clear confirmation explaining that trusted processes are ignored by detection.

Alerts are prepended (newest first) and the list is capped at 100 entries.

### Allowlist (Trusted Processes)
A fully-featured allowlist manager:
- Header indicating warnings and implications of adding processes.
- An add form with Process Name and custom Notes/Reason input fields.
- Real-time search/filter bar to match process names.
- Complete tabular/list grid columns including: Process Name, Source (DefaultConfig, AlertAction, UserAdded), Date Added, Reason/Notes.
- Protected status marker badge for default system configuration entries.
- Action button "Remove" exclusively for user-added entries. Prompts with a confirmation dialog before deletion.

### Settings / Tuning
A safe, controlled configuration management panel:
- **Preset Profile Buttons** — Balanced, Sensitive, Low Resource, Conservative. Each profile button populates all exposed form fields with a tested, validated set of values appropriate for that use case.
- **Profile Description** — Short description of the currently selected preset shown below the profile buttons.
- **Advanced Settings Grid** (two-column layout):
  - **Stage 1 Settings**: Suspicion Threshold, all six feature weights.
  - **Feature Windows**: Primary Window, Context Window, Emit Interval, Inactivity Expiry.
  - **Stage 2 Settings**: Entropy Threshold, Max Files To Sample, Min Confirm Files, Cooldown.
  - **Collector Settings**: Event Queue Capacity.
- **Footer Action Area**:
  - **Validation Error** — shown in red if any field violates the safe tuning ranges.
  - **Restart Required notice** — shown in green after a successful save.
  - **Reset to Defaults** button — resets all fields to Balanced profile values, with confirmation.
  - **Save Settings** button — validates, writes to `appsettings.json`, and notifies the user that a restart is required.

All settings fields are bound to `SettingsViewModel` which is a singleton injected into `MainWindowViewModel.Settings`.

## Close-to-Tray and Exit Behaviour

- **Closing the Window**: Clicking the 'X' title bar button hides the window to the tray and keeps background monitoring active.
- **Tray Icon Context Menu**: Right-clicking the tray icon brings up a menu with:
  - **Open Dashboard**: Restores the window to view.
  - **Exit ActDefend**: Asks for confirmation, stops background monitoring gracefully by signaling `IHostApplicationLifetime.StopApplication()`, disposes the tray icon, and fully shuts down the process.
- **App Startup Configuration**: WPF `ShutdownMode` is set to `OnExplicitShutdown` to prevent application termination when the main window is hidden.

## Live Counter Refresh (Phase 8 Fix)

**Root cause fixed:** `Events Processed`, `Tracked Processes`, `Events Dropped`, and `Uptime` were displaying `0` in the dashboard even while alerts were being raised and events were flowing. Two separate issues:

1. `MonitoringStatusService.SetActiveProcessCount()` did not call `RaiseChanged()`, so `StatusChanged` was never fired during normal operation — only on collector start/stop transitions.
2. `MainWindowViewModel` only re-read counter values when `StatusChanged` fired. Since `IncrementEventsProcessed()` is called once per ETW event (too frequent to fire a UI event), there was no mechanism to refresh `EventsProcessed` between status-change events.

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

