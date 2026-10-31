using Microsoft.Extensions.Logging.Abstractions;
using QuickDock.Core.Modules;
using QuickDock.Modules.Battery;
using QuickDock.Modules.Clock;
using QuickDock.Modules.Media;
using QuickDock.Modules.Network;
using QuickDock.Modules.System;
using QuickDock.Modules.Volume;
using QuickDock.Services.Audio;
using QuickDock.Services.Media;
using QuickDock.Services.System;
using Xunit;

namespace QuickDock.Core.Tests;

public class ModuleLifecycleTests
{
    [Fact]
    public async Task ClockModule_LifecycleTransitions_AreSafe()
    {
        var clock = new ClockModule(NullLogger<ClockModule>.Instance);

        Assert.Equal(ModuleState.Uninitialized, clock.State);

        await clock.InitializeAsync(CancellationToken.None);
        Assert.Equal(ModuleState.Initialized, clock.State);

        await clock.StartAsync(CancellationToken.None);
        Assert.Equal(ModuleState.Running, clock.State);

        // Duplicate start protection
        await clock.StartAsync(CancellationToken.None);
        Assert.Equal(ModuleState.Running, clock.State);

        await clock.StopAsync(CancellationToken.None);
        Assert.Equal(ModuleState.Stopped, clock.State);

        // Stop after stop is safe
        await clock.StopAsync(CancellationToken.None);
        Assert.Equal(ModuleState.Stopped, clock.State);
    }

    [Fact]
    public async Task SystemModule_LifecycleTransitions_AreSafe()
    {
        var metricsService = new SystemMetricsService(NullLogger<SystemMetricsService>.Instance);
        var system = new SystemModule(metricsService, NullLogger<SystemModule>.Instance);

        Assert.Equal(ModuleState.Uninitialized, system.State);

        await system.InitializeAsync(CancellationToken.None);
        Assert.Equal(ModuleState.Initialized, system.State);
        Assert.NotNull(system.CurrentSnapshot);

        await system.StartAsync(CancellationToken.None);
        Assert.Equal(ModuleState.Running, system.State);

        // Duplicate start protection
        await system.StartAsync(CancellationToken.None);
        Assert.Equal(ModuleState.Running, system.State);

        await system.StopAsync(CancellationToken.None);
        Assert.Equal(ModuleState.Stopped, system.State);
    }

    [Fact]
    public async Task VolumeModule_LifecycleTransitions_AreSafe()
    {
        using var audioService = new CoreAudioService(NullLogger<CoreAudioService>.Instance);
        var volume = new VolumeModule(audioService, NullLogger<VolumeModule>.Instance);

        Assert.Equal(ModuleState.Uninitialized, volume.State);

        await volume.InitializeAsync(CancellationToken.None);
        Assert.Equal(ModuleState.Initialized, volume.State);

        await volume.StartAsync(CancellationToken.None);
        Assert.Equal(ModuleState.Running, volume.State);

        await volume.StopAsync(CancellationToken.None);
        Assert.Equal(ModuleState.Stopped, volume.State);
    }

    [Fact]
    public async Task MediaModule_LifecycleTransitions_AreSafe()
    {
        using var mediaService = new WindowsMediaService(NullLogger<WindowsMediaService>.Instance);
        var media = new MediaModule(mediaService, NullLogger<MediaModule>.Instance);

        Assert.Equal(ModuleState.Uninitialized, media.State);

        await media.InitializeAsync(CancellationToken.None);
        Assert.Equal(ModuleState.Initialized, media.State);

        await media.StartAsync(CancellationToken.None);
        Assert.Equal(ModuleState.Running, media.State);

        await media.StopAsync(CancellationToken.None);
        Assert.Equal(ModuleState.Stopped, media.State);
    }

    [Fact]
    public async Task BatteryAndNetworkModules_LifecycleTransitions_AreSafe()
    {
        var metricsService = new SystemMetricsService(NullLogger<SystemMetricsService>.Instance);
        var battery = new BatteryModule(metricsService, NullLogger<BatteryModule>.Instance);
        var network = new NetworkModule(metricsService, NullLogger<NetworkModule>.Instance);

        Assert.Equal(ModuleState.Uninitialized, battery.State);
        Assert.Equal(ModuleState.Uninitialized, network.State);

        await battery.InitializeAsync(CancellationToken.None);
        await network.InitializeAsync(CancellationToken.None);

        Assert.Equal(ModuleState.Initialized, battery.State);
        Assert.Equal(ModuleState.Initialized, network.State);

        await battery.StartAsync(CancellationToken.None);
        await network.StartAsync(CancellationToken.None);

        Assert.Equal(ModuleState.Running, battery.State);
        Assert.Equal(ModuleState.Running, network.State);

        await battery.StopAsync(CancellationToken.None);
        await network.StopAsync(CancellationToken.None);

        Assert.Equal(ModuleState.Stopped, battery.State);
        Assert.Equal(ModuleState.Stopped, network.State);
    }
}
