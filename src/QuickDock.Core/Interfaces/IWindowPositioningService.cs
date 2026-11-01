namespace QuickDock.Core.Interfaces;

public interface IWindowPositioningService
{
    void ConfigureDockWindow(IntPtr hWnd, MonitorInfo? targetMonitor = null);
    void SetDockState(IntPtr hWnd, bool isExpanded, MonitorInfo? targetMonitor = null);
    void Reposition(IntPtr hWnd, bool isExpanded, MonitorInfo? targetMonitor = null);
    void SetAlwaysOnTop(IntPtr hWnd, bool alwaysOnTop);
    (int Width, int Height) GetDockDimensions(bool isExpanded, double scaleFactor = 1.0);
    (int X, int Y, int Width, int Height) CalculatePlacement(
        bool isExpanded,
        int workX, int workY, int workWidth, int workHeight,
        double scaleFactor,
        int topMargin = 0);
}
