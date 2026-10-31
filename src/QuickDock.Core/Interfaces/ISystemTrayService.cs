namespace QuickDock.Core.Interfaces;

public interface ISystemTrayService : IDisposable
{
    void Initialize(
        IntPtr hWnd,
        Action onToggleDock,
        Action onOpenSettings,
        Action onToggleStartup,
        Func<bool> isStartupEnabled,
        Action onTogglePerformanceMode,
        Action onAbout,
        Action onRestart,
        Action onExit);

    void UpdateDockState(bool isExpanded);
}
