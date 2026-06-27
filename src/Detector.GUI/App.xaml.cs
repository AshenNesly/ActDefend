using ActDefend.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Windows;

namespace ActDefend.GUI;

/// <summary>
/// WPF Application class.
/// Does not use a built-in startup URI — the generic host controls startup
/// via WpfHostedService, which calls App.Run() directly.
///
/// Receives the DI service provider so it can resolve the MainWindow
/// (which has constructor dependencies) on the STA thread.
/// </summary>
public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;

    public App(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        
        // Catch any unhandled WPF/UI-thread exceptions that bypass Serilog
        DispatcherUnhandledException += (_, e) =>
        {
            Serilog.Log.Fatal(e.Exception, "Unhandled WPF Dispatcher exception: {Message}", e.Exception.Message);
            Serilog.Log.CloseAndFlush();
            try {
                System.Windows.MessageBox.Show(
                    $"ActDefend encountered a fatal error:\n\n{e.Exception.Message}\n\nDetails logged to the logs folder.",
                    "Fatal Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            } catch { }
            e.Handled = true;
            Shutdown(1);
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // Resolve the main window through DI so its dependencies are injected.
            var mainWindow = Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<MainWindow>(_serviceProvider);
            MainWindow     = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            Serilog.Log.Fatal(ex, "Fatal error creating MainWindow: {Message}", ex.Message);
            Serilog.Log.CloseAndFlush();
            try {
                System.Windows.MessageBox.Show(
                    $"ActDefend failed to open the dashboard:\n\n{ex.Message}\n\nInner: {ex.InnerException?.Message}\n\nDetails logged to the logs folder.",
                    "Startup Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            } catch { }
            Shutdown(1);
        }
    }
}
