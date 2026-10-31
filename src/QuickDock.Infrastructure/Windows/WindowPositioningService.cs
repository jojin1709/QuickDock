using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;

namespace QuickDock.Infrastructure.Windows;

public sealed class WindowPositioningService : IWindowPositioningService
{
    private readonly ILogger<WindowPositioningService> _logger;

    // Idle dimensions (320-520px wide, 40-56px high)
    public const int IdleWidth = 440;
    public const int IdleHeight = 48;

    // Expanded dimensions (700-1000px wide, 400-650px high)
    public const int ExpandedWidth = 860;
    public const int ExpandedHeight = 560;

    public const int TopMargin = 12;

    public WindowPositioningService(ILogger<WindowPositioningService> logger)
    {
        _logger = logger;
    }

    public (int Width, int Height) GetDockDimensions(bool isExpanded)
    {
        return isExpanded ? (ExpandedWidth, ExpandedHeight) : (IdleWidth, IdleHeight);
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

            long exStyle = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE).ToInt64();
            exStyle |= NativeMethods.WS_EX_TOPMOST;
            NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle));

            SetDockState(hWnd, isExpanded: false);
            _logger.LogInformation("Dock window successfully configured as borderless topmost dock.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure dock window styles.");
        }
    }

    public void SetDockState(IntPtr hWnd, bool isExpanded)
    {
        if (hWnd == IntPtr.Zero) return;

        try
        {
            int screenWidth = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
            if (screenWidth <= 0) screenWidth = 1920;

            var (width, height) = GetDockDimensions(isExpanded);
            int x = (screenWidth - width) / 2;
            int y = TopMargin;

            NativeMethods.SetWindowPos(
                hWnd,
                NativeMethods.HWND_TOPMOST,
                x,
                y,
                width,
                height,
                NativeMethods.SWP_SHOWWINDOW | NativeMethods.SWP_FRAMECHANGED
            );

            _logger.LogDebug("Dock state set. Expanded: {IsExpanded}, Pos: ({X},{Y}), Size: {W}x{H}",
                isExpanded, x, y, width, height);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set dock window state.");
        }
    }
}

