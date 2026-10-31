namespace QuickDock.Services.System;

public record CpuMetrics(double UsagePercentage, string? ModelName = null);
public record MemoryMetrics(double UsedGigabytes, double TotalGigabytes, int Percentage);
public record GpuMetrics(string Name, double? UsagePercentage, double? UsedMemoryGigabytes, double? TotalMemoryGigabytes, bool IsAvailable);
public record DiskMetrics(string DriveLetter, double TotalGigabytes, double FreeGigabytes, double UsedGigabytes, int UsedPercentage);
public record BatteryMetrics(int Percentage, bool IsCharging, bool IsOnBattery, bool HasBattery, string StateDescription);
public record NetworkMetrics(string InterfaceType, bool IsConnected, string? ConnectionName);
public record UptimeMetrics(TimeSpan Uptime, string Formatted);

public record SystemMetricsSnapshot(
    CpuMetrics? Cpu,
    MemoryMetrics? Memory,
    GpuMetrics? Gpu,
    DiskMetrics? Disk,
    BatteryMetrics? Battery,
    NetworkMetrics? Network,
    UptimeMetrics? Uptime
);

public interface ISystemMetricsService
{
    double? GetCpuUsagePercentage();
    CpuMetrics? GetCpuMetrics();
    MemoryMetrics? GetMemoryMetrics();
    GpuMetrics? GetGpuMetrics();
    DiskMetrics? GetDiskMetrics(string? driveLetter = null);
    BatteryMetrics? GetBatteryMetrics();
    NetworkMetrics? GetNetworkMetrics();
    UptimeMetrics GetUptimeMetrics();
    SystemMetricsSnapshot GetSystemSnapshot(bool detailed = false);
}
