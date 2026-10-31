namespace QuickDock.Core.Interfaces;

public interface IWindowPositioningService
{
    void ConfigureDockWindow(IntPtr hWnd);
    void SetDockState(IntPtr hWnd, bool isExpanded);
    (int Width, int Height) GetDockDimensions(bool isExpanded);
}
