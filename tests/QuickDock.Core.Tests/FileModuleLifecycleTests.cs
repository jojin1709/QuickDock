using Microsoft.Extensions.Logging.Abstractions;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;
using QuickDock.Core.Modules;
using QuickDock.Infrastructure.Storage;
using QuickDock.Modules.Files;
using QuickDock.Services.Files;
using QuickDock.Services.QuickDrop;
using Xunit;

namespace QuickDock.Core.Tests;

public class FileModuleLifecycleTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteDatabaseService _db;

    public FileModuleLifecycleTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"quickdock_lifecycle_file_{Guid.NewGuid():N}.db");
        _db = new SqliteDatabaseService(NullLogger<SqliteDatabaseService>.Instance, _testDbPath);
    }

    [Fact]
    public async Task AllFileModules_SupportFullLifecycle_AndAreIdempotent()
    {
        var shelfService = new FileShelfService(_db, NullLogger<FileShelfService>.Instance);
        var transferManager = new FileTransferManager(_db, NullLogger<FileTransferManager>.Instance);
        var notif = new MockNotificationService();
        var settingsMgr = new MockSettingsManager();
        var hashService = new FileHashService(NullLogger<FileHashService>.Instance);
        var converterService = new FileConversionService(NullLogger<FileConversionService>.Instance);

        var quickDropService = new QuickDropService(
            transferManager,
            notif,
            settingsMgr,
            hashService,
            NullLogger<QuickDropService>.Instance,
            NullLogger<QuickDropHttpServer>.Instance);

        var modules = new IQuickDockModule[]
        {
            new FileShelfModule(shelfService, NullLogger<FileShelfModule>.Instance),
            new QuickDropModule(quickDropService, NullLogger<QuickDropModule>.Instance),
            new FileConverterModule(converterService, NullLogger<FileConverterModule>.Instance),
            new TransferManagerModule(transferManager, NullLogger<TransferManagerModule>.Instance)
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
    }

    [Fact]
    public async Task QuickDropService_DuplicateStart_ReturnsExistingSession()
    {
        var transferManager = new FileTransferManager(_db, NullLogger<FileTransferManager>.Instance);
        var notif = new MockNotificationService();
        var settingsMgr = new MockSettingsManager();
        var hashService = new FileHashService(NullLogger<FileHashService>.Instance);

        using var quickDropService = new QuickDropService(
            transferManager,
            notif,
            settingsMgr,
            hashService,
            NullLogger<QuickDropService>.Instance,
            NullLogger<QuickDropHttpServer>.Instance);

        var session1 = await quickDropService.StartSessionAsync();
        Assert.NotNull(session1);
        Assert.True(quickDropService.IsRunning);

        // Duplicate start returns same session
        var session2 = await quickDropService.StartSessionAsync();
        Assert.Same(session1, session2);

        await quickDropService.StopSessionAsync();
        Assert.False(quickDropService.IsRunning);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }
}
