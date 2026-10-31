using Microsoft.Extensions.Logging;
using QuickDock.Infrastructure.Windows;

namespace QuickDock.Services.System;

public sealed class SystemMetricsService : ISystemMetricsService
{
    private readonly ILogger<SystemMetricsService> _logger;
    private long _lastIdleTime;
    private long _lastKernelTime;
    private long _lastUserTime;
    private DateTimeOffset _lastSampleTime = DateTimeOffset.MinValue;
    private double? _lastCalculatedCpu;

    public SystemMetricsService(ILogger<SystemMetricsService> logger)
    {
        _logger = logger;
        // Warm up initial CPU sample
        NativeMethods.GetSystemTimes(out _lastIdleTime, out _lastKernelTime, out _lastUserTime);
        _lastSampleTime = DateTimeOffset.UtcNow;
    }

    public double? GetCpuUsagePercentage()
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            if ((now - _lastSampleTime).TotalMilliseconds < 500 && _lastCalculatedCpu.HasValue)
            {
                return _lastCalculatedCpu;
            }

            if (!NativeMethods.GetSystemTimes(out long idleTime, out long kernelTime, out long userTime))
            {
                return null;
            }

            long usrDiff = userTime - _lastUserTime;
            long kerDiff = kernelTime - _lastKernelTime;
            long idlDiff = idleTime - _lastIdleTime;

            long total = usrDiff + kerDiff;
            if (total <= 0)
            {
                return _lastCalculatedCpu;
            }

            long totalTime = total;
            long busy = totalTime - idlDiff;
            if (busy < 0) busy = 0;

            double cpuPercent = (busy * 100.0) / totalTime;
            if (cpuPercent > 100.0) cpuPercent = 100.0;
            if (cpuPercent < 0.0) cpuPercent = 0.0;

            _lastIdleTime = idleTime;
            _lastKernelTime = kernelTime;
            _lastUserTime = userTime;
            _lastSampleTime = now;
            _lastCalculatedCpu = Math.Round(cpuPercent, 1);

            return _lastCalculatedCpu;
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Failed to retrieve CPU usage metrics.");
            return null;
        }
    }

    public MemoryMetrics? GetMemoryMetrics()
    {
        try
        {
            var status = new NativeMethods.MEMORYSTATUSEX();
            if (!NativeMethods.GlobalMemoryStatusEx(ref status))
            {
                return null;
            }

            double totalGb = Math.Round((double)status.ullTotalPhys / (1024 * 1024 * 1024), 1);
            double availGb = Math.Round((double)status.ullAvailPhys / (1024 * 1024 * 1024), 1);
            double usedGb = Math.Round(totalGb - availGb, 1);
            int percent = (int)status.dwMemoryLoad;

            return new MemoryMetrics(usedGb, totalGb, percent);
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Failed to retrieve memory metrics.");
            return null;
        }
    }

    public BatteryMetrics? GetBatteryMetrics()
    {
        try
        {
            if (!NativeMethods.GetSystemPowerStatus(out var powerStatus))
            {
                return null;
            }

            // 255 = Unknown status / no system battery (e.g. desktop)
            if (powerStatus.BatteryLifePercent == 255 || powerStatus.BatteryFlag == 128)
            {
                return null;
            }

            int percent = powerStatus.BatteryLifePercent;
            bool isCharging = (powerStatus.ACLineStatus == 1) || (powerStatus.BatteryFlag & 8) != 0;

            return new BatteryMetrics(percent, isCharging);
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Failed to retrieve battery metrics.");
            return null;
        }
    }
}

