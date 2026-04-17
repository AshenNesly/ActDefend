using ActDefend.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Windows;

namespace ActDefend.GUI;

/// <summary>
/// IHostedService that owns the WPF Application lifetime.
/// Starts the WPF message pump on a dedicated STA thread and integrates
/// WPF shutdown with the generic host's cancellation token.
///
/// The service provider is captured so the WPF App can resolve MainWindow
/// through DI rather than constructing it directly — this keeps the window
/// testable and free of hard service-locator anti-patterns.
///
/// Phase 6 will add tray icon setup and minimize-to-tray logic here.
/// </summary>
public sealed class WpfHostedService : IHostedService
{
    private readonly ILogger<WpfHostedService>  _logger;
    private readonly IServiceProvider           _serviceProvider;
    private readonly IHostApplicationLifetime   _lifetime;
    private Thread? _uiThread;
    private App?    _wpfApp;

    public WpfHostedService(
        ILogger<WpfHostedService> logger,
        IServiceProvider          serviceProvider,
        IHostApplicationLifetime  lifetime)
    {
        _logger          = logger;
        _serviceProvider = serviceProvider;
        _lifetime        = lifetime;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("WpfHostedService starting WPF shell.");

        _uiThread = new Thread(RunWpf)
        {
            IsBackground = true,
            Name         = "WPF-UI-Thread"
        };
        _uiThread.SetApartmentState(ApartmentState.STA);
        _uiThread.Start();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("WpfHostedService stopping WPF shell.");
        _wpfApp?.Dispatcher.InvokeAsync(() => _wpfApp.Shutdown());
        return Task.CompletedTask;
    }

    private void RunWpf()
    {
        _wpfApp = new App(_serviceProvider);
        _wpfApp.InitializeComponent();

        _wpfApp.Exit += (_, _) =>
        {
            _logger.LogInformation("WPF application exited — stopping host.");
            _lifetime.StopApplication();
        };

        _wpfApp.Run();
    }
}
