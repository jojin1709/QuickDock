namespace QuickDock.Core.Interfaces;

/// <summary>
/// Manages application-wide lifecycle orchestration, idle transitions, and graceful shutdown.
/// </summary>
public interface IAppLifecycleService
{
    /// <summary>
    /// Indicates whether the application is currently performing a graceful shutdown.
    /// </summary>
    bool IsShuttingDown { get; }

    /// <summary>
    /// Event triggered when application shutdown has been initiated.
    /// </summary>
    event EventHandler? ShutdownRequested;

    /// <summary>
    /// Coordinates startup initialization of minimal core systems.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Coordinates graceful shutdown: stops all active modules, flushes logs, and releases resources.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests application shutdown gracefully.
    /// </summary>
    void RequestShutdown();
}
