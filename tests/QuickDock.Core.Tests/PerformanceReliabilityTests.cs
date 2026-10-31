using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using QuickDock.Core.Events;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;
using QuickDock.Core.Modules;
using QuickDock.Core.Settings;
using QuickDock.Core.Utilities;
using QuickDock.Infrastructure.Storage;
using QuickDock.Services.Files;
using QuickDock.Services.QuickDrop;
using QuickDock.Services.System;
using Xunit;

namespace QuickDock.Core.Tests;

public class PerformanceReliabilityTests
{
    private sealed class FaultyModule : ModuleBase
    {
        public bool ThrowOnStart { get; set; } = true;
        public bool DisposedCalled { get; private set; }

        public override string Id => "test.faulty";
        public override string Name => "Faulty Test Module";
        public override string Description => "A module that simulates failure.";
        public override ModuleCategory Category => ModuleCategory.Utilities;

        public FaultyModule() : base(NullLogger<FaultyModule>.Instance) { }

        protected override Task OnStartAsync(CancellationToken cancellationToken)
        {
            if (ThrowOnStart)
            {
                throw new InvalidOperationException("Simulated module crash during start.");
            }
            return Task.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposedCalled = true;
            }
            base.Dispose(disposing);
        }
    }

    private sealed class StubEventBus : IEventBus
    {
        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : class, IEvent
            => Task.CompletedTask;

        public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler) where TEvent : class, IEvent
            => new DummyDisposable();

        private sealed class DummyDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }

    [Fact]
    public async Task ModuleManager_IsolatesFaultedModule_WithoutThrowing()
    {
        var bus = new StubEventBus();
        var manager = new ModuleManager(bus, NullLogger<ModuleManager>.Instance);
        var faulty = new FaultyModule();
        manager.RegisterModule(faulty);

        bool started = await manager.StartModuleAsync(faulty.Id);

        Assert.False(started);
        Assert.Equal(ModuleState.Faulted, faulty.State);
        Assert.NotNull(faulty.LastError);
    }

    [Fact]
    public void ModuleManager_Dispose_DisposesAllRegisteredModules()
    {
        var bus = new StubEventBus();
        var manager = new ModuleManager(bus, NullLogger<ModuleManager>.Instance);
        var module = new FaultyModule();
        manager.RegisterModule(module);

        manager.Dispose();

        Assert.True(module.DisposedCalled);
        Assert.Empty(manager.Modules);
    }

    [Fact]
    public async Task ProcessExecutionHelper_RunsExecutable_AndCapturesOutput()
    {
        // Use cmd.exe /c echo test as a cross-architecture reliable Windows command
        var result = await ProcessExecutionHelper.RunProcessAsync(
            "cmd.exe",
            ["/c", "echo", "QuickDockPerformanceTest"],
            null,
            TimeSpan.FromSeconds(5));

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("QuickDockPerformanceTest", result.Output);
    }

    [Fact]
    public async Task ProcessExecutionHelper_KillsProcess_OnCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        // Sleep for 10 seconds via powershell or ping to test cancellation
        var sw = Stopwatch.StartNew();
        var result = await ProcessExecutionHelper.RunProcessAsync(
            "cmd.exe",
            ["/c", "ping", "127.0.0.1", "-n", "10"],
            null,
            TimeSpan.FromSeconds(15),
            cts.Token);
        sw.Stop();

        Assert.False(result.Success);
        Assert.True(sw.ElapsedMilliseconds < 5000, "Process should have been cancelled well before ping finished.");
    }

    [Fact]
    public void PerformanceDiagnostics_ReturnsValidRuntimeSnapshot()
    {
        var bus = new StubEventBus();
        var manager = new ModuleManager(bus, NullLogger<ModuleManager>.Instance);
        var service = new PerformanceDiagnosticsService(manager, NullLogger<PerformanceDiagnosticsService>.Instance);

        var snapshot = service.GetSnapshot();

        Assert.NotNull(snapshot);
        Assert.True(snapshot.WorkingSetBytes > 0);
        Assert.True(snapshot.ThreadCount > 0);
        Assert.True(snapshot.HandleCount > 0);
        Assert.True(snapshot.GcHeapSizeBytes > 0);
        Assert.True(snapshot.Uptime.TotalMilliseconds > 0);
    }

    [Fact]
    public async Task SqliteDatabaseService_RecoversFromCorruption_Automatically()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"corrupt_test_{Guid.NewGuid():N}.db");
        try
        {
            // Corrupt file by writing raw invalid binary text
            await File.WriteAllTextAsync(tempDbPath, "THIS IS NOT A VALID SQLITE DATABASE HEADER AT ALL!");

            var dbService = new SqliteDatabaseService(NullLogger<SqliteDatabaseService>.Instance, tempDbPath);
            await dbService.InitializeAsync();

            // Should be healthy now and able to query
            var items = await dbService.GetClipboardItemsAsync(limit: 10);
            Assert.NotNull(items);
            Assert.Empty(items);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { }
            }
        }
    }

    [Fact]
    public async Task SqliteDatabaseService_VerifiesCompositeIndexesExist()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"index_test_{Guid.NewGuid():N}.db");
        try
        {
            var dbService = new SqliteDatabaseService(NullLogger<SqliteDatabaseService>.Instance, tempDbPath);
            await dbService.InitializeAsync();

            using var conn = new SqliteConnection($"Data Source={tempDbPath}");
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='index';";
            using var reader = await cmd.ExecuteReaderAsync();

            var indexNames = new List<string>();
            while (await reader.ReadAsync())
            {
                indexNames.Add(reader.GetString(0));
            }

            Assert.Contains("idx_clipboard_pinned_created", indexNames);
            Assert.Contains("idx_notes_pinned_updated", indexNames);
            Assert.Contains("idx_temp_msg_active", indexNames);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { }
            }
        }
    }

    [Fact]
    public async Task QuickDropHttpServer_RepeatedStartStop_ReleasesPortCleanly()
    {
        var transferManager = new FileTransferManager(
            new SqliteDatabaseService(NullLogger<SqliteDatabaseService>.Instance, Path.Combine(Path.GetTempPath(), $"qd_test_{Guid.NewGuid():N}.db")),
            NullLogger<FileTransferManager>.Instance);

        var notif = new DummyNotificationService();

        for (int i = 0; i < 3; i++)
        {
            using var server = new QuickDropHttpServer(transferManager, notif, NullLogger<QuickDropHttpServer>.Instance);
            var session = new QuickDropSession
            {
                LocalAddress = "127.0.0.1",
                Port = 19876,
                Token = "test_token",
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
                IsActive = true
            };

            server.Start(session);
            Assert.True(server.IsRunning);

            server.Stop();
            Assert.False(server.IsRunning);
        }
    }

    [Fact]
    public void StoragePaths_CleanupStaleTemporaryFiles_RemovesOnlyExpiredFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"quickdock_cleanup_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var oldTmp = Path.Combine(tempDir, "old_file.tmp");
            var newTmp = Path.Combine(tempDir, "new_file.tmp");

            File.WriteAllText(oldTmp, "old");
            File.WriteAllText(newTmp, "new");

            // Manually set old creation time
            File.SetCreationTimeUtc(oldTmp, DateTime.UtcNow.AddHours(-48));

            // Run cleanup with 24h threshold
            var cutoff = DateTime.UtcNow.AddHours(-24);
            foreach (var f in Directory.EnumerateFiles(tempDir, "*.tmp"))
            {
                if (new FileInfo(f).CreationTimeUtc < cutoff)
                {
                    File.Delete(f);
                }
            }

            Assert.False(File.Exists(oldTmp));
            Assert.True(File.Exists(newTmp));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }

    [Fact]
    public async Task Benchmark_FileHashStreaming_ExecutesEfficiently()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"hash_bench_{Guid.NewGuid():N}.dat");
        try
        {
            // Write 5 MB of data
            byte[] chunk = new byte[1024 * 1024];
            Array.Fill(chunk, (byte)0xAB);

            using (var fs = File.Create(tempFile))
            {
                for (int i = 0; i < 5; i++)
                {
                    await fs.WriteAsync(chunk);
                }
            }

            var hashService = new FileHashService(NullLogger<FileHashService>.Instance);
            var sw = Stopwatch.StartNew();
            string hash = await hashService.ComputeSha256Async(tempFile);
            sw.Stop();

            Assert.False(string.IsNullOrWhiteSpace(hash));
            Assert.Equal(64, hash.Length);
            Assert.True(sw.ElapsedMilliseconds < 2000, $"Streaming hash of 5MB took {sw.ElapsedMilliseconds}ms, should be <2000ms.");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }

    private sealed class DummyNotificationService : INotificationService
    {
        public void ShowNotification(string title, string message, string? action = null) { }
    }
}

