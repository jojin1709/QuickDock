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
    [InlineData(1920, 1080, 0, 0, 1.0, 740, 12, 440, 48)]      // 1080p, 100% DPI -> (1920 - 440)/2 = 740
    [InlineData(2560, 1440, 0, 0, 1.0, 1060, 12, 440, 48)]     // 1440p, 100% DPI -> (2560 - 440)/2 = 1060
    [InlineData(3840, 2160, 0, 0, 1.0, 1700, 12, 440, 48)]     // 4K, 100% DPI -> (3840 - 440)/2 = 1700
    [InlineData(3440, 1440, 0, 0, 1.0, 1500, 12, 440, 48)]     // Ultrawide, 100% DPI -> (3440 - 440)/2 = 1500
    [InlineData(1080, 1920, 0, 0, 1.0, 320, 12, 440, 48)]      // Portrait, 100% DPI -> (1080 - 440)/2 = 320
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
    [InlineData(1920, 1080, 0, 0, 1.0, 530, 12, 860, 560)]     // 1080p, 100% DPI -> (1920 - 860)/2 = 530
    [InlineData(2560, 1440, 0, 0, 1.0, 850, 12, 860, 560)]     // 1440p, 100% DPI -> (2560 - 860)/2 = 850
    [InlineData(3840, 2160, 0, 0, 1.0, 1490, 12, 860, 560)]    // 4K, 100% DPI -> (3840 - 860)/2 = 1490
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
    [InlineData(1.0, 440, 48, 12)]
    [InlineData(1.25, 550, 60, 15)]
    [InlineData(1.5, 660, 72, 18)]
    [InlineData(1.75, 770, 84, 21)]
    [InlineData(2.0, 880, 96, 24)]
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

        Assert.Equal(1920 + (1920 - 440) / 2, x);
        Assert.Equal(40 + 12, y);
        Assert.Equal(440, w);
        Assert.Equal(48, h);
    }
}
