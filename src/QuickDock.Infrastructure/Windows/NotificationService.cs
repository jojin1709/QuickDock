using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Settings;

namespace QuickDock.Infrastructure.Windows;

public sealed class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;
    private readonly ISettingsManager? _settingsManager;
    private IntPtr _trayHwnd = IntPtr.Zero;
    private int _trayIconId = 1;

    public NotificationService(ILogger<NotificationService> logger, ISettingsManager? settingsManager = null)
    {
        _logger = logger;
        _settingsManager = settingsManager;
    }

    public void RegisterTrayHandle(IntPtr hWnd, int iconId = 1)
    {
        _trayHwnd = hWnd;
        _trayIconId = iconId;
    }

    public void ShowNotification(string title, string message, string? action = null)
    {
        if (_settingsManager != null)
        {
            var notif = _settingsManager.CurrentSettings.Notifications;
            if (!notif.Enabled)
            {
                _logger.LogDebug("Notification suppressed (global notifications disabled): [{Title}] {Message}", title, message);
                return;
            }

            if (title.Contains("Timer", StringComparison.OrdinalIgnoreCase) && !notif.TimerNotifications)
                return;
            if (title.Contains("Pomodoro", StringComparison.OrdinalIgnoreCase) && !notif.PomodoroNotifications)
                return;
            if (title.Contains("QuickDrop", StringComparison.OrdinalIgnoreCase) && !notif.QuickDropNotifications)
                return;
            if (title.Contains("Transfer", StringComparison.OrdinalIgnoreCase) && !notif.TransferNotifications)
                return;
            if (title.Contains("Security", StringComparison.OrdinalIgnoreCase) && !notif.SecurityNotifications)
                return;
        }

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

