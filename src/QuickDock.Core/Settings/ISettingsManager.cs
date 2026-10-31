namespace QuickDock.Core.Settings;

public sealed record SettingsImportResult(bool Success, string? ErrorMessage = null, int ImportedKeysCount = 0);

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

    /// <summary>
    /// Resets a specific section of settings to its default values.
    /// </summary>
    Task ResetSectionAsync(string sectionName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports current non-sensitive settings to a serialized JSON string with schema metadata.
    /// </summary>
    Task<string> ExportSettingsJsonAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates and imports settings from a serialized JSON string, protecting against malformed or malicious data.
    /// </summary>
    Task<SettingsImportResult> ImportSettingsJsonAsync(string json, CancellationToken cancellationToken = default);
}

