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
