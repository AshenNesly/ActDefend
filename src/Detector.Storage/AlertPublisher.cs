using ActDefend.Core.Interfaces;
using ActDefend.Core.Models;
using Microsoft.Extensions.Logging;

namespace ActDefend.Storage;

/// <summary>
/// In-process alert publisher using a simple event.
/// Phase 6 may upgrade this to a more decoupled mechanism if needed.
/// Current design is sufficient for a single-process WPF desktop application.
/// </summary>
internal sealed class AlertPublisher : IAlertPublisher
{
    private readonly ILogger<AlertPublisher> _logger;

    public AlertPublisher(ILogger<AlertPublisher> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public event EventHandler<DetectionAlert>? AlertRaised;

    /// <inheritdoc />
    public void Publish(DetectionAlert alert)
    {
        _logger.LogWarning("Publishing alert {Id} for PID={Pid} ({Name}) Severity={Severity}",
            alert.AlertId, alert.ProcessId, alert.ProcessName, alert.Severity);
        AlertRaised?.Invoke(this, alert);
    }
}
