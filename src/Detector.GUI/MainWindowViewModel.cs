using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
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
    private readonly IMonitoringStatus         _status;
    private readonly IAlertPublisher           _publisher;
    private readonly IAlertRepository          _alerts;
    private readonly ITrustedProcessRepository _trustedProcesses;

    // Static brushes shared across all view-model instances
    private static readonly SolidColorBrush BrushSafe     = new(Color.FromRgb(0x27, 0xAE, 0x60)); // green
    private static readonly SolidColorBrush BrushWarn     = new(Color.FromRgb(0xF5, 0xA6, 0x23)); // amber
    private static readonly SolidColorBrush BrushDanger   = new(Color.FromRgb(0xE9, 0x45, 0x60)); // red
    private static readonly SolidColorBrush BrushCritical = new(Color.FromRgb(0xFF, 0x44, 0x44)); // bright red
    private static readonly SolidColorBrush BrushNeutral  = new(Color.FromRgb(0x88, 0x88, 0x88)); // grey

    public SettingsViewModel Settings { get; }

    public MainWindowViewModel(
        IMonitoringStatus         status,
        IAlertPublisher           publisher,
        IAlertRepository          alerts,
        ITrustedProcessRepository trustedProcesses,
        SettingsViewModel         settings)
    {
        _status           = status;
        _publisher        = publisher;
        _alerts           = alerts;
        _trustedProcesses = trustedProcesses;
        Settings          = settings;

        _status.StatusChanged  += (_, _) => Application.Current.Dispatcher.Invoke(RefreshStatus);
        _publisher.AlertRaised += (_, alert) => Application.Current.Dispatcher.Invoke(() => SafeAddAlert(alert));

        AllowProcessFromAlertCommand = new RelayCommand(ExecuteAllowProcessFromAlert);
        RemoveAllowlistEntryCommand  = new RelayCommand(ExecuteRemoveAllowlistEntry);
        AddAllowlistEntryCommand     = new RelayCommand(ExecuteAddAllowlistEntry);

        // Refresh live counters every 3 seconds — EventsProcessed increments faster than StatusChanged fires
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
        _ = LoadAllowlistAsync();
    }

    // ── Alert collection ──────────────────────────────────────────────────────

    public ObservableCollection<AlertRowViewModel> RecentAlerts { get; } = new();

    public string AlertCountText => RecentAlerts.Count == 0
        ? "— no alerts —"
        : $"({RecentAlerts.Count} shown)";

    // ── Allowlist collection ──────────────────────────────────────────────────

    /// <summary>Full list loaded from the repository (default + user-added).</summary>
    private readonly ObservableCollection<TrustedProcessRowViewModel> _allowlistAll = new();

    /// <summary>Filtered view shown in the UI — updated whenever filter text changes.</summary>
    public ObservableCollection<TrustedProcessRowViewModel> Allowlist { get; } = new();

    public ICommand AllowProcessFromAlertCommand { get; }
    public ICommand RemoveAllowlistEntryCommand  { get; }
    public ICommand AddAllowlistEntryCommand     { get; }

    // ── Allowlist form fields ─────────────────────────────────────────────────

    private string _newAllowlistProcessName = string.Empty;
    public string NewAllowlistProcessName
    {
        get => _newAllowlistProcessName;
        set { _newAllowlistProcessName = value; OnPropertyChanged(); }
    }



    private string _allowlistFilterText = string.Empty;
    public string AllowlistFilterText
    {
        get => _allowlistFilterText;
        set
        {
            _allowlistFilterText = value;
            OnPropertyChanged();
            ApplyAllowlistFilter();
        }
    }

    // ── Allowlist commands ────────────────────────────────────────────────────

    private async void ExecuteAllowProcessFromAlert(object? parameter)
    {
        if (parameter is not AlertRowViewModel alertRow) return;

        var result = MessageBox.Show(
            $"Are you sure you want to trust '{alertRow.ProcessName}'?\n\n" +
            "Trusted processes will be ignored by ActDefend. Only allowlist applications you fully trust.",
            "Allow Process",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        // Prevent duplicate from alert
        if (!_allowlistAll.Any(e => e.ProcessName.Equals(alertRow.ProcessName, StringComparison.OrdinalIgnoreCase)))
        {
            var entry = new TrustedProcessEntry
            {
                EntryId     = Guid.NewGuid(),
                ProcessName = alertRow.ProcessName,
                CreatedAt   = DateTimeOffset.UtcNow,
                Reason      = "User allowed from alert dashboard.",
                Source      = "AlertAction",
                IsDefault   = false
            };

            await _trustedProcesses.AddAsync(entry);
            await LoadAllowlistAsync();
        }

        // Issue 1: Recent alert rows should disappear after trusting a process
        var alertsToRemove = RecentAlerts
            .Where(a => a.ProcessName.Equals(alertRow.ProcessName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var a in alertsToRemove)
        {
            RecentAlerts.Remove(a);
        }
        
        OnPropertyChanged(nameof(AlertCountText));
    }

    private async void ExecuteRemoveAllowlistEntry(object? parameter)
    {
        if (parameter is not TrustedProcessRowViewModel row) return;
        if (!row.CanRemove) return; // extra guard — default entries are protected

        var result = MessageBox.Show(
            $"Remove '{row.ProcessName}' from the trusted processes list?\n\n" +
            "The process will be monitored by the detection pipeline again after this change.",
            "Remove Trusted Process",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        await _trustedProcesses.RemoveAsync(row.EntryId);
        // Reload from source rather than manipulating the collection directly,
        // to ensure the in-memory repository and UI are in sync.
        await LoadAllowlistAsync();
    }

    private async void ExecuteAddAllowlistEntry(object? parameter)
    {
        var name = NewAllowlistProcessName?.Trim();
        if (string.IsNullOrEmpty(name)) return;

        // Check for duplicates
        if (_allowlistAll.Any(e => e.ProcessName.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show($"The process '{name}' is already in the trusted list.", "Duplicate Entry", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var entry = new TrustedProcessEntry
        {
            EntryId     = Guid.NewGuid(),
            ProcessName = name,
            CreatedAt   = DateTimeOffset.UtcNow,
            Reason      = "User manually added via dashboard.",
            Source      = "UserAdded",
            IsDefault   = false
        };

        await _trustedProcesses.AddAsync(entry);
        NewAllowlistProcessName = string.Empty;
        await LoadAllowlistAsync();
    }

    private async Task LoadAllowlistAsync()
    {
        try
        {
            var list = await _trustedProcesses.GetAllAsync();
            Application.Current.Dispatcher.Invoke(() =>
            {
                _allowlistAll.Clear();
                foreach (var item in list)
                    _allowlistAll.Add(new TrustedProcessRowViewModel(item));
                ApplyAllowlistFilter();
            });
        }
        catch { /* transient error — list will refresh next call */ }
    }

    private void ApplyAllowlistFilter()
    {
        var filter = _allowlistFilterText.Trim();
        Allowlist.Clear();
        foreach (var row in _allowlistAll)
        {
            if (string.IsNullOrEmpty(filter) ||
                row.ProcessName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                Allowlist.Add(row);
            }
        }
        OnPropertyChanged(nameof(AllowlistEmptyMessage));
    }

    /// <summary>Returns empty-state message text when no entries match the current filter.</summary>
    public string AllowlistEmptyMessage
    {
        get
        {
            if (_allowlistAll.Count == 0)
                return "No trusted processes configured. Use the Add form above to allowlist a process.";
            if (Allowlist.Count == 0)
                return $"No processes match \"{_allowlistFilterText}\".";
            return string.Empty;
        }
    }

    public bool AllowlistIsEmpty => Allowlist.Count == 0;

    // ── Elevation ─────────────────────────────────────────────────────────────

    public bool   IsElevated     => _status.IsElevated;
    public string ElevationText  => IsElevated ? "Administrator" : "Not Elevated ⚠";
    public Brush  ElevationBrush => IsElevated ? BrushSafe : BrushDanger;

    // ── Collector ─────────────────────────────────────────────────────────────

    public bool   IsCollectorRunning => _status.IsCollectorRunning;
    public string CollectorText      => IsCollectorRunning ? "Running ●" : "Stopped ✕";
    public Brush  CollectorBrush     => IsCollectorRunning ? BrushSafe : BrushDanger;

    // ── Counters ──────────────────────────────────────────────────────────────

    public string EventsProcessed  => _status.TotalEventsProcessed.ToString("N0");
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
            // Transient load error — alerts will appear as they are raised live.
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

    public string ProcessName   => _alert.ProcessName;
    public string Summary       => _alert.Summary;
    public string PidText       => $"PID {_alert.ProcessId}";
    public string TimestampText => _alert.Timestamp.LocalDateTime.ToString("HH:mm:ss");
    public string SeverityLabel => _alert.Severity.ToString().ToUpperInvariant();
    public Brush  SeverityBrush => MainWindowViewModel.BrushForSeverity(_alert.Severity);

    public string EvidenceSummary
    {
        get
        {
            var latency = _alert.DetectionLatencyMs > 0 ? $"{_alert.DetectionLatencyMs:F0}ms" : "N/A";
            var hef = _alert.HighEntropyFileCount;
            var reasons = string.IsNullOrEmpty(_alert.Stage1TopReasons) ? "N/A" : _alert.Stage1TopReasons;
            return $"Latency={latency} | HighEntropyFiles={hef} | Reasons={reasons}";
        }
    }
}

/// <summary>
/// Row view-model for an entry in the Trusted Processes / Allowlist tab.
/// </summary>
public sealed class TrustedProcessRowViewModel
{
    private readonly TrustedProcessEntry _entry;

    public TrustedProcessRowViewModel(TrustedProcessEntry entry) => _entry = entry;

    public Guid   EntryId       => _entry.EntryId;
    public string ProcessName   => _entry.ProcessName ?? _entry.ProcessPath ?? "Unknown";
    public string ProcessPath   => _entry.ProcessPath ?? "—";
    public string Source        => _entry.Source;
    public string Reason        => string.IsNullOrWhiteSpace(_entry.Reason) ? "—" : _entry.Reason;
    public string TimestampText => _entry.CreatedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm");

    /// <summary>True when this entry can be removed (user-added; not a protected default).</summary>
    public bool CanRemove => !_entry.IsDefault;

    /// <summary>Label displayed instead of the Remove button for protected default entries.</summary>
    public string ProtectedLabel => _entry.IsDefault ? "PROTECTED" : string.Empty;
}
