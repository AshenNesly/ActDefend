using System.ComponentModel;
using System.Windows;
using ActDefend.Core.Interfaces;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;

namespace ActDefend.GUI;

public partial class MainWindow : Window
{
    private readonly IAlertPublisher _publisher;

    public MainWindow(IMonitoringStatus status, IAlertPublisher publisher, IAlertRepository alerts)
    {
        InitializeComponent();
        
        _publisher = publisher;

        // Hook MVVM Context exclusively
        DataContext = new MainWindowViewModel(status, publisher, alerts);

        // Trap publisher native warnings routing them up into the Windows Action Center via Tray overrides
        _publisher.AlertRaised += OnAlertRaised;
    }

    private void OnAlertRaised(object? sender, Core.Models.DetectionAlert alert)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            TaskbarIcon.ShowBalloonTip(
                "Ransomware Detected",
                $"{alert.ProcessName} triggered an anomaly.\n{alert.Summary}",
                BalloonIcon.Warning);
        });
    }

    private void TaskbarIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Don't kill the monitoring kernel pipeline when clicking X. Send it down to the tray layer.
        e.Cancel = true;
        Hide();
        
        TaskbarIcon.ShowBalloonTip("ActDefend Running", "Monitoring running securely in the background.", BalloonIcon.Info);
        
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _publisher.AlertRaised -= OnAlertRaised;
        TaskbarIcon.Dispose();
        
        base.OnClosed(e);
    }
}
