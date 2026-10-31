using Microsoft.Extensions.Logging.Abstractions;
using QuickDock.Core.Events;
using QuickDock.Core.Modules;
using Xunit;

namespace QuickDock.Core.Tests;

public class ModuleManagerTests
{
    private class TestModule : ModuleBase
    {
        public override string Id { get; }
        public override string Name { get; }
        public override string Description => "Test module";
        public override ModuleCategory Category => ModuleCategory.Utilities;

        public bool ThrowOnInitialize { get; set; }
        public bool ThrowOnStart { get; set; }
        public bool ThrowOnStop { get; set; }

        public int InitializeCount { get; private set; }
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }

        public TestModule(string id, string name = "Test Module")
            : base(NullLogger.Instance)
        {
            Id = id;
            Name = name;
        }

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            InitializeCount++;
            if (ThrowOnInitialize)
            {
                throw new InvalidOperationException("Initialization failure simulated.");
            }
            return Task.CompletedTask;
        }

        protected override Task OnStartAsync(CancellationToken cancellationToken)
        {
            StartCount++;
            if (ThrowOnStart)
            {
                throw new InvalidOperationException("Start failure simulated.");
            }
            return Task.CompletedTask;
        }

        protected override Task OnStopAsync(CancellationToken cancellationToken)
        {
            StopCount++;
            if (ThrowOnStop)
            {
                throw new InvalidOperationException("Stop failure simulated.");
            }
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void RegisterAndGetModule_Succeeds()
    {
        var bus = new InMemoryEventBus();
        var manager = new ModuleManager(bus, NullLogger<ModuleManager>.Instance);
        var module = new TestModule("test.mod1");

        manager.RegisterModule(module);

        var retrieved = manager.GetModule("test.mod1");
        Assert.NotNull(retrieved);
        Assert.Equal("test.mod1", retrieved.Id);
    }

    [Fact]
    public async Task InitializeAllEnabledAsync_InitializesOnlyEnabledModules()
    {
        var bus = new InMemoryEventBus();
        var manager = new ModuleManager(bus, NullLogger<ModuleManager>.Instance);

        var enabledModule = new TestModule("mod.enabled") { IsEnabled = true };
        var disabledModule = new TestModule("mod.disabled") { IsEnabled = false };

        manager.RegisterModule(enabledModule);
        manager.RegisterModule(disabledModule);

        await manager.InitializeAllEnabledAsync();

        Assert.Equal(ModuleState.Initialized, enabledModule.State);
        Assert.Equal(1, enabledModule.InitializeCount);

        Assert.Equal(ModuleState.Uninitialized, disabledModule.State);
        Assert.Equal(0, disabledModule.InitializeCount);
    }

    [Fact]
    public async Task StartAndStopModuleAsync_CyclesStateCorrectly()
    {
        var bus = new InMemoryEventBus();
        var manager = new ModuleManager(bus, NullLogger<ModuleManager>.Instance);
        var module = new TestModule("mod.lifecycle");
        manager.RegisterModule(module);

        await manager.InitializeAllEnabledAsync();
        Assert.Equal(ModuleState.Initialized, module.State);

        var startResult = await manager.StartModuleAsync("mod.lifecycle");
        Assert.True(startResult);
        Assert.Equal(ModuleState.Running, module.State);

        var stopResult = await manager.StopModuleAsync("mod.lifecycle");
        Assert.True(stopResult);
        Assert.Equal(ModuleState.Stopped, module.State);
    }

    [Fact]
    public async Task CrashIsolation_FaultyModuleOnInitialize_DoesNotCrashManager()
    {
        var bus = new InMemoryEventBus();
        var manager = new ModuleManager(bus, NullLogger<ModuleManager>.Instance);

        var faultyModule = new TestModule("faulty.init") { ThrowOnInitialize = true };
        var normalModule = new TestModule("normal.init");

        manager.RegisterModule(faultyModule);
        manager.RegisterModule(normalModule);

        ModuleStateChangedEvent? capturedEvent = null;
        using var sub = bus.Subscribe<ModuleStateChangedEvent>((e, _) =>
        {
            if (e.ModuleId == "faulty.init")
            {
                capturedEvent = e;
            }
            return Task.CompletedTask;
        });

        // Must NOT throw
        var exception = await Record.ExceptionAsync(() => manager.InitializeAllEnabledAsync());
        Assert.Null(exception);

        // Faulty module is safely isolated and marked Faulted
        Assert.Equal(ModuleState.Faulted, faultyModule.State);
        Assert.NotNull(faultyModule.LastError);

        // Normal module succeeded
        Assert.Equal(ModuleState.Initialized, normalModule.State);

        // State change event was published
        Assert.NotNull(capturedEvent);
        Assert.Equal(ModuleState.Faulted, capturedEvent.NewState);
    }

    [Fact]
    public async Task CrashIsolation_FaultyModuleOnStart_DoesNotCrashHost()
    {
        var bus = new InMemoryEventBus();
        var manager = new ModuleManager(bus, NullLogger<ModuleManager>.Instance);
        var faultyModule = new TestModule("faulty.start") { ThrowOnStart = true };

        manager.RegisterModule(faultyModule);
        await manager.InitializeAllEnabledAsync();

        var success = await manager.StartModuleAsync("faulty.start");
        Assert.False(success);
        Assert.Equal(ModuleState.Faulted, faultyModule.State);
    }

    [Fact]
    public async Task StopAllRunningAsync_StopsOnlyActiveModules()
    {
        var bus = new InMemoryEventBus();
        var manager = new ModuleManager(bus, NullLogger<ModuleManager>.Instance);

        var runningModule = new TestModule("mod.running");
        var stoppedModule = new TestModule("mod.stopped");

        manager.RegisterModule(runningModule);
        manager.RegisterModule(stoppedModule);

        await manager.InitializeAllEnabledAsync();
        await manager.StartModuleAsync("mod.running");

        Assert.Equal(ModuleState.Running, runningModule.State);
        Assert.Equal(ModuleState.Initialized, stoppedModule.State);

        await manager.StopAllRunningAsync();

        Assert.Equal(ModuleState.Stopped, runningModule.State);
        Assert.Equal(ModuleState.Initialized, stoppedModule.State);
    }
}
