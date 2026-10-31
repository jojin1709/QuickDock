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
    public const int BaseIdleWidth = 440;
    public const int BaseIdleHeight = 48;

    public const int BaseExpandedWidth = 860;
    public const int BaseExpandedHeight = 560;

    public const int BaseTopMargin = 12;

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

        int baseW = isExpanded ? BaseExpandedWidth : BaseIdleWidth;
        int baseH = isExpanded ? BaseExpandedHeight : BaseIdleHeight;

        int w = (int)Math.Round(baseW * scaleFactor);
        int h = (int)Math.Round(baseH * scaleFactor);

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
        int scaledTopMargin = (int)Math.Round(topMargin * scaleFactor);

        // Center horizontally on the target monitor's work area
        int x = workX + (workWidth - width) / 2;
        int y = workY + scaledTopMargin;

        return (x, y, width, height);
    }

    public void ConfigureDockWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return;

        try
        {
            long style = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWL_STYLE).ToInt64();
            style &= ~NativeMethods.WS_CAPTION;
            style &= ~NativeMethods.WS_THICKFRAME;
            style &= ~NativeMethods.WS_MINIMIZEBOX;
            style &= ~NativeMethods.WS_MAXIMIZEBOX;
            style |= NativeMethods.WS_POPUP;
            NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWL_STYLE, new IntPtr(style));

            bool alwaysOnTop = _settingsManager.CurrentSettings.Behavior.AlwaysOnTop;
            long exStyle = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE).ToInt64();
            if (alwaysOnTop)
            {
                exStyle |= NativeMethods.WS_EX_TOPMOST;
            }
            else
            {
                exStyle &= ~NativeMethods.WS_EX_TOPMOST;
            }
            NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle));

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

            _logger.LogDebug(
                "Dock repositioned on {MonitorName}. Expanded: {Expanded}, Pos: ({X},{Y}), Size: {W}x{H}, Scale: {Scale}",
                targetMonitor.DeviceName, isExpanded, x, y, width, height, targetMonitor.ScaleFactor);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reposition dock window.");
        }
    }
}
