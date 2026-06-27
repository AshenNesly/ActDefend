using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ActDefend.Core.Interfaces;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.Hosting;

namespace ActDefend.GUI;

public partial class MainWindow : Window
{
    private readonly IAlertPublisher           _publisher;
    private readonly IHostApplicationLifetime  _appLifetime;

    /// <summary>
    /// When true the OnClosing handler allows the window to actually close
    /// (triggered by "Exit ActDefend" from the tray menu).
    /// When false, closing the window hides it to tray instead.
    /// </summary>
    private bool _isExiting;

    public MainWindow(
        IMonitoringStatus         status,
        IAlertPublisher           publisher,
        IAlertRepository          alerts,
        ITrustedProcessRepository trustedProcesses,
        SettingsViewModel         settings,
        IHostApplicationLifetime  appLifetime)
    {
        InitializeComponent();

        _publisher   = publisher;
        _appLifetime = appLifetime;

        // Hook MVVM Context exclusively
        DataContext = new MainWindowViewModel(status, publisher, alerts, trustedProcesses, settings);

        // Trap publisher alerts and route them to tray balloon notifications
        _publisher.AlertRaised += OnAlertRaised;
        
        Serilog.Log.Information("MainWindow constructed successfully.");
        
        this.Loaded += (s, e) => Serilog.Log.Information("MainWindow Loaded event fired. Window is now visible.");
        this.Closing += (s, e) => Serilog.Log.Information("MainWindow Closing event fired.");
        this.Closed += (s, e) => Serilog.Log.Information("MainWindow Closed event fired.");
    }

    // ── Tray balloon notifications ────────────────────────────────────────────

    private void OnAlertRaised(object? sender, Core.Models.DetectionAlert alert)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var title = alert.Severity switch
            {
                Core.Models.AlertSeverity.Critical => "⚠ CRITICAL — Ransomware Detected",
                Core.Models.AlertSeverity.High     => "⚠ HIGH — Suspicious Activity",
                Core.Models.AlertSeverity.Medium   => "⚑ MEDIUM — Elevated Activity",
                _                                  => "ℹ LOW — Suspicious Signal"
            };

            TaskbarIcon.ShowBalloonTip(
                title,
                $"{alert.ProcessName} (PID {alert.ProcessId})\n{alert.Summary}",
                Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Warning);
        });
    }

    // ── Tray context menu handlers ────────────────────────────────────────────

    /// <summary>
    /// Tray menu "Open Dashboard" — restores the window from tray.
    /// Also wired to double-click on the tray icon.
    /// </summary>
    private void TrayMenuOpenDashboard_Click(object sender, RoutedEventArgs e)
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    /// <summary>
    /// Tray right-click — explicitly shows the context menu at the mouse cursor
    /// to fix WPF ContextMenu placement issues.
    /// </summary>
    private void TaskbarIcon_TrayRightMouseUp(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint };
        
        var openItem = new MenuItem { Header = "Open Dashboard", FontWeight = FontWeights.Bold };
        openItem.Click += TrayMenuOpenDashboard_Click;
        
        var exitItem = new MenuItem { Header = "Exit ActDefend" };
        exitItem.Click += TrayMenuExit_Click;
        
        menu.Items.Add(openItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(exitItem);

        menu.IsOpen = true;
    }

    /// <summary>
    /// Tray icon double-click — same as Open Dashboard.
    /// </summary>
    private void TaskbarIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
    {
        TrayMenuOpenDashboard_Click(sender, e);
    }

    /// <summary>
    /// Tray menu "Exit ActDefend" — confirms, then performs a graceful full shutdown.
    /// </summary>
    private void TrayMenuExit_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to exit ActDefend?\n\nMonitoring will stop and the application will close completely.",
            "Exit ActDefend",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        // Signal the .NET Generic Host to stop (stops all IHostedServices including detection pipeline)
        _isExiting = true;
        
        // Ensure tray icon is disposed so it doesn't linger after exit
        TaskbarIcon?.Dispose();

        _appLifetime.StopApplication();

        // Also ask WPF to shut down so the UI message loop exits cleanly
        Application.Current.Shutdown();
    }

    // ── Window lifetime ───────────────────────────────────────────────────────

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_isExiting)
        {
            // Full exit — allow the window to close normally
            base.OnClosing(e);
            return;
        }

        // Close-to-tray: cancel the close and hide the window instead
        e.Cancel = true;
        Hide();

        TaskbarIcon.ShowBalloonTip(
            "ActDefend Running",
            "Monitoring continues in the background. Right-click the tray icon to exit.",
            BalloonIcon.Info);

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _publisher.AlertRaised -= OnAlertRaised;
        TaskbarIcon?.Dispose();

        base.OnClosed(e);
    }
    
    // ── Settings ──────────────────────────────────────────────────────────────

    private void SettingsRestartButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to restart ActDefend to apply new settings?\n\nMonitoring will be temporarily stopped.",
            "Restart ActDefend",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try 
        {
            var processPath = System.Environment.ProcessPath;
            if (!string.IsNullOrEmpty(processPath))
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = processPath,
                    UseShellExecute = true,
                    Verb = "runas"
                };
                System.Diagnostics.Process.Start(psi);
            }
            else 
            {
                throw new Exception("Could not determine executable path.");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not restart automatically. Please close and reopen ActDefend manually.\n\nError: {ex.Message}", "Restart Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Proceed with graceful exit
        _isExiting = true;
        TaskbarIcon?.Dispose();
        _appLifetime.StopApplication();
        Application.Current.Shutdown();
    }
}
