using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;

namespace QuickDock.Infrastructure.Windows;

public sealed class FullscreenDetectionService : IFullscreenService
{
    private readonly ILogger<FullscreenDetectionService> _logger;
    private readonly NativeMethods.WinEventProc _winEventProc;

    private IntPtr _hHook = IntPtr.Zero;
    private IntPtr _dockHwnd = IntPtr.Zero;
    private bool _isFullscreenActive;
    private bool _isDisposed;

    public bool IsFullscreenActive => _isFullscreenActive;
    public event EventHandler<bool>? FullscreenStateChanged;

    public FullscreenDetectionService(ILogger<FullscreenDetectionService> logger)
    {
        _logger = logger;
        _winEventProc = OnWinEvent;
    }

    public void SetDockHandle(IntPtr dockHwnd)
    {
        _dockHwnd = dockHwnd;
    }

    public void StartMonitoring()
    {
        if (_hHook != IntPtr.Zero || _isDisposed) return;

        try
        {
            _hHook = NativeMethods.SetWinEventHook(
                NativeMethods.EVENT_SYSTEM_FOREGROUND,
                NativeMethods.EVENT_OBJECT_LOCATIONCHANGE,
                IntPtr.Zero,
                _winEventProc,
                0,
                0,
                NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS
            );

            if (_hHook != IntPtr.Zero)
            {
                _logger.LogInformation("Fullscreen detection WinEventHook installed.");
            }
            else
            {
                _logger.LogWarning("Failed to install WinEventHook for fullscreen detection.");
            }

            CheckCurrentFullscreenState();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception starting fullscreen monitoring.");
        }
    }

    public void StopMonitoring()
    {
        if (_hHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_hHook);
            _hHook = IntPtr.Zero;
            _logger.LogInformation("Fullscreen detection WinEventHook unhooked.");
        }
    }

    private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hWnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        // Only inspect standard window events (idObject == 0: OBJID_WINDOW)
        if (idObject != 0) return;

        CheckCurrentFullscreenState();
    }

    public void CheckCurrentFullscreenState()
    {
        try
        {
            IntPtr fgHwnd = NativeMethods.GetForegroundWindow();
            bool isFullscreen = IsWindowFullscreen(fgHwnd, _dockHwnd);

            if (isFullscreen != _isFullscreenActive)
            {
                _isFullscreenActive = isFullscreen;
                _logger.LogInformation("Fullscreen state changed: {IsFullscreen}", isFullscreen);
                FullscreenStateChanged?.Invoke(this, isFullscreen);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking fullscreen state.");
        }
    }

    public static bool IsWindowFullscreen(IntPtr hWnd, IntPtr dockHwnd = default)
    {
        if (hWnd == IntPtr.Zero || hWnd == dockHwnd) return false;

        // Ignore shell windows
        if (hWnd == NativeMethods.GetShellWindow() || hWnd == NativeMethods.GetDesktopWindow())
        {
            return false;
        }

        if (!NativeMethods.IsWindowVisible(hWnd) || NativeMethods.IsIconic(hWnd))
        {
            return false;
        }

        var sbClass = new StringBuilder(256);
        NativeMethods.GetClassNameW(hWnd, sbClass, sbClass.Capacity);
        string className = sbClass.ToString();

        if (className is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Windows.UI.Core.CoreWindow")
        {
            return false;
        }

        if (!NativeMethods.GetWindowRect(hWnd, out var windowRect))
        {
            return false;
        }

        IntPtr hMonitor = NativeMethods.MonitorFromWindow(hWnd, NativeMethods.MONITOR_DEFAULTTONULL);
        if (hMonitor == IntPtr.Zero)
        {
            return false;
        }

        var mi = new NativeMethods.MONITORINFOEX();
        mi.cbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFOEX));
        if (!NativeMethods.GetMonitorInfoW(hMonitor, ref mi))
        {
            return false;
        }

        return EvaluateFullscreenBounds(
            windowRect.Left, windowRect.Top, windowRect.Right, windowRect.Bottom,
            mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.Right, mi.rcMonitor.Bottom
        );
    }

    public static bool EvaluateFullscreenBounds(
        int winLeft, int winTop, int winRight, int winBottom,
        int monLeft, int monTop, int monRight, int monBottom)
    {
        // Window covers or exceeds monitor boundary
        return winLeft <= monLeft &&
               winTop <= monTop &&
               winRight >= monRight &&
               winBottom >= monBottom;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        StopMonitoring();
    }
}
