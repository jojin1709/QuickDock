using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;

namespace QuickDock.Infrastructure.Windows;

public sealed class SystemTrayService : ISystemTrayService
{
    private const int TrayIconId = 1;
    private static readonly UIntPtr SubclassId = new(1002);

    private const int CmdToggleDock = 101;
    private const int CmdSettings = 102;
    private const int CmdStartWithWindows = 103;
    private const int CmdPerformanceMode = 104;
    private const int CmdAbout = 105;
    private const int CmdRestart = 106;
    private const int CmdExit = 107;

    private readonly ILogger<SystemTrayService> _logger;
    private readonly NativeMethods.SUBCLASSPROC _subclassProc;

    private IntPtr _hWnd = IntPtr.Zero;
    private IntPtr _hIcon = IntPtr.Zero;
    private bool _isIconAdded;
    private bool _isExpanded;
    private bool _isDisposed;

    private Action? _onToggleDock;
    private Action? _onOpenSettings;
    private Action? _onToggleStartup;
    private Func<bool>? _isStartupEnabled;
    private Action? _onTogglePerformanceMode;
    private Action? _onAbout;
    private Action? _onRestart;
    private Action? _onExit;

    public SystemTrayService(ILogger<SystemTrayService> logger)
    {
        _logger = logger;
        _subclassProc = SubclassProcedure;
    }

