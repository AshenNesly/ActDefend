using ActDefend.Core.Interfaces;
using System.Windows;
using System.Windows.Media;

namespace ActDefend.GUI;

/// <summary>
/// MainWindow code-behind — Phase 1 skeleton.
/// Binds UI elements to IMonitoringStatus via a periodic refresh timer.
///
/// Phase 6 will replace the timer-based refresh with proper MVVM:
/// - MainWindowViewModel bound via DataContext
/// - INotifyPropertyChanged / ObservableCollection
/// - Commands for alert acknowledgement, trusted-process management
/// - Tray icon with NotifyIcon
/// </summary>
public partial class MainWindow : Window
{
    private readonly IMonitoringStatus _status;
    private System.Windows.Threading.DispatcherTimer? _refreshTimer;

    public MainWindow(IMonitoringStatus status)
    {
        _status = status;
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshStatus();

        // Refresh UI every 2 seconds.
        _refreshTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _refreshTimer.Tick += (_, _) => RefreshStatus();
        _refreshTimer.Start();
    }

    private void RefreshStatus()
    {
        // Elevation
        if (_status.IsElevated)
        {
            ElevationStatus.Text       = "Administrator";
            ElevationStatus.Foreground = (SolidColorBrush)FindResource("SafeBrush");
        }
        else
        {
            ElevationStatus.Text       = "Not Elevated ⚠";
            ElevationStatus.Foreground = (SolidColorBrush)FindResource("DangerBrush");
        }

        // Collector
        if (_status.IsCollectorRunning)
        {
            CollectorStatus.Text       = "Running ●";
            CollectorStatus.Foreground = (SolidColorBrush)FindResource("SafeBrush");
        }
        else
        {
            CollectorStatus.Text       = "Stopped";
            CollectorStatus.Foreground = (SolidColorBrush)FindResource("DangerBrush");
        }

        EventsProcessed.Text  = _status.TotalEventsProcessed.ToString("N0");
        TrackedProcesses.Text = _status.ActiveProcessCount.ToString("N0");
    }

    protected override void OnClosed(EventArgs e)
    {
        _refreshTimer?.Stop();
        base.OnClosed(e);
    }
}
