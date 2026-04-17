using ActDefend.Core.Models;

namespace ActDefend.Core.Interfaces;

/// <summary>
/// Published whenever a confirmed detection alert is raised.
/// GUI and tray components subscribe to this to display notifications.
/// </summary>
public interface IAlertPublisher
{
    /// <summary>Publishes a confirmed alert to all registered subscribers.</summary>
    void Publish(DetectionAlert alert);

    /// <summary>Subscribe to new alert notifications.</summary>
    event EventHandler<DetectionAlert> AlertRaised;
}

/// <summary>
/// Read-only view of the current monitoring system state.
/// Used by the GUI to display status without coupling to implementation.
/// </summary>
public interface IMonitoringStatus
{
    /// <summary>True when the ETW collector is running and events are flowing.</summary>
    bool IsCollectorRunning { get; }

    /// <summary>True when the process is running with Administrator privileges.</summary>
    bool IsElevated { get; }

    /// <summary>Number of active process contexts currently being tracked.</summary>
    int ActiveProcessCount { get; }

    /// <summary>Total events processed since startup.</summary>
    long TotalEventsProcessed { get; }

    /// <summary>Total events dropped since startup (backpressure indicator).</summary>
    long TotalEventsDropped { get; }

    /// <summary>UTC time the monitoring system started.</summary>
    DateTimeOffset? StartedAt { get; }

    /// <summary>Raised when any monitored status property changes.</summary>
    event EventHandler StatusChanged;
}
