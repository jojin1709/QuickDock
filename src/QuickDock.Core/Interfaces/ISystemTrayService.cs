namespace QuickDock.Core.Interfaces;

public interface ISystemTrayService : IDisposable
{
    void Initialize(
        IntPtr hWnd,
        Action onToggleDock,
        Action onOpenSettings,
        Action onTogglePerformanceMode,
        Action onRestart,
        Action onExit);
    void UpdateDockState(bool isExpanded);
}

