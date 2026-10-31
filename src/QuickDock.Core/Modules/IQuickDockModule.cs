namespace QuickDock.Core.Modules;

/// <summary>
/// Standard contract for all QuickDock modular components.
/// Modules follow lazy-load lifecycle semantics: "Core stays alive. Modules do not."
/// </summary>
public interface IQuickDockModule
{
    /// <summary>
    /// Unique identifier for the module (e.g. "quickdock.clock", "quickdock.clipboard").
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Brief description of the module capabilities.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Category classification of the module.
    /// </summary>
    ModuleCategory Category { get; }

    /// <summary>
    /// Current lifecycle state of the module.
    /// </summary>
    ModuleState State { get; }

    /// <summary>
    /// Indicates whether the module is enabled by user configuration.
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// Last error encountered by the module, if any.
    /// </summary>
    Exception? LastError { get; }

    /// <summary>
    /// Initializes necessary module resources, configuration, and event subscriptions.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts module operations when activated or requested by the user.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops module operations when deactivated or idle to conserve system resources.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
