namespace QuickDock.Core.Settings;

/// <summary>
/// Provides strongly-typed persistence and mutation for QuickDock settings.
/// </summary>
public interface ISettingsManager
{
    /// <summary>
    /// The currently active settings snapshot in memory.
    /// </summary>
    QuickDockSettings CurrentSettings { get; }

    /// <summary>
    /// Loads settings from persistent storage, or initializes defaults if not found.
    /// </summary>
    Task<QuickDockSettings> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists settings to disk atomically and publishes a SettingsChangedEvent.
    /// </summary>
    Task SaveAsync(QuickDockSettings settings, string? changedSection = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets all settings to default values and saves them.
    /// </summary>
    Task ResetToDefaultsAsync(CancellationToken cancellationToken = default);
}
