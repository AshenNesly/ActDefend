using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using ActDefend.Core.Interfaces;
using ActDefend.Core.Models;

namespace ActDefend.GUI;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IMonitoringStatus _status;
    private readonly IAlertPublisher _publisher;
    private readonly IAlertRepository _alerts;

    public MainWindowViewModel(IMonitoringStatus status, IAlertPublisher publisher, IAlertRepository alerts)
    {
        _status = status;
        _publisher = publisher;
        _alerts = alerts;

        _status.StatusChanged += (_, _) => Application.Current.Dispatcher.Invoke(RefreshStatus);
        _publisher.AlertRaised += (_, alert) => Application.Current.Dispatcher.Invoke(() => SafeAddAlert(alert));

        // Load initially
        RefreshStatus();
        LoadHistoryAsync();
    }

    public ObservableCollection<DetectionAlert> RecentAlerts { get; } = new();

    public bool IsElevated => _status.IsElevated;
    public string ElevationText => IsElevated ? "Administrator" : "Not Elevated ⚠";

    public bool IsCollectorRunning => _status.IsCollectorRunning;
    public string CollectorText => IsCollectorRunning ? "Running ●" : "Stopped";

    public string EventsProcessed => _status.TotalEventsProcessed.ToString("N0");
    public string TrackedProcesses => _status.ActiveProcessCount.ToString("N0");

    private async void LoadHistoryAsync()
    {
        try
        {
            var history = await _alerts.GetRecentAsync(50);
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var alert in history)
                {
                    RecentAlerts.Add(alert);
                }
            });
        }
        catch 
        {
            // Transient loading error on SQLite block.
        }
    }

    private void RefreshStatus()
    {
        OnPropertyChanged(nameof(IsElevated));
        OnPropertyChanged(nameof(ElevationText));
        OnPropertyChanged(nameof(IsCollectorRunning));
        OnPropertyChanged(nameof(CollectorText));
        OnPropertyChanged(nameof(EventsProcessed));
        OnPropertyChanged(nameof(TrackedProcesses));
    }

    private void SafeAddAlert(DetectionAlert alert)
    {
        RecentAlerts.Insert(0, alert);
        if (RecentAlerts.Count > 100)
        {
            RecentAlerts.RemoveAt(RecentAlerts.Count - 1);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
