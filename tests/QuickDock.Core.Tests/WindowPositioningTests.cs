using Microsoft.Extensions.Logging.Abstractions;
using QuickDock.Core.Events;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Settings;
using QuickDock.Infrastructure.Storage;
using QuickDock.Infrastructure.Windows;
using Xunit;

namespace QuickDock.Core.Tests;

public class WindowPositioningTests
{
    private static WindowPositioningService CreateService()
    {
        var bus = new InMemoryEventBus();
        var settingsManager = new JsonSettingsManager(bus, NullLogger<JsonSettingsManager>.Instance);
        var monitorService = new MonitorService(NullLogger<MonitorService>.Instance);
        return new WindowPositioningService(monitorService, settingsManager, NullLogger<WindowPositioningService>.Instance);
    }

    [Theory]
    [InlineData(1920, 1080, 0, 0, 1.0, 815, 0, 290, 34)]      // 1080p, 100% DPI -> (1920 - 290)/2 = 815
    [InlineData(2560, 1440, 0, 0, 1.0, 1135, 0, 290, 34)]     // 1440p, 100% DPI -> (2560 - 290)/2 = 1135
    [InlineData(3840, 2160, 0, 0, 1.0, 1775, 0, 290, 34)]     // 4K, 100% DPI -> (3840 - 290)/2 = 1775
    [InlineData(3440, 1440, 0, 0, 1.0, 1575, 0, 290, 34)]     // Ultrawide, 100% DPI -> (3440 - 290)/2 = 1575
    [InlineData(1080, 1920, 0, 0, 1.0, 395, 0, 290, 34)]      // Portrait, 100% DPI -> (1080 - 290)/2 = 395
    public void CalculatePlacement_IdleMode_CentersHorizontally_At100Dpi(
        int workWidth, int workHeight, int workX, int workY, double scale,
        int expectedX, int expectedY, int expectedW, int expectedH)
    {
        var service = CreateService();
        var (x, y, w, h) = service.CalculatePlacement(
            isExpanded: false,
            workX: workX,
            workY: workY,
            workWidth: workWidth,
            workHeight: workHeight,
            scaleFactor: scale);

        Assert.Equal(expectedX, x);
        Assert.Equal(expectedY, y);
        Assert.Equal(expectedW, w);
        Assert.Equal(expectedH, h);
    }

    [Theory]
    [InlineData(1920, 1080, 0, 0, 1.0, 550, 0, 820, 540)]     // 1080p, 100% DPI -> (1920 - 820)/2 = 550
    [InlineData(2560, 1440, 0, 0, 1.0, 870, 0, 820, 540)]     // 1440p, 100% DPI -> (2560 - 820)/2 = 870
    [InlineData(3840, 2160, 0, 0, 1.0, 1510, 0, 820, 540)]    // 4K, 100% DPI -> (3840 - 820)/2 = 1510
    public void CalculatePlacement_ExpandedMode_CentersHorizontally_At100Dpi(
        int workWidth, int workHeight, int workX, int workY, double scale,
        int expectedX, int expectedY, int expectedW, int expectedH)
    {
        var service = CreateService();
        var (x, y, w, h) = service.CalculatePlacement(
            isExpanded: true,
            workX: workX,
            workY: workY,
            workWidth: workWidth,
            workHeight: workHeight,
            scaleFactor: scale);

        Assert.Equal(expectedX, x);
        Assert.Equal(expectedY, y);
        Assert.Equal(expectedW, w);
        Assert.Equal(expectedH, h);
    }

    [Theory]
    [InlineData(1.0, 290, 34, 0)]
    [InlineData(1.25, 362, 42, 0)]
    [InlineData(1.5, 435, 51, 0)]
    [InlineData(1.75, 508, 60, 0)]
    [InlineData(2.0, 580, 68, 0)]
    public void CalculatePlacement_DpiScaling_ScalesDimensionsAndMargins(
        double scale, int expectedW, int expectedH, int expectedTopMargin)
    {
        var service = CreateService();
        int screenWidth = 3840;
        int screenHeight = 2160;

        var (x, y, w, h) = service.CalculatePlacement(
            isExpanded: false,
            workX: 0,
            workY: 0,
            workWidth: screenWidth,
            workHeight: screenHeight,
            scaleFactor: scale);

        Assert.Equal(expectedW, w);
        Assert.Equal(expectedH, h);
        Assert.Equal(expectedTopMargin, y);
        Assert.Equal((screenWidth - expectedW) / 2, x);
    }

    [Fact]
    public void CalculatePlacement_RespectsWorkAreaOffsets_OnSecondaryMonitor()
    {
        var service = CreateService();

        // Secondary monitor placed to the right of primary monitor: WorkX = 1920, WorkY = 40 (e.g. top taskbar)
        int secondaryWorkX = 1920;
        int secondaryWorkY = 40;
        int secondaryWorkWidth = 1920;
        int secondaryWorkHeight = 1040;

        var (x, y, w, h) = service.CalculatePlacement(
            isExpanded: false,
            workX: secondaryWorkX,
            workY: secondaryWorkY,
            workWidth: secondaryWorkWidth,
            workHeight: secondaryWorkHeight,
            scaleFactor: 1.0);

        Assert.Equal(1920 + (1920 - 290) / 2, x);
        Assert.Equal(40 + 0, y);
        Assert.Equal(290, w);
        Assert.Equal(34, h);
    }
}
