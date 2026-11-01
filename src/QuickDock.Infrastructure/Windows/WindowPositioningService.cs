using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Settings;

namespace QuickDock.Infrastructure.Windows;

public sealed class WindowPositioningService : IWindowPositioningService
{
    private readonly IMonitorService _monitorService;
    private readonly ISettingsManager _settingsManager;
    private readonly ILogger<WindowPositioningService> _logger;

    // Base dimensions at 100% DPI (96 DPI)
    public const int BaseIdleWidth = 290;
    public const int BaseIdleHeight = 34;

    public const int BaseExpandedWidth = 820;
    public const int BaseExpandedHeight = 540;

    public const int BaseTopMargin = 0;

    public WindowPositioningService(
        IMonitorService monitorService,
        ISettingsManager settingsManager,
        ILogger<WindowPositioningService> logger)
    {
        _monitorService = monitorService;
        _settingsManager = settingsManager;
        _logger = logger;
    }

    public (int Width, int Height) GetDockDimensions(bool isExpanded, double scaleFactor = 1.0)
    {
        if (scaleFactor <= 0) scaleFactor = 1.0;

        double sizeMultiplier = _settingsManager.CurrentSettings.Behavior.Size switch
        {
            DockSize.Compact => 0.85,
            DockSize.Large => 1.15,
            _ => 1.0
        };

        int baseW = isExpanded ? BaseExpandedWidth : BaseIdleWidth;
        int baseH = isExpanded ? BaseExpandedHeight : BaseIdleHeight;

        int w = (int)Math.Round(baseW * scaleFactor * sizeMultiplier);
        int h = (int)Math.Round(baseH * scaleFactor * sizeMultiplier);

        return (w, h);
    }

    public (int X, int Y, int Width, int Height) CalculatePlacement(
        bool isExpanded,
        int workX, int workY, int workWidth, int workHeight,
        double scaleFactor,
        int topMargin = BaseTopMargin)
    {
        if (scaleFactor <= 0) scaleFactor = 1.0;

        var (width, height) = GetDockDimensions(isExpanded, scaleFactor);
        int scaledMargin = (int)Math.Round(topMargin * scaleFactor);

        // Center horizontally on the target monitor's work area
        int x = workX + (workWidth - width) / 2;

        int y;
        if (_settingsManager.CurrentSettings.Behavior.Position == DockPosition.Bottom)
        {
            y = workY + workHeight - height - scaledMargin;
        }
        else
        {
            y = workY + scaledMargin;
        }

        return (x, y, width, height);
    }

    public void ConfigureDockWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return;

        try
        {
            // 1. Remove from taskbar and Alt+Tab via extended styles
            long exStyle = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE).ToInt64();
            exStyle |= NativeMethods.WS_EX_TOOLWINDOW;
            exStyle &= ~NativeMethods.WS_EX_APPWINDOW;
            NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle));

            // 2. Disable default OS window border and standard window rounding
            int cornerPreference = NativeMethods.DWMWCP_DONOTROUND;
            NativeMethods.DwmSetWindowAttribute(hWnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));

            uint colorNone = NativeMethods.DWMWA_COLOR_NONE;
            NativeMethods.DwmSetWindowAttribute(hWnd, NativeMethods.DWMWA_BORDER_COLOR, ref colorNone, sizeof(uint));

            bool alwaysOnTop = _settingsManager.CurrentSettings.Behavior.AlwaysOnTop;
            SetAlwaysOnTop(hWnd, alwaysOnTop);
            SetDockState(hWnd, isExpanded: false);
            _logger.LogInformation("Dock window successfully configured (AlwaysOnTop={AlwaysOnTop}).", alwaysOnTop);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure dock window styles.");
        }
    }

    public void SetAlwaysOnTop(IntPtr hWnd, bool alwaysOnTop)
    {
        if (hWnd == IntPtr.Zero) return;

        try
        {
            IntPtr insertAfter = alwaysOnTop ? NativeMethods.HWND_TOPMOST : NativeMethods.HWND_NOTOPMOST;
            NativeMethods.SetWindowPos(
                hWnd,
                insertAfter,
                0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE
            );
            _logger.LogInformation("Dock window TopMost updated to {AlwaysOnTop}", alwaysOnTop);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update always-on-top state.");
        }
    }

    public void SetDockState(IntPtr hWnd, bool isExpanded)
    {
        Reposition(hWnd, isExpanded);
    }

    public void Reposition(IntPtr hWnd, bool isExpanded)
    {
        if (hWnd == IntPtr.Zero) return;

        try
        {
            var behavior = _settingsManager.CurrentSettings.Behavior;
            var targetMonitor = _monitorService.GetTargetMonitor(behavior.MonitorTarget, behavior.FollowActiveMonitor);

            var (x, y, width, height) = CalculatePlacement(
                isExpanded,
                targetMonitor.WorkX,
                targetMonitor.WorkY,
                targetMonitor.WorkWidth,
                targetMonitor.WorkHeight,
                targetMonitor.ScaleFactor
            );

            IntPtr insertAfter = behavior.AlwaysOnTop ? NativeMethods.HWND_TOPMOST : NativeMethods.HWND_NOTOPMOST;

            NativeMethods.SetWindowPos(
                hWnd,
                insertAfter,
                x,
                y,
                width,
                height,
                NativeMethods.SWP_SHOWWINDOW | NativeMethods.SWP_FRAMECHANGED
            );

            // Shape the HWND so corners outside the pill are physically transparent and not drawn by DWM
            ApplyWindowRegion(hWnd, width, height, isExpanded);

            _logger.LogDebug(
                "Dock repositioned on {MonitorName}. Expanded: {Expanded}, Pos: ({X},{Y}), Size: {W}x{H}, Scale: {Scale}",
                targetMonitor.DeviceName, isExpanded, x, y, width, height, targetMonitor.ScaleFactor);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reposition dock window.");
        }
    }

    private static void ApplyWindowRegion(IntPtr hWnd, int width, int height, bool isExpanded)
    {
        if (hWnd == IntPtr.Zero || width <= 0 || height <= 0) return;

        try
        {
            // Idle notch: corner radius 14 (diameter 28). Top starts at -10 to sit flush with top display bezel.
            // Expanded dashboard: corner radius 20 (diameter 40). Top starts at -10 to sit flush with top display bezel.
            int cornerDiameter = isExpanded ? 40 : 28;
            IntPtr hRgn = NativeMethods.CreateRoundRectRgn(0, -10, width + 1, height + 1, cornerDiameter, cornerDiameter);
            if (hRgn != IntPtr.Zero)
            {
                // Windows takes ownership of hRgn after successful SetWindowRgn
                NativeMethods.SetWindowRgn(hWnd, hRgn, true);
            }
        }
        catch
        {
            // Fallback gracefully
        }
    }
}
