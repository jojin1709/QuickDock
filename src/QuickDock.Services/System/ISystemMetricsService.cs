namespace QuickDock.Services.System;

public record MemoryMetrics(double UsedGigabytes, double TotalGigabytes, int Percentage);
public record BatteryMetrics(int Percentage, bool IsCharging);

public interface ISystemMetricsService
{
    double? GetCpuUsagePercentage();
    MemoryMetrics? GetMemoryMetrics();
    BatteryMetrics? GetBatteryMetrics();
}

