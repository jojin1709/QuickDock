using Microsoft.Extensions.Logging.Abstractions;
using QuickDock.Core.Events;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Settings;
using QuickDock.Infrastructure.Storage;
using QuickDock.Infrastructure.Windows;
using QuickDock.Services.System;
using Xunit;

namespace QuickDock.Core.Tests;

public class ShellServicesTests
{
    private static WindowPositioningService CreatePositioningService()
    {
        var bus = new InMemoryEventBus();
        var settingsManager = new JsonSettingsManager(bus, NullLogger<JsonSettingsManager>.Instance);
        var monitorService = new MonitorService(NullLogger<MonitorService>.Instance);
        return new WindowPositioningService(monitorService, settingsManager, NullLogger<WindowPositioningService>.Instance);
    }

    [Fact]
    public void WindowPositioningService_ReturnsExpectedDockDimensions()
    {
        var service = CreatePositioningService();

        var idleDims = service.GetDockDimensions(isExpanded: false);
        Assert.Equal(440, idleDims.Width);
        Assert.Equal(48, idleDims.Height);

        var expandedDims = service.GetDockDimensions(isExpanded: true);
        Assert.Equal(860, expandedDims.Width);
        Assert.Equal(560, expandedDims.Height);
    }

    [Fact]
    public void SystemMetricsService_DoesNotThrow_AndReturnsValidOrNullMetrics()
    {
        var service = new SystemMetricsService(NullLogger<SystemMetricsService>.Instance);

        var mem = service.GetMemoryMetrics();
        if (mem != null)
        {
            Assert.True(mem.TotalGigabytes > 0);
            Assert.True(mem.UsedGigabytes >= 0);
            Assert.InRange(mem.Percentage, 0, 100);
        }

        var cpu = service.GetCpuUsagePercentage();
        if (cpu.HasValue)
        {
            Assert.InRange(cpu.Value, 0.0, 100.0);
        }

        var battery = service.GetBatteryMetrics();
        if (battery != null)
        {
            Assert.InRange(battery.Percentage, 0, 100);
        }
    }

    [Fact]
    public async Task EventBus_PublishesAndReceives_AppVisibilityChangedEvent()
    {
        var eventBus = new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance);
        AppVisibilityChangedEvent? received = null;

        using var sub = eventBus.Subscribe<AppVisibilityChangedEvent>((evt, ct) =>
        {
            received = evt;
            return Task.CompletedTask;
        });

        await eventBus.PublishAsync(new AppVisibilityChangedEvent(true, "Hotkey"));

        Assert.NotNull(received);
        Assert.True(received!.IsExpanded);
        Assert.Equal("Hotkey", received.Trigger);
    }

    [Fact]
    public void QuickDockSettings_DefaultAppearanceAndBehavior_AreValid()
    {
        var settings = new QuickDockSettings();

        Assert.Equal(AppTheme.System, settings.Appearance.Theme);
        Assert.Equal("Ctrl+Space", settings.Behavior.GlobalHotkey);
        Assert.Equal(150, settings.Behavior.HoverDelayMs);
        Assert.True(settings.Behavior.AutoHide);
        Assert.True(settings.Behavior.AlwaysOnTop);
        Assert.True(settings.Behavior.HideInFullscreen);
        Assert.True(settings.Behavior.FollowActiveMonitor);
        Assert.True(settings.EnabledModules.ContainsKey("quickdock.clock"));
    }

    [Fact]
    public void SystemMetricsService_CanSampleRepeatedlyWithoutDeadlock()
    {
        var service = new SystemMetricsService(NullLogger<SystemMetricsService>.Instance);
        for (int i = 0; i < 5; i++)
        {
            var cpu = service.GetCpuUsagePercentage();
            var mem = service.GetMemoryMetrics();
            var bat = service.GetBatteryMetrics();
        }
    }
}
