using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;

namespace QuickDock.Infrastructure.Windows;

public sealed class MonitorService : IMonitorService
{
    private readonly ILogger<MonitorService> _logger;

    public event EventHandler? MonitorsChanged;

    public MonitorService(ILogger<MonitorService> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<MonitorInfo> GetAllMonitors()
    {
        var monitors = new List<MonitorInfo>();

        NativeMethods.MonitorEnumProc callback = (IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT lprcMonitor, IntPtr dwData) =>
        {
            var mi = new NativeMethods.MONITORINFOEX();
            mi.cbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFOEX));

            if (NativeMethods.GetMonitorInfoW(hMonitor, ref mi))
            {
                bool isPrimary = (mi.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0;
                string deviceId = mi.szDevice ?? $"DISPLAY_{monitors.Count + 1}";
                string friendlyName = FormatFriendlyName(deviceId, monitors.Count + 1, isPrimary);

                int dpi = 96;
                try
                {
                    int hr = NativeMethods.GetDpiForMonitor(
                        hMonitor,
                        NativeMethods.MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI,
                        out uint dpiX,
                        out uint _);
                    if (hr == 0 && dpiX > 0)
                    {
                        dpi = (int)dpiX;
                    }
                }
                catch
                {
                    dpi = 96;
                }

                double scaleFactor = dpi / 96.0;

                monitors.Add(new MonitorInfo(
                    DeviceId: deviceId,
                    DeviceName: friendlyName,
                    IsPrimary: isPrimary,
                    BoundsX: mi.rcMonitor.Left,
                    BoundsY: mi.rcMonitor.Top,
                    BoundsWidth: mi.rcMonitor.Width,
                    BoundsHeight: mi.rcMonitor.Height,
                    WorkX: mi.rcWork.Left,
                    WorkY: mi.rcWork.Top,
                    WorkWidth: mi.rcWork.Width,
                    WorkHeight: mi.rcWork.Height,
                    Dpi: dpi,
                    ScaleFactor: scaleFactor
                ));
            }
            return true;
        };

        try
        {
            NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate display monitors.");
        }

        if (monitors.Count == 0)
        {
            // Fallback for headless / simulated environments
            int width = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
            int height = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);
            if (width <= 0) width = 1920;
            if (height <= 0) height = 1080;

            monitors.Add(new MonitorInfo(
                DeviceId: @"\\.\DISPLAY1",
                DeviceName: "Monitor 1 (Primary)",
                IsPrimary: true,
                BoundsX: 0,
                BoundsY: 0,
                BoundsWidth: width,
                BoundsHeight: height,
                WorkX: 0,
                WorkY: 0,
                WorkWidth: width,
                WorkHeight: height - 48, // approximate taskbar
                Dpi: 96,
                ScaleFactor: 1.0
            ));
        }

        return monitors;
    }

    public MonitorInfo GetPrimaryMonitor()
    {
        var monitors = GetAllMonitors();
        return monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];
    }

    public MonitorInfo GetActiveMonitor()
    {
        var monitors = GetAllMonitors();
        if (monitors.Count == 0) return GetPrimaryMonitor();

        try
        {
            // 1. Try foreground window
            IntPtr fgHwnd = NativeMethods.GetForegroundWindow();
            if (fgHwnd != IntPtr.Zero)
            {
                IntPtr hMon = NativeMethods.MonitorFromWindow(fgHwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
                var match = FindMonitorByHandle(hMon, monitors);
                if (match != null) return match;
            }

            // 2. Try cursor position
            if (NativeMethods.GetCursorPos(out var pt))
            {
                IntPtr hMon = NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONEAREST);
                var match = FindMonitorByHandle(hMon, monitors);
                if (match != null) return match;

                // Or check point in monitor bounds
                var pointMatch = monitors.FirstOrDefault(m =>
                    pt.X >= m.BoundsX && pt.X < m.BoundsX + m.BoundsWidth &&
                    pt.Y >= m.BoundsY && pt.Y < m.BoundsY + m.BoundsHeight);
                if (pointMatch != null) return pointMatch;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error detecting active monitor, falling back to primary.");
        }

        return GetPrimaryMonitor();
    }

    public MonitorInfo GetTargetMonitor(string? monitorTarget, bool followActive)
    {
        if (followActive || string.Equals(monitorTarget, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return GetActiveMonitor();
        }

        if (string.IsNullOrEmpty(monitorTarget) ||
            string.Equals(monitorTarget, "Primary", StringComparison.OrdinalIgnoreCase))
        {
            return GetPrimaryMonitor();
        }

        var monitors = GetAllMonitors();

        // Match by exact DeviceId or DeviceName
        var match = monitors.FirstOrDefault(m =>
            string.Equals(m.DeviceId, monitorTarget, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(m.DeviceName, monitorTarget, StringComparison.OrdinalIgnoreCase));

        if (match != null) return match;

        // Match by index like "Monitor 1" or "1"
        if (int.TryParse(monitorTarget.Replace("Monitor", "", StringComparison.OrdinalIgnoreCase).Trim(), out int idx) &&
            idx >= 1 && idx <= monitors.Count)
        {
            return monitors[idx - 1];
        }

        return GetPrimaryMonitor();
    }

    private static MonitorInfo? FindMonitorByHandle(IntPtr hMonitor, IReadOnlyList<MonitorInfo> monitors)
    {
        if (hMonitor == IntPtr.Zero) return null;

        var mi = new NativeMethods.MONITORINFOEX();
        mi.cbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFOEX));
        if (NativeMethods.GetMonitorInfoW(hMonitor, ref mi))
        {
            string id = mi.szDevice ?? "";
            return monitors.FirstOrDefault(m => string.Equals(m.DeviceId, id, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    private static string FormatFriendlyName(string deviceId, int index, bool isPrimary)
    {
        string baseName = deviceId;
        if (deviceId.StartsWith(@"\\.\DISPLAY", StringComparison.OrdinalIgnoreCase))
        {
            string num = deviceId.Substring(@"\\.\DISPLAY".Length);
            baseName = $"Monitor {num}";
        }
        else
        {
            baseName = $"Monitor {index}";
        }

        return isPrimary ? $"{baseName} (Primary)" : baseName;
    }

    public void NotifyMonitorsChanged()
    {
        MonitorsChanged?.Invoke(this, EventArgs.Empty);
    }
}
