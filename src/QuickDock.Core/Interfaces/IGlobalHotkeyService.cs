namespace QuickDock.Core.Interfaces;

public interface IGlobalHotkeyService : IDisposable
{
    bool Register(IntPtr hWnd, Action onTriggered);
    void Unregister();
}

