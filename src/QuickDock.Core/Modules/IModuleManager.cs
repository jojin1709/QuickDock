namespace QuickDock.Core.Modules;

/// <summary>
/// Orchestrates module lifecycle, lazy activation, and crash isolation.
/// Prevents one broken module from terminating QuickDock.
/// </summary>
public interface IModuleManager
{
    /// <summary>
    /// Gets all registered modules.
    /// </summary>
    IReadOnlyList<IQuickDockModule> Modules { get; }

    /// <summary>
    /// Retrieves a specific module by its unique identifier.
    /// </summary>
    IQuickDockModule? GetModule(string moduleId);

    /// <summary>
    /// Registers a module with the manager.
    /// </summary>
    void RegisterModule(IQuickDockModule module);

    /// <summary>
    /// Initializes all registered modules that are currently enabled.
    /// </summary>
    Task InitializeAllEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a specific module safely with crash isolation.
    /// </summary>
    Task<bool> StartModuleAsync(string moduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops a specific module safely.
    /// </summary>
    Task<bool> StopModuleAsync(string moduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops all running modules (used for idle or application shutdown).
    /// </summary>
    Task StopAllRunningAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables a module and initializes it if needed.
    /// </summary>
    Task EnableModuleAsync(string moduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disables a module, stopping it first if active.
    /// </summary>
    Task DisableModuleAsync(string moduleId, CancellationToken cancellationToken = default);
}
