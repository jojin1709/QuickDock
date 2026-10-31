using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using QuickDock.Core.Events;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Modules;
using QuickDock.Core.Settings;
using QuickDock.Infrastructure.Storage;
using QuickDock.Infrastructure.Windows;
using QuickDock.Services.Storage;
using QuickDock.Services.System;
using Xunit;

namespace QuickDock.Core.Tests;

public class SettingsPersonalizationTests
{
    private sealed class TestEventBus : IEventBus
    {
        public List<object> PublishedEvents { get; } = [];

        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : class, IEvent
        {
            PublishedEvents.Add(@event);
            return Task.CompletedTask;
        }

        public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler) where TEvent : class, IEvent
        {
            return new DummyDisposable();
        }

        private sealed class DummyDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }

    private sealed class TestMonitorService : IMonitorService
    {
        public event EventHandler? MonitorsChanged
        {
            add { }
            remove { }
        }
        public MonitorInfo GetActiveMonitor() => new("MON1", "Display 1", true, 0, 0, 1920, 1080, 0, 0, 1920, 1040, 96, 1.0);
        public MonitorInfo GetPrimaryMonitor() => GetActiveMonitor();
        public MonitorInfo GetTargetMonitor(string? targetId, bool followActive = true) => GetActiveMonitor();
        public IReadOnlyList<MonitorInfo> GetAllMonitors() => [GetActiveMonitor()];
    }

    private static (JsonSettingsManager Manager, string TempPath, TestEventBus Bus) CreateTestManager()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"quickdock_test_{Guid.NewGuid():N}.json");
        var bus = new TestEventBus();
        var manager = new JsonSettingsManager(bus, NullLogger<JsonSettingsManager>.Instance, tempFile);
        return (manager, tempFile, bus);
    }

    [Fact]
    public async Task Settings_LoadDefault_WhenFileDoesNotExist()
    {
        var (manager, path, _) = CreateTestManager();
        try
        {
            var settings = await manager.LoadAsync();
            Assert.NotNull(settings);
            Assert.Equal("Ctrl+Space", settings.Behavior.GlobalHotkey);
            Assert.Equal(AppTheme.System, settings.Appearance.Theme);
            Assert.Equal(DockPosition.Top, settings.Behavior.Position);
            Assert.True(File.Exists(path)); // Initial default file was created atomically
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Settings_SaveAndLoad_PreservesValues()
    {
        var (manager, path, bus) = CreateTestManager();
        try
        {
            var settings = await manager.LoadAsync();
            settings.Appearance.Theme = AppTheme.Dark;
            settings.Appearance.AccentName = "Purple";
            settings.Appearance.AccentColor = "#8764B8";
            settings.Behavior.Position = DockPosition.Bottom;
            settings.Behavior.Size = DockSize.Large;
            settings.Notifications.Enabled = false;
            settings.Network.RequestTimeoutSeconds = 45;
            settings.Developer.LogLevel = "Debug";

            await manager.SaveAsync(settings, "Appearance");

            var reloaded = await manager.LoadAsync();
            Assert.Equal(AppTheme.Dark, reloaded.Appearance.Theme);
            Assert.Equal("Purple", reloaded.Appearance.AccentName);
            Assert.Equal(DockPosition.Bottom, reloaded.Behavior.Position);
            Assert.Equal(DockSize.Large, reloaded.Behavior.Size);
            Assert.False(reloaded.Notifications.Enabled);
            Assert.Equal(45, reloaded.Network.RequestTimeoutSeconds);
            Assert.Equal("Debug", reloaded.Developer.LogLevel);

            Assert.Contains(bus.PublishedEvents, e => e is SettingsChangedEvent sce && sce.ChangedSection == "Appearance");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Settings_CorruptedFile_FallsBackToDefaultsGracefully()
    {
        var (manager, path, _) = CreateTestManager();
        try
        {
            await File.WriteAllTextAsync(path, "{\"this is invalid json: definitely broken");
            var loaded = await manager.LoadAsync();
            Assert.NotNull(loaded);
            Assert.Equal(AppTheme.System, loaded.Appearance.Theme);
            Assert.Equal("Ctrl+Space", loaded.Behavior.GlobalHotkey);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Settings_ResetSection_ResetsOnlySpecifiedCategory()
    {
        var (manager, path, _) = CreateTestManager();
        try
        {
            var s = await manager.LoadAsync();
            s.Appearance.Theme = AppTheme.Light;
            s.Appearance.AccentName = "Green";
            s.Network.RequestTimeoutSeconds = 90;
            await manager.SaveAsync(s);

            await manager.ResetSectionAsync("Appearance");

            var reloaded = await manager.LoadAsync();
            // Appearance restored to default
            Assert.Equal(AppTheme.System, reloaded.Appearance.Theme);
            Assert.Equal("Blue", reloaded.Appearance.AccentName);
            // Network remains untouched
            Assert.Equal(90, reloaded.Network.RequestTimeoutSeconds);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Settings_ResetToDefaults_RestoresEverything()
    {
        var (manager, path, _) = CreateTestManager();
        try
        {
            var s = await manager.LoadAsync();
            s.Behavior.Position = DockPosition.Bottom;
            s.Notifications.Enabled = false;
            await manager.SaveAsync(s);

            await manager.ResetToDefaultsAsync();

            var reloaded = await manager.LoadAsync();
            Assert.Equal(DockPosition.Top, reloaded.Behavior.Position);
            Assert.True(reloaded.Notifications.Enabled);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Settings_Export_ExcludesSensitiveFields()
    {
        var (manager, path, _) = CreateTestManager();
        try
        {
            var s = await manager.LoadAsync();
            s.Weather.ApiKey = "SECRET_SUPER_API_KEY_12345";
            s.CustomModuleConfig["api_token"] = "PRIVATE_TOKEN_XYZ";
            s.CustomModuleConfig["safe_label"] = "MyDockWorkspace";
            await manager.SaveAsync(s);

            string exportedJson = await manager.ExportSettingsJsonAsync();
            Assert.NotNull(exportedJson);
            Assert.DoesNotContain("SECRET_SUPER_API_KEY_12345", exportedJson);
            Assert.DoesNotContain("PRIVATE_TOKEN_XYZ", exportedJson);
            Assert.Contains("MyDockWorkspace", exportedJson);
            Assert.Contains("\"schemaVersion\": 1", exportedJson);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Settings_Import_ValidConfiguration_SucceedsAndApplies()
    {
        var (manager, path, _) = CreateTestManager();
        try
        {
            string payload = """
            {
              "schemaVersion": 1,
              "application": "QuickDock",
              "settings": {
                "Appearance": {
                  "Theme": 2,
                  "AccentName": "Orange",
                  "AccentColor": "#CA5010"
                },
                "Behavior": {
                  "Position": 1,
                  "Size": 2,
                  "HoverDelayMs": 300
                },
                "Network": {
                  "RequestTimeoutSeconds": 60
                }
              }
            }
            """;

            var result = await manager.ImportSettingsJsonAsync(payload);
            Assert.True(result.Success);
            Assert.Null(result.ErrorMessage);

            var s = await manager.LoadAsync();
            Assert.Equal(AppTheme.Dark, s.Appearance.Theme);
            Assert.Equal("Orange", s.Appearance.AccentName);
            Assert.Equal(DockPosition.Bottom, s.Behavior.Position);
            Assert.Equal(DockSize.Large, s.Behavior.Size);
            Assert.Equal(300, s.Behavior.HoverDelayMs);
            Assert.Equal(60, s.Network.RequestTimeoutSeconds);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Settings_Import_RejectsInvalidSchemaVersion()
    {
        var (manager, path, _) = CreateTestManager();
        try
        {
            string payload = """
            {
              "schemaVersion": 999,
              "settings": {}
            }
            """;

            var result = await manager.ImportSettingsJsonAsync(payload);
            Assert.False(result.Success);
            Assert.Contains("Unsupported schema version", result.ErrorMessage);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Settings_Import_RejectsMalformedJson()
    {
        var (manager, path, _) = CreateTestManager();
        try
        {
            var result = await manager.ImportSettingsJsonAsync("{ not valid json at all");
            Assert.False(result.Success);
            Assert.Contains("Malformed JSON syntax", result.ErrorMessage);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Settings_Import_RejectsExcessivelyLargePayload()
    {
        var (manager, path, _) = CreateTestManager();
        try
        {
            string huge = new string('x', 6 * 1024 * 1024); // 6 MB
            var result = await manager.ImportSettingsJsonAsync(huge);
            Assert.False(result.Success);
            Assert.Contains("threshold", result.ErrorMessage);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Settings_Import_ClampsExtremeNumericValues()
    {
        var (manager, path, _) = CreateTestManager();
        try
        {
            string payload = """
            {
              "schemaVersion": 1,
              "settings": {
                "Behavior": {
                  "HoverDelayMs": 999999
                },
                "Network": {
                  "RequestTimeoutSeconds": 10000,
                  "DnsTimeoutSeconds": -50
                }
              }
            }
            """;

            var result = await manager.ImportSettingsJsonAsync(payload);
            Assert.True(result.Success);

            var s = await manager.LoadAsync();
            Assert.Equal(2000, s.Behavior.HoverDelayMs); // clamped to max 2000
            Assert.Equal(120, s.Network.RequestTimeoutSeconds); // clamped to max 120
            Assert.Equal(1, s.Network.DnsTimeoutSeconds); // clamped to min 1
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void WindowPositioning_CalculatesTopAndBottomCorrectly()
    {
        var (manager, path, _) = CreateTestManager();
        try
        {
            var positioning = new WindowPositioningService(new TestMonitorService(), manager, NullLogger<WindowPositioningService>.Instance);

            // Default Top
            manager.CurrentSettings.Behavior.Position = DockPosition.Top;
            var topPlacement = positioning.CalculatePlacement(isExpanded: false, workX: 0, workY: 0, workWidth: 1920, workHeight: 1040, scaleFactor: 1.0, topMargin: 12);
            Assert.Equal(12, topPlacement.Y);

            // Bottom
            manager.CurrentSettings.Behavior.Position = DockPosition.Bottom;
            var bottomPlacement = positioning.CalculatePlacement(isExpanded: false, workX: 0, workY: 0, workWidth: 1920, workHeight: 1040, scaleFactor: 1.0, topMargin: 12);
            int expectedBottomY = 1040 - topPlacement.Height - 12;
            Assert.Equal(expectedBottomY, bottomPlacement.Y);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void WindowPositioning_CalculatesSizeScalingCorrectly()
    {
        var (manager, path, _) = CreateTestManager();
        try
        {
            var positioning = new WindowPositioningService(new TestMonitorService(), manager, NullLogger<WindowPositioningService>.Instance);

            manager.CurrentSettings.Behavior.Size = DockSize.Normal;
            var normalDim = positioning.GetDockDimensions(isExpanded: false, scaleFactor: 1.0);
            Assert.Equal(WindowPositioningService.BaseIdleWidth, normalDim.Width);

            manager.CurrentSettings.Behavior.Size = DockSize.Compact;
            var compactDim = positioning.GetDockDimensions(isExpanded: false, scaleFactor: 1.0);
            Assert.True(compactDim.Width < normalDim.Width);

            manager.CurrentSettings.Behavior.Size = DockSize.Large;
            var largeDim = positioning.GetDockDimensions(isExpanded: false, scaleFactor: 1.0);
            Assert.True(largeDim.Width > normalDim.Width);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void ShortcutManager_ReturnsInitialShortcuts_AndDetectsConflicts()
    {
        var (manager, path, _) = CreateTestManager();
        try
        {
            var service = new ShortcutManagerService(manager, NullLogger<ShortcutManagerService>.Instance);
            var list = service.GetShortcuts();

            Assert.NotEmpty(list);
            Assert.Contains(list, s => s.Id == "dock.toggle" && s.CurrentKeyGesture == "Ctrl+Space");

            // Valid update
            var updateRes = service.UpdateShortcut("timer.new", "Ctrl+Shift+A");
            Assert.True(updateRes.Success);
            Assert.Equal("Ctrl+Shift+A", manager.CurrentSettings.Shortcuts.NewTimer);

            // Conflict detection: Attempt to assign Ctrl+Space (already dock.toggle) to notes.open
            var conflictRes = service.UpdateShortcut("notes.open", "Ctrl+Space");
            Assert.False(conflictRes.Success);
            Assert.Contains("Conflict detected", conflictRes.ErrorMessage);

            // Reset to defaults
            service.ResetToDefaults();
            Assert.Equal("Ctrl+Shift+T", manager.CurrentSettings.Shortcuts.NewTimer);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void ShortcutManager_RejectsInvalidKeyCombinations()
    {
        var (manager, path, _) = CreateTestManager();
        try
        {
            var service = new ShortcutManagerService(manager, NullLogger<ShortcutManagerService>.Instance);

            var emptyRes = service.UpdateShortcut("timer.new", "");
            Assert.False(emptyRes.Success);

            var noModifierRes = service.UpdateShortcut("timer.new", "K");
            Assert.False(noModifierRes.Success);
            Assert.Contains("Invalid shortcut format", noModifierRes.ErrorMessage);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task StorageManager_CalculatesBreakdown_AndClearsSafely()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"quickdock_stor_test_{Guid.NewGuid():N}.db");
        var db = new SqliteDatabaseService(NullLogger<SqliteDatabaseService>.Instance, tempDb);
        await db.InitializeAsync();
        try
        {
            var storage = new StorageManagerService(db, NullLogger<StorageManagerService>.Instance);

            var breakdown = await storage.GetStorageBreakdownAsync();
            Assert.NotNull(breakdown);
            Assert.NotNull(breakdown.FormattedTotalSize);

            await storage.ClearCacheAsync();
            await storage.ClearLogsAsync();
            await storage.ClearTemporaryDataAsync();
            await storage.ClearProductivityDataAsync();
        }
        finally
        {
            db.Dispose();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try
            {
                if (File.Exists(tempDb)) File.Delete(tempDb);
            }
            catch
            {
                // Best effort cleanup in tests
            }
        }
    }

    [Fact]
    public void NotificationService_SuppressesWhenDisabled()
    {
        var (manager, path, _) = CreateTestManager();
        try
        {
            var service = new NotificationService(NullLogger<NotificationService>.Instance, manager);

            manager.CurrentSettings.Notifications.Enabled = false;
            // Should not throw and should safely suppress
            service.ShowNotification("Timer Alert", "Timer has expired.");

            manager.CurrentSettings.Notifications.Enabled = true;
            manager.CurrentSettings.Notifications.TimerNotifications = false;
            // Suppresses timer category
            service.ShowNotification("Timer Alert", "Timer has expired.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task ModuleManager_EnableAndDisable_RespectsLifecycle()
    {
        var bus = new TestEventBus();
        var moduleManager = new ModuleManager(bus, NullLogger<ModuleManager>.Instance);

        // Create a test module
        var testMod = new TestLifecycleModule("test.dummy", "Test Module", ModuleCategory.Utilities);
        moduleManager.RegisterModule(testMod);

        Assert.Equal(ModuleState.Uninitialized, testMod.State);

        await moduleManager.EnableModuleAsync("test.dummy");
        Assert.True(testMod.IsEnabled);

        await moduleManager.StartModuleAsync("test.dummy");
        Assert.Equal(ModuleState.Running, testMod.State);

        await moduleManager.DisableModuleAsync("test.dummy");
        Assert.False(testMod.IsEnabled);
        Assert.Equal(ModuleState.Stopped, testMod.State);
    }

    private sealed class TestLifecycleModule : ModuleBase
    {
        public override string Id { get; }
        public override string Name { get; }
        public override string Description => "Test description";
        public override ModuleCategory Category { get; }

        public TestLifecycleModule(string id, string name, ModuleCategory category)
            : base(NullLogger<TestLifecycleModule>.Instance)
        {
            Id = id;
            Name = name;
            Category = category;
        }

        protected override Task OnInitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        protected override Task OnStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        protected override Task OnStopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
