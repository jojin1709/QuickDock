using Microsoft.Extensions.Logging.Abstractions;
using QuickDock.Core.Interfaces;
using QuickDock.Infrastructure.Windows;
using Xunit;

namespace QuickDock.Core.Tests;

public class MonitorServiceTests
{
    [Fact]
    public void MonitorService_GetAllMonitors_ReturnsAtLeastOneMonitor()
    {
        var service = new MonitorService(NullLogger<MonitorService>.Instance);
        var monitors = service.GetAllMonitors();

        Assert.NotEmpty(monitors);
        var primary = monitors.FirstOrDefault(m => m.IsPrimary);
        Assert.NotNull(primary);
        Assert.True(primary.BoundsWidth > 0);
        Assert.True(primary.BoundsHeight > 0);
        Assert.True(primary.ScaleFactor >= 1.0);
    }

    [Fact]
    public void MonitorService_GetPrimaryMonitor_ReturnsValidPrimary()
    {
        var service = new MonitorService(NullLogger<MonitorService>.Instance);
        var primary = service.GetPrimaryMonitor();

        Assert.NotNull(primary);
        Assert.True(primary.IsPrimary);
    }

    [Fact]
    public void MonitorService_GetTargetMonitor_WithPrimary_ReturnsPrimary()
    {
        var service = new MonitorService(NullLogger<MonitorService>.Instance);
        var target = service.GetTargetMonitor("Primary", followActive: false);

        Assert.NotNull(target);
        Assert.True(target.IsPrimary);
    }

    [Fact]
    public void MonitorInfo_RecordProperties_AreAssignedCorrectly()
    {
        var info = new MonitorInfo(
            DeviceId: @"\\.\DISPLAY1",
            DeviceName: "Monitor 1 (Primary)",
            IsPrimary: true,
            BoundsX: 0,
            BoundsY: 0,
            BoundsWidth: 2560,
            BoundsHeight: 1440,
            WorkX: 0,
            WorkY: 0,
            WorkWidth: 2560,
            WorkHeight: 1400,
            Dpi: 120,
            ScaleFactor: 1.25
        );

        Assert.Equal(@"\\.\DISPLAY1", info.DeviceId);
        Assert.Equal(2560, info.BoundsWidth);
        Assert.Equal(1400, info.WorkHeight);
        Assert.Equal(1.25, info.ScaleFactor);
        Assert.Equal(120, info.Dpi);
    }
}

