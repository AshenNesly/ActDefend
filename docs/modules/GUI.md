# GUI Module (WPF Dashboard & Tray)

The `Detector.GUI` project serves as the end-user facing front-end for ActDefend, projecting runtime detection events up from the kernel into human-readable alerts seamlessly.

## Architecture

To ensure cross-compatibility with strict .NET patterns, ActDefend utilizes an **MVVM (Model-View-ViewModel)** structure replacing legacy explicit event bindings in the underlying Code-Behind `MainWindow.xaml.cs`.

### The View Model (`MainWindowViewModel.cs`)
The VM directly requests injected `IMonitoringStatus` objects to pull active `TotalEventsProcessed` and connection strings without explicitly calling heavy native locks on arbitrary intervals. 

Instead: It binds to the explicit action `StatusChanged`, generating `OnPropertyChanged` execution paths keeping UI loops absolutely minimal.

### Alert Output Handling
The `Alerts` system receives outputs via `IAlertPublisher.AlertRaised`.
When malicious files pop Stage 2 Confirmation flags:
1. `MainWindowViewModel` pushes the JSON entity onto an `ObservableCollection`.
2. The `ItemsControl` natively bounds inside `MainWindow.xaml` generates a dynamic alert row.

### System Tray Overrides
Activating `Hardcodet.NotifyIcon.Wpf`, closing the ActDefend monitor screen explicitly terminates standard user bounds (`e.Cancel = true;`) overriding them to `Hide()`. This allows EDR software to naturally slide into a passive Tray-bound background icon. If the machine triggers an internal `AlertRaised` while minimized, the Tray produces native balloon pops mimicking Windows Action Center.
