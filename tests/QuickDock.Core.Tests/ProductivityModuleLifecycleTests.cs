using Microsoft.Extensions.Logging.Abstractions;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;
using QuickDock.Core.Modules;
using QuickDock.Infrastructure.Storage;
using QuickDock.Modules.Productivity;
using QuickDock.Services.Clipboard;
using QuickDock.Services.Productivity;
using QuickDock.Services.ScreenTime;
using QuickDock.Services.Time;
using QuickDock.Services.Weather;
using Xunit;

namespace QuickDock.Core.Tests;

public class MockClipboardService : IClipboardService
{
#pragma warning disable CS0067
    public event EventHandler<ClipboardItem>? ClipboardItemAdded;
    public event EventHandler? ClipboardCleared;
#pragma warning restore CS0067
    public bool IsPaused { get; set; }
    public bool IsListening { get; private set; }

    public Task StartListeningAsync()
    {
        IsListening = true;
        return Task.CompletedTask;
    }

    public void StopListening()
    {
        IsListening = false;
    }

    public Task ProcessCurrentClipboardAsync() => Task.CompletedTask;

    public Task<List<ClipboardItem>> GetHistoryAsync(string? search = null, ClipboardContentType? filterType = null) => Task.FromResult(new List<ClipboardItem>());
    public Task CopyItemToClipboardAsync(ClipboardItem item) => Task.CompletedTask;
    public Task SetPinnedAsync(string id, bool isPinned) => Task.CompletedTask;
    public Task DeleteItemAsync(string id) => Task.CompletedTask;
    public Task ClearHistoryAsync(bool clearPinned = false) => Task.CompletedTask;
    public void Dispose() => StopListening();
}

public class ProductivityModuleLifecycleTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteDatabaseService _db;

    public ProductivityModuleLifecycleTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"quickdock_test_life_{Guid.NewGuid():N}.db");
        _db = new SqliteDatabaseService(NullLogger<SqliteDatabaseService>.Instance, _testDbPath);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }

    [Fact]
    public async Task ProductivityModules_LifecycleTransitions_AreSafe()
    {
        var settings = new MockSettingsManager();
        var notif = new MockNotificationService();

        var clipService = new MockClipboardService();
        var timerService = new TimerService(notif, settings, NullLogger<TimerService>.Instance);
        var stopwatchService = new StopwatchService();
        var pomodoroService = new PomodoroService(notif, settings, NullLogger<PomodoroService>.Instance);
        var notesService = new NotesService(_db, NullLogger<NotesService>.Instance);
        var calService = new CalendarService(_db, NullLogger<CalendarService>.Instance);
        var weatherService = new WeatherService(new MockWeatherProvider(), settings, NullLogger<WeatherService>.Instance);
        var linksService = new SavedLinksService(_db, NullLogger<SavedLinksService>.Instance);
        var tempMsgService = new TemporaryMessageService(_db, NullLogger<TemporaryMessageService>.Instance);
        var screenService = new ScreenTimeService(_db, settings, NullLogger<ScreenTimeService>.Instance);

        var modules = new IQuickDockModule[]
        {
            new ClipboardModule(clipService, NullLogger<ClipboardModule>.Instance),
            new TimerModule(timerService, stopwatchService, pomodoroService, NullLogger<TimerModule>.Instance),
            new NotesModule(notesService, NullLogger<NotesModule>.Instance),
            new CalendarModule(calService, NullLogger<CalendarModule>.Instance),
            new WeatherModule(weatherService, NullLogger<WeatherModule>.Instance),
            new LinksModule(linksService, NullLogger<LinksModule>.Instance),
            new TemporaryMessageModule(tempMsgService, NullLogger<TemporaryMessageModule>.Instance),
            new ScreenTimeModule(screenService, NullLogger<ScreenTimeModule>.Instance)
        };

        foreach (var mod in modules)
        {
            Assert.Equal(ModuleState.Uninitialized, mod.State);

            await mod.InitializeAsync(CancellationToken.None);
            Assert.Equal(ModuleState.Initialized, mod.State);

            await mod.StartAsync(CancellationToken.None);
            Assert.Equal(ModuleState.Running, mod.State);

            // Redundant start is idempotent
            await mod.StartAsync(CancellationToken.None);
            Assert.Equal(ModuleState.Running, mod.State);

            await mod.StopAsync(CancellationToken.None);
            Assert.Equal(ModuleState.Stopped, mod.State);

            // Redundant stop is safe
            await mod.StopAsync(CancellationToken.None);
            Assert.Equal(ModuleState.Stopped, mod.State);
        }

        Assert.True(clipService.IsListening);
    }
}
