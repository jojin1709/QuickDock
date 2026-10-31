namespace QuickDock.Core.Interfaces;

public interface IFullscreenService : IDisposable
{
    bool IsFullscreenActive { get; }
    event EventHandler<bool>? FullscreenStateChanged;
    void StartMonitoring();
    void StopMonitoring();
}

