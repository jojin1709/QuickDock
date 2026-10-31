using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;

namespace QuickDock.Infrastructure.Windows;

public sealed class GlobalHotkeyService : IGlobalHotkeyService
{
    private const int HotkeyId = 1;
    private static readonly UIntPtr SubclassId = new(1001);

    private readonly ILogger<GlobalHotkeyService> _logger;
    private readonly NativeMethods.SUBCLASSPROC _subclassProc;

    private IntPtr _hWnd = IntPtr.Zero;
    private Action? _onTriggered;
    private bool _isRegistered;
    private bool _isDisposed;

    public GlobalHotkeyService(ILogger<GlobalHotkeyService> logger)
    {
        _logger = logger;
        // Keep a reference to the delegate to prevent Garbage Collection
        _subclassProc = SubclassProcedure;
    }

    public bool Register(IntPtr hWnd, Action onTriggered)
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(GlobalHotkeyService));
        if (hWnd == IntPtr.Zero) return false;

        // Idempotent: unregister any previous hook before re-registering
        if (_isRegistered || _hWnd != IntPtr.Zero)
        {
            Unregister();
        }

        _hWnd = hWnd;
        _onTriggered = onTriggered;

        try
        {
            // Attach window subclass to intercept WM_HOTKEY
            NativeMethods.SetWindowSubclass(_hWnd, _subclassProc, SubclassId, UIntPtr.Zero);

            // Register Ctrl + Space with MOD_NOREPEAT
            uint modifiers = NativeMethods.MOD_CONTROL | NativeMethods.MOD_NOREPEAT;
            _isRegistered = NativeMethods.RegisterHotKey(_hWnd, HotkeyId, modifiers, NativeMethods.VK_SPACE);

            if (_isRegistered)
            {
                _logger.LogInformation("Global hotkey (Ctrl+Space) successfully registered.");
            }
            else
            {
                _logger.LogWarning("Failed to register global hotkey (Ctrl+Space). It may be in use by another application.");
            }

            return _isRegistered;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while registering global hotkey.");
            return false;
        }
    }

    public void Unregister()
    {
        if (_hWnd != IntPtr.Zero)
        {
            if (_isRegistered)
            {
                NativeMethods.UnregisterHotKey(_hWnd, HotkeyId);
                _isRegistered = false;
            }

            NativeMethods.RemoveWindowSubclass(_hWnd, _subclassProc, SubclassId);
            _hWnd = IntPtr.Zero;
            _logger.LogInformation("Global hotkey unregistered.");
        }
    }

    private IntPtr SubclassProcedure(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, UIntPtr dwRefData)
    {
        if (uMsg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            _logger.LogDebug("WM_HOTKEY received. Invoking callback.");
            try
            {
                _onTriggered?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing hotkey callback.");
            }
            return IntPtr.Zero;
        }

        return NativeMethods.DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        Unregister();
    }
}