    public void Initialize(
        IntPtr hWnd,
        Action onToggleDock,
        Action onOpenSettings,
        Action onToggleStartup,
        Func<bool> isStartupEnabled,
        Action onTogglePerformanceMode,
        Action onAbout,
        Action onRestart,
        Action onExit)
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(SystemTrayService));
        if (hWnd == IntPtr.Zero) return;

        _hWnd = hWnd;
        _onToggleDock = onToggleDock;
        _onOpenSettings = onOpenSettings;
        _onToggleStartup = onToggleStartup;
        _isStartupEnabled = isStartupEnabled;
        _onTogglePerformanceMode = onTogglePerformanceMode;
        _onAbout = onAbout;
        _onRestart = onRestart;
        _onExit = onExit;

        try
        {
            // Attach subclass for WM_TRAYICON
            NativeMethods.SetWindowSubclass(_hWnd, _subclassProc, SubclassId, UIntPtr.Zero);

            // Load Tray Icon
            _hIcon = LoadTrayIcon();

            var nid = new NativeMethods.NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf(typeof(NativeMethods.NOTIFYICONDATA)),
                hWnd = _hWnd,
                uID = TrayIconId,
                uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP,
                uCallbackMessage = NativeMethods.WM_TRAYICON,
                hIcon = _hIcon,
                szTip = "QuickDock"
            };

            _isIconAdded = NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_ADD, ref nid);
            if (_isIconAdded)
            {
                _logger.LogInformation("System tray icon successfully created.");
            }
            else
            {
                _logger.LogWarning("Shell_NotifyIconW failed to create tray icon.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize system tray service.");
        }
    }

    public void UpdateDockState(bool isExpanded)
    {
        _isExpanded = isExpanded;
    }

    private IntPtr LoadTrayIcon()
    {
        string[] searchPaths =
        [
            Path.Combine(AppContext.BaseDirectory, "Assets", "tray.ico"),
            Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"),
            Path.Combine(AppContext.BaseDirectory, "tray.ico")
        ];

        foreach (var path in searchPaths)
        {
            if (File.Exists(path))
            {
                IntPtr hIcon = NativeMethods.LoadImage(
                    IntPtr.Zero,
                    path,
                    1, /* IMAGE_ICON */
                    16,
                    16,
                    0x00000010 /* LR_LOADFROMFILE */
                );

                if (hIcon != IntPtr.Zero)
                {
                    _logger.LogDebug("Loaded tray icon from {Path}", path);
                    return hIcon;
                }
            }
        }

        _logger.LogWarning("Custom tray icon not found on disk, using empty fallback.");
        return IntPtr.Zero;
    }

    private IntPtr SubclassProcedure(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, UIntPtr dwRefData)
    {
        if (uMsg == NativeMethods.WM_TRAYICON)
        {
            uint mouseMsg = (uint)(lParam.ToInt64() & 0xFFFF);
            if (mouseMsg == NativeMethods.WM_LBUTTONUP)
            {
                _logger.LogDebug("Tray left-clicked. Toggling dock.");
                _onToggleDock?.Invoke();
                return IntPtr.Zero;
            }
            else if (mouseMsg == NativeMethods.WM_RBUTTONUP)
            {
                _logger.LogDebug("Tray right-clicked. Showing context menu.");
                ShowContextMenu();
                return IntPtr.Zero;
            }
        }

        return NativeMethods.DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        if (_hWnd == IntPtr.Zero) return;

        IntPtr hMenu = NativeMethods.CreatePopupMenu();
        if (hMenu == IntPtr.Zero) return;

        try
        {
            string toggleText = _isExpanded ? "Hide QuickDock" : "Show QuickDock";
            NativeMethods.AppendMenuW(hMenu, NativeMethods.MF_STRING, CmdToggleDock, toggleText);
            NativeMethods.AppendMenuW(hMenu, NativeMethods.MF_STRING, CmdSettings, "Settings");
            NativeMethods.AppendMenuW(hMenu, NativeMethods.MF_SEPARATOR, 0, null);

            // Start with Windows checkbox
            NativeMethods.AppendMenuW(hMenu, NativeMethods.MF_STRING, CmdStartWithWindows, "Start with Windows");
            bool isStartup = _isStartupEnabled?.Invoke() ?? false;
            NativeMethods.CheckMenuItem(hMenu, CmdStartWithWindows, isStartup ? NativeMethods.MF_CHECKED : NativeMethods.MF_UNCHECKED);

            NativeMethods.AppendMenuW(hMenu, NativeMethods.MF_STRING, CmdPerformanceMode, "Performance Mode");
            NativeMethods.AppendMenuW(hMenu, NativeMethods.MF_SEPARATOR, 0, null);

            NativeMethods.AppendMenuW(hMenu, NativeMethods.MF_STRING, CmdAbout, "About QuickDock");
            NativeMethods.AppendMenuW(hMenu, NativeMethods.MF_SEPARATOR, 0, null);

            NativeMethods.AppendMenuW(hMenu, NativeMethods.MF_STRING, CmdRestart, "Restart QuickDock");
            NativeMethods.AppendMenuW(hMenu, NativeMethods.MF_STRING, CmdExit, "Exit QuickDock");

            NativeMethods.SetForegroundWindow(_hWnd);
            NativeMethods.GetCursorPos(out var pt);

            int selectedCmd = NativeMethods.TrackPopupMenu(
                hMenu,
                NativeMethods.TPM_RETURNCMD | NativeMethods.TPM_BOTTOMALIGN | NativeMethods.TPM_LEFTALIGN,
                pt.X,
                pt.Y,
                0,
                _hWnd,
                IntPtr.Zero
            );

            switch (selectedCmd)
            {
                case CmdToggleDock:
                    _onToggleDock?.Invoke();
                    break;
                case CmdSettings:
                    _onOpenSettings?.Invoke();
                    break;
                case CmdStartWithWindows:
                    _onToggleStartup?.Invoke();
                    break;
                case CmdPerformanceMode:
                    _onTogglePerformanceMode?.Invoke();
                    break;
                case CmdAbout:
                    _onAbout?.Invoke();
                    break;
                case CmdRestart:
                    _onRestart?.Invoke();
                    break;
                case CmdExit:
                    _onExit?.Invoke();
                    break;
            }
        }
        finally
        {
            NativeMethods.DestroyMenu(hMenu);
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (_isIconAdded && _hWnd != IntPtr.Zero)
        {
            var nid = new NativeMethods.NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf(typeof(NativeMethods.NOTIFYICONDATA)),
                hWnd = _hWnd,
                uID = TrayIconId
            };
            NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_DELETE, ref nid);
            _isIconAdded = false;
        }

        if (_hWnd != IntPtr.Zero)
        {
            NativeMethods.RemoveWindowSubclass(_hWnd, _subclassProc, SubclassId);
            _hWnd = IntPtr.Zero;
        }

        _logger.LogInformation("System tray service disposed.");
    }
}
