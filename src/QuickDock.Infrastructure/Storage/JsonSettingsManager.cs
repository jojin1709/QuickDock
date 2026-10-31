using System.Text.Json;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Events;
using QuickDock.Core.Settings;

namespace QuickDock.Infrastructure.Storage;

/// <summary>
/// Thread-safe, atomic JSON persistence implementation for <see cref="ISettingsManager"/>.
/// </summary>
public sealed class JsonSettingsManager : ISettingsManager
{
    private readonly string _filePath;
    private readonly IEventBus _eventBus;
    private readonly ILogger<JsonSettingsManager> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private QuickDockSettings _currentSettings;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public QuickDockSettings CurrentSettings => _currentSettings;

    public JsonSettingsManager(IEventBus eventBus, ILogger<JsonSettingsManager> logger, string? customFilePath = null)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _filePath = customFilePath ?? AppStoragePaths.ConfigFile;
        _currentSettings = new QuickDockSettings();
    }

    public async Task<QuickDockSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_filePath))
            {
                _logger.LogInformation("Configuration file not found at {Path}. Initializing with defaults.", _filePath);
                _currentSettings = new QuickDockSettings();
                await SaveInternalAsync(_currentSettings, cancellationToken).ConfigureAwait(false);
                return _currentSettings;
            }

            var json = await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);
            var loaded = JsonSerializer.Deserialize<QuickDockSettings>(json, JsonOptions);

            _currentSettings = loaded ?? new QuickDockSettings();
            _logger.LogInformation("Configuration loaded successfully from {Path}.", _filePath);
            return _currentSettings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load configuration from {Path}. Falling back to safe defaults.", _filePath);
            _currentSettings = new QuickDockSettings();
            return _currentSettings;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(QuickDockSettings settings, string? changedSection = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SaveInternalAsync(settings, cancellationToken).ConfigureAwait(false);
            _currentSettings = settings;
        }
        finally
        {
            _lock.Release();
        }

        await _eventBus.PublishAsync(new SettingsChangedEvent(settings, changedSection), cancellationToken).ConfigureAwait(false);
    }

    public async Task ResetToDefaultsAsync(CancellationToken cancellationToken = default)
    {
        var defaultSettings = new QuickDockSettings();
        await SaveAsync(defaultSettings, "Reset", cancellationToken).ConfigureAwait(false);
    }

    public async Task ResetSectionAsync(string sectionName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);
        var settings = _currentSettings;
        var defaults = new QuickDockSettings();

        switch (sectionName.Trim().ToLowerInvariant())
        {
            case "appearance":
                settings.Appearance = defaults.Appearance;
                break;
            case "dock":
            case "behavior":
                settings.Behavior = defaults.Behavior;
                break;
            case "general":
                settings.General = defaults.General;
                break;
            case "modules":
                settings.EnabledModules = new Dictionary<string, bool>(defaults.EnabledModules, StringComparer.OrdinalIgnoreCase);
                break;
            case "shortcuts":
                settings.Shortcuts = defaults.Shortcuts;
                break;
            case "notifications":
                settings.Notifications = defaults.Notifications;
                break;
            case "privacy":
                settings.Privacy = defaults.Privacy;
                break;
            case "network":
                settings.Network = defaults.Network;
                break;
            case "developer":
                settings.Developer = defaults.Developer;
                break;
            case "accessibility":
                settings.Accessibility = defaults.Accessibility;
                break;
            case "all":
                settings = defaults;
                break;
            default:
                _logger.LogWarning("Unknown settings section to reset: {Section}", sectionName);
                return;
        }

        await SaveAsync(settings, sectionName, cancellationToken).ConfigureAwait(false);
    }

    public Task<string> ExportSettingsJsonAsync(CancellationToken cancellationToken = default)
    {
        // Clone current settings to prevent mutation
        var serialized = JsonSerializer.Serialize(_currentSettings, JsonOptions);
        var clone = JsonSerializer.Deserialize<QuickDockSettings>(serialized, JsonOptions) ?? new QuickDockSettings();

        // Security boundary: sanitize and strip sensitive credentials
        clone.Weather.ApiKey = string.Empty;
        if (clone.CustomModuleConfig.Count > 0)
        {
            var keysToRemove = clone.CustomModuleConfig.Keys
                .Where(k => k.Contains("token", StringComparison.OrdinalIgnoreCase) ||
                            k.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
                            k.Contains("key", StringComparison.OrdinalIgnoreCase) ||
                            k.Contains("password", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var k in keysToRemove)
            {
                clone.CustomModuleConfig.Remove(k);
            }
        }

        var exportContainer = new
        {
            schemaVersion = 1,
            application = "QuickDock",
            exportedAtUtc = DateTime.UtcNow,
            settings = clone
        };

        var json = JsonSerializer.Serialize(exportContainer, JsonOptions);
        return Task.FromResult(json);
    }

    public async Task<SettingsImportResult> ImportSettingsJsonAsync(string json, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new SettingsImportResult(false, "Import failed: Configuration payload is empty.");
        }

        if (json.Length > 5 * 1024 * 1024)
        {
            return new SettingsImportResult(false, "Import failed: Configuration file exceeds maximum 5 MB threshold.");
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Validate schema version
            if (root.TryGetProperty("schemaVersion", out var versionElement))
            {
                int version = versionElement.GetInt32();
                if (version != 1)
                {
                    return new SettingsImportResult(false, $"Import failed: Unsupported schema version {version}. Expected version 1.");
                }
            }

            QuickDockSettings? importedSettings = null;
            if (root.TryGetProperty("settings", out var settingsElement))
            {
                importedSettings = JsonSerializer.Deserialize<QuickDockSettings>(settingsElement.GetRawText(), JsonOptions);
            }
            else
            {
                // Fallback direct root deserialize
                importedSettings = JsonSerializer.Deserialize<QuickDockSettings>(json, JsonOptions);
            }

            if (importedSettings == null)
            {
                return new SettingsImportResult(false, "Import failed: Unable to deserialize valid QuickDock settings.");
            }

            // Clamping and boundary validation
            importedSettings.Behavior.HoverDelayMs = Math.Clamp(importedSettings.Behavior.HoverDelayMs, 50, 2000);
            importedSettings.Appearance.Opacity = Math.Clamp(importedSettings.Appearance.Opacity, 0.2, 1.0);
            importedSettings.Accessibility.TextScalePercent = Math.Clamp(importedSettings.Accessibility.TextScalePercent, 100, 200);
            importedSettings.Network.RequestTimeoutSeconds = Math.Clamp(importedSettings.Network.RequestTimeoutSeconds, 5, 120);
            importedSettings.Network.DnsTimeoutSeconds = Math.Clamp(importedSettings.Network.DnsTimeoutSeconds, 1, 30);
            importedSettings.Network.HttpTimeoutSeconds = Math.Clamp(importedSettings.Network.HttpTimeoutSeconds, 2, 60);
            importedSettings.Network.TlsTimeoutSeconds = Math.Clamp(importedSettings.Network.TlsTimeoutSeconds, 2, 30);
            importedSettings.Developer.ProcessMonitorRefreshRateSeconds = Math.Clamp(importedSettings.Developer.ProcessMonitorRefreshRateSeconds, 2, 30);

            // Strip sensitive credentials on import
            importedSettings.Weather.ApiKey = string.Empty;

            // Save validated settings
            await SaveAsync(importedSettings, "Import", cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Successfully imported and validated QuickDock configuration.");

            return new SettingsImportResult(true, null, importedSettings.EnabledModules.Count);
        }
        catch (JsonException jex)
        {
            _logger.LogWarning(jex, "Failed to parse imported JSON settings.");
            return new SettingsImportResult(false, $"Import failed: Malformed JSON syntax. {jex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error importing settings.");
            return new SettingsImportResult(false, $"Import failed: {ex.Message}");
        }
    }

    private async Task SaveInternalAsync(QuickDockSettings settings, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempFilePath = _filePath + $".{Guid.NewGuid():N}.tmp";
        var json = JsonSerializer.Serialize(settings, JsonOptions);

        await File.WriteAllTextAsync(tempFilePath, json, cancellationToken).ConfigureAwait(false);

        // Atomic file replacement to prevent corrupt files on sudden crash
        File.Move(tempFilePath, _filePath, overwrite: true);
        _logger.LogDebug("Configuration saved atomically to {Path}.", _filePath);
    }
}

