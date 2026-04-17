using ActDefend.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
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
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Resolve the main window through DI so its dependencies are injected.
        var status     = _serviceProvider.GetRequiredService<IMonitoringStatus>();
        var mainWindow = new MainWindow(status);
        MainWindow     = mainWindow;
        mainWindow.Show();
    }
}
