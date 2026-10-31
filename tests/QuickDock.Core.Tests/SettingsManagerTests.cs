using Microsoft.Extensions.Logging.Abstractions;
using QuickDock.Core.Events;
using QuickDock.Core.Settings;
using QuickDock.Infrastructure.Storage;
using Xunit;

namespace QuickDock.Core.Tests;

public class SettingsManagerTests : IDisposable
{
    private readonly string _tempConfigDir;
    private readonly string _tempConfigFile;

    public SettingsManagerTests()
    {
        _tempConfigDir = Path.Combine(Path.GetTempPath(), $"QuickDock_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempConfigDir);
        _tempConfigFile = Path.Combine(_tempConfigDir, "test_config.json");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempConfigDir))
            {
                Directory.Delete(_tempConfigDir, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public async Task LoadAsync_WhenFileMissing_CreatesAndReturnsDefaults()
    {
        var bus = new InMemoryEventBus();
        var manager = new JsonSettingsManager(bus, NullLogger<JsonSettingsManager>.Instance, _tempConfigFile);

        var settings = await manager.LoadAsync();

        Assert.NotNull(settings);
        Assert.Equal("Ctrl+Space", settings.Behavior.GlobalHotkey);
        Assert.Equal(AppTheme.System, settings.Appearance.Theme);
        Assert.True(File.Exists(_tempConfigFile));
    }

    [Fact]
    public async Task SaveAsync_PersistsMutationsAtomically()
    {
        var bus = new InMemoryEventBus();
        var manager = new JsonSettingsManager(bus, NullLogger<JsonSettingsManager>.Instance, _tempConfigFile);

        var initial = await manager.LoadAsync();
        initial.Appearance.Theme = AppTheme.Dark;
        initial.Behavior.GlobalHotkey = "Alt+Space";
        initial.Privacy.ClipboardRetentionCount = 100;

        await manager.SaveAsync(initial);

        // Create a new instance to verify disk persistence
        var secondManager = new JsonSettingsManager(bus, NullLogger<JsonSettingsManager>.Instance, _tempConfigFile);
        var loaded = await secondManager.LoadAsync();

        Assert.Equal(AppTheme.Dark, loaded.Appearance.Theme);
        Assert.Equal("Alt+Space", loaded.Behavior.GlobalHotkey);
        Assert.Equal(100, loaded.Privacy.ClipboardRetentionCount);
    }

    [Fact]
    public async Task SaveAsync_FiresSettingsChangedEvent()
    {
        var bus = new InMemoryEventBus();
        var manager = new JsonSettingsManager(bus, NullLogger<JsonSettingsManager>.Instance, _tempConfigFile);

        SettingsChangedEvent? receivedEvent = null;
        using var sub = bus.Subscribe<SettingsChangedEvent>((e, _) =>
        {
            receivedEvent = e;
            return Task.CompletedTask;
        });

        var settings = await manager.LoadAsync();
        settings.Appearance.Theme = AppTheme.Light;

        await manager.SaveAsync(settings, "Appearance");

        Assert.NotNull(receivedEvent);
        Assert.Equal("Appearance", receivedEvent.ChangedSection);
        Assert.Equal(AppTheme.Light, receivedEvent.Settings.Appearance.Theme);
    }

    [Fact]
    public async Task ResetToDefaultsAsync_RestoresDefaults()
    {
        var bus = new InMemoryEventBus();
        var manager = new JsonSettingsManager(bus, NullLogger<JsonSettingsManager>.Instance, _tempConfigFile);

        var settings = await manager.LoadAsync();
        settings.Behavior.GlobalHotkey = "Win+K";
        await manager.SaveAsync(settings);

        await manager.ResetToDefaultsAsync();

        var restored = await manager.LoadAsync();
        Assert.Equal("Ctrl+Space", restored.Behavior.GlobalHotkey);
    }
}

