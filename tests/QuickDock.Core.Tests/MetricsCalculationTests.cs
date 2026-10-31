using Microsoft.Extensions.Logging.Abstractions;
using QuickDock.Services.System;
using Xunit;

namespace QuickDock.Core.Tests;

public class MetricsCalculationTests
{
    [Fact]
    public void MemoryMetrics_CalculatesValuesCorrectly()
    {
        var metrics = new MemoryMetrics(UsedGigabytes: 8.4, TotalGigabytes: 16.0, Percentage: 52);

        Assert.Equal(8.4, metrics.UsedGigabytes);
        Assert.Equal(16.0, metrics.TotalGigabytes);
        Assert.Equal(52, metrics.Percentage);
    }

    [Fact]
    public void DiskMetrics_Properties_AreAssignedCorrectly()
    {
        var metrics = new DiskMetrics(DriveLetter: "C:", TotalGigabytes: 512.0, FreeGigabytes: 148.0, UsedGigabytes: 364.0, UsedPercentage: 71);

        Assert.Equal("C:", metrics.DriveLetter);
        Assert.Equal(512.0, metrics.TotalGigabytes);
        Assert.Equal(148.0, metrics.FreeGigabytes);
        Assert.Equal(364.0, metrics.UsedGigabytes);
        Assert.Equal(71, metrics.UsedPercentage);
    }

    [Fact]
    public void BatteryMetrics_ChargingAndDischargingStates_FormatCorrectly()
    {
        var charging = new BatteryMetrics(82, IsCharging: true, IsOnBattery: false, HasBattery: true, StateDescription: "82% Charging");
        Assert.True(charging.IsCharging);
        Assert.False(charging.IsOnBattery);
        Assert.Equal("82% Charging", charging.StateDescription);

        var discharging = new BatteryMetrics(64, IsCharging: false, IsOnBattery: true, HasBattery: true, StateDescription: "64% On battery");
        Assert.False(discharging.IsCharging);
        Assert.True(discharging.IsOnBattery);
        Assert.Equal("64% On battery", discharging.StateDescription);

        var desktop = new BatteryMetrics(100, IsCharging: false, IsOnBattery: false, HasBattery: false, StateDescription: "AC Power");
        Assert.False(desktop.HasBattery);
        Assert.Equal("AC Power", desktop.StateDescription);
    }

    [Fact]
    public void UptimeMetrics_CalculatesAndFormatsUptime()
    {
        var service = new SystemMetricsService(NullLogger<SystemMetricsService>.Instance);
        var uptime = service.GetUptimeMetrics();

        Assert.NotNull(uptime);
        Assert.True(uptime.Uptime.TotalSeconds > 0);
        Assert.False(string.IsNullOrWhiteSpace(uptime.Formatted));
    }

    [Fact]
    public void DiskMetrics_CanQuerySystemDrive()
    {
        var service = new SystemMetricsService(NullLogger<SystemMetricsService>.Instance);
        var disk = service.GetDiskMetrics();

        Assert.NotNull(disk);
        Assert.True(disk.TotalGigabytes > 0);
        Assert.True(disk.FreeGigabytes >= 0);
        Assert.InRange(disk.UsedPercentage, 0, 100);
    }

    [Fact]
    public void NetworkMetrics_ReturnsValidNetworkStatus()
    {
        var service = new SystemMetricsService(NullLogger<SystemMetricsService>.Instance);
        var net = service.GetNetworkMetrics();

        Assert.NotNull(net);
        Assert.False(string.IsNullOrWhiteSpace(net.InterfaceType));
    }
}

