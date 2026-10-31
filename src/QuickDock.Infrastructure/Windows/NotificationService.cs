using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;

namespace QuickDock.Infrastructure.Windows;

public sealed class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;
    private IntPtr _trayHwnd = IntPtr.Zero;
    private int _trayIconId = 1;

    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger;
    }

    public void RegisterTrayHandle(IntPtr hWnd, int iconId = 1)
    {
        _trayHwnd = hWnd;
        _trayIconId = iconId;
    }

    public void ShowNotification(string title, string message, string? action = null)
    {
        _logger.LogInformation("QuickDock Notification: [{Title}] {Message}", title, message);

        try
        {
            if (_trayHwnd != IntPtr.Zero)
            {
                var nid = new NativeMethods.NOTIFYICONDATA
                {
                    cbSize = Marshal.SizeOf(typeof(NativeMethods.NOTIFYICONDATA)),
                    hWnd = _trayHwnd,
                    uID = _trayIconId,
                    uFlags = NativeMethods.NIF_INFO,
                    szInfoTitle = Truncate(title, 63),
                    szInfo = Truncate(message, 255),
                    dwInfoFlags = NativeMethods.NIIF_INFO,
                    uTimeoutOrVersion = 3000
                };

                NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_MODIFY, ref nid);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to display shell notification.");
        }
    }

    private static string Truncate(string str, int maxLen)
    {
        if (string.IsNullOrEmpty(str)) return string.Empty;
        return str.Length <= maxLen ? str : str[..maxLen];
    }
}
