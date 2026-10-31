namespace QuickDock.Core.Interfaces;

public sealed record MonitorInfo(
    string DeviceId,
    string DeviceName,
    bool IsPrimary,
    int BoundsX,
    int BoundsY,
    int BoundsWidth,
    int BoundsHeight,
    int WorkX,
    int WorkY,
    int WorkWidth,
    int WorkHeight,
    int Dpi,
    double ScaleFactor
);

public interface IMonitorService
{
    IReadOnlyList<MonitorInfo> GetAllMonitors();
    MonitorInfo GetPrimaryMonitor();
    MonitorInfo GetActiveMonitor();
    MonitorInfo GetTargetMonitor(string? monitorTarget, bool followActive);
    event EventHandler? MonitorsChanged;
}

