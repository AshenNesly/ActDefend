using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using ActDefend.Core.Interfaces;
using ActDefend.Core.Models;

namespace ActDefend.GUI;

/// <summary>
/// ViewModel for MainWindow.
/// Bridges IMonitoringStatus and IAlertPublisher to WPF-bindable properties.
/// All property access is on the UI thread via Dispatcher.Invoke.
/// </summary>
public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IMonitoringStatus _status;
    private readonly IAlertPublisher   _publisher;
    private readonly IAlertRepository  _alerts;

    // Static brushes shared across all view-model instances
    private static readonly SolidColorBrush BrushSafe    = new(Color.FromRgb(0x27, 0xAE, 0x60)); // green
    private static readonly SolidColorBrush BrushWarn    = new(Color.FromRgb(0xF5, 0xA6, 0x23)); // amber
    private static readonly SolidColorBrush BrushDanger  = new(Color.FromRgb(0xE9, 0x45, 0x60)); // red
    private static readonly SolidColorBrush BrushCritical = new(Color.FromRgb(0xFF, 0x44, 0x44)); // bright red
    private static readonly SolidColorBrush BrushNeutral = new(Color.FromRgb(0x88, 0x88, 0x88)); // grey

    public MainWindowViewModel(IMonitoringStatus status, IAlertPublisher publisher, IAlertRepository alerts)
    {
        _status    = status;
        _publisher = publisher;
        _alerts    = alerts;

        _status.StatusChanged += (_, _) => Application.Current.Dispatcher.Invoke(RefreshStatus);
        _publisher.AlertRaised += (_, alert) => Application.Current.Dispatcher.Invoke(() => SafeAddAlert(alert));

        // Refresh live counters (Events Processed, Uptime) independently of status change events.
        // StatusChanged fires on collector state changes and every ~2 s via SetActiveProcessCount,
        // but EventsProcessed increments much faster than that. A 3-second timer ensures the
        // count display stays current without flooding the Dispatcher queue.
        var liveRefreshTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        liveRefreshTimer.Tick += (_, _) =>
        {
            OnPropertyChanged(nameof(EventsProcessed));
            OnPropertyChanged(nameof(EventsDropped));
            OnPropertyChanged(nameof(DroppedBrush));
            OnPropertyChanged(nameof(UptimeText));
            OnPropertyChanged(nameof(StatusBarText));
        };
        liveRefreshTimer.Start();

        RefreshStatus();
        LoadHistoryAsync();
    }

    // ── Alert collection ──────────────────────────────────────────────────────

    public ObservableCollection<AlertRowViewModel> RecentAlerts { get; } = new();

    public string AlertCountText => RecentAlerts.Count == 0
        ? "— no alerts —"
        : $"({RecentAlerts.Count} shown)";

    // ── Elevation ─────────────────────────────────────────────────────────────

    public bool   IsElevated     => _status.IsElevated;
    public string ElevationText  => IsElevated ? "Administrator" : "Not Elevated ⚠";
    public Brush  ElevationBrush => IsElevated ? BrushSafe : BrushDanger;

    // ── Collector ─────────────────────────────────────────────────────────────

    public bool   IsCollectorRunning => _status.IsCollectorRunning;
    public string CollectorText      => IsCollectorRunning ? "Running ●" : "Stopped ✕";
    public Brush  CollectorBrush     => IsCollectorRunning ? BrushSafe : BrushDanger;

    // ── Counters ──────────────────────────────────────────────────────────────

    public string EventsProcessed => _status.TotalEventsProcessed.ToString("N0");
    public string TrackedProcesses => _status.ActiveProcessCount.ToString("N0");

    public string EventsDropped => _status.TotalEventsDropped.ToString("N0");
    public Brush  DroppedBrush  => _status.TotalEventsDropped > 0 ? BrushWarn : BrushNeutral;

    // ── Uptime ────────────────────────────────────────────────────────────────

    public string UptimeText
    {
        get
        {
            if (_status.StartedAt is null) return "Not started";
            var elapsed = DateTimeOffset.UtcNow - _status.StartedAt.Value;
            return elapsed.TotalHours >= 1
                ? $"{(int)elapsed.TotalHours}h {elapsed.Minutes:D2}m"
                : $"{elapsed.Minutes}m {elapsed.Seconds:D2}s";
        }
    }

    // ── Status bar ────────────────────────────────────────────────────────────

    public string StatusBarText => IsCollectorRunning
        ? $"ActDefend v0.1 — Monitoring active  |  {EventsProcessed} events processed"
        : IsElevated
            ? "ActDefend v0.1 — Collector stopped"
            : "ActDefend v0.1 — Administrator privileges required for monitoring";

    // ── Private helpers ───────────────────────────────────────────────────────

    private void RefreshStatus()
    {
        OnPropertyChanged(nameof(IsElevated));
        OnPropertyChanged(nameof(ElevationText));
        OnPropertyChanged(nameof(ElevationBrush));
        OnPropertyChanged(nameof(IsCollectorRunning));
        OnPropertyChanged(nameof(CollectorText));
        OnPropertyChanged(nameof(CollectorBrush));
        OnPropertyChanged(nameof(EventsProcessed));
        OnPropertyChanged(nameof(TrackedProcesses));
        OnPropertyChanged(nameof(EventsDropped));
        OnPropertyChanged(nameof(DroppedBrush));
        OnPropertyChanged(nameof(UptimeText));
        OnPropertyChanged(nameof(StatusBarText));
    }

    private async void LoadHistoryAsync()
    {
        try
        {
            var history = await _alerts.GetRecentAsync(50);
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var alert in history)
                    RecentAlerts.Add(new AlertRowViewModel(alert));
            });
        }
        catch
        {
            // Transient load error; alerts will appear as they are raised live.
        }
    }

    private void SafeAddAlert(DetectionAlert alert)
    {
        RecentAlerts.Insert(0, new AlertRowViewModel(alert));
        if (RecentAlerts.Count > 100)
            RecentAlerts.RemoveAt(RecentAlerts.Count - 1);
        OnPropertyChanged(nameof(AlertCountText));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ── Static brush helpers ──────────────────────────────────────────────────

    internal static Brush BrushForSeverity(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Critical => BrushCritical,
        AlertSeverity.High     => BrushDanger,
        AlertSeverity.Medium   => BrushWarn,
        _                      => BrushNeutral,
    };
}

/// <summary>
/// Thin wrapper around a DetectionAlert that exposes UI-friendly formatted
/// properties for data binding inside the alert list.
/// </summary>
public sealed class AlertRowViewModel
{
    private readonly DetectionAlert _alert;

    public AlertRowViewModel(DetectionAlert alert) => _alert = alert;

    public string ProcessName  => _alert.ProcessName;
    public string Summary      => _alert.Summary;
    public string PidText      => $"PID {_alert.ProcessId}";
    public string TimestampText => _alert.Timestamp.LocalDateTime.ToString("HH:mm:ss");

    public string SeverityLabel => _alert.Severity.ToString().ToUpperInvariant();

    public Brush SeverityBrush => MainWindowViewModel.BrushForSeverity(_alert.Severity);
}
