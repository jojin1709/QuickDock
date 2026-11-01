using System;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using QuickDock.App.ViewModels;
using QuickDock.App.Views;
using WinRT.Interop;

namespace QuickDock.App;

public sealed class SettingsWindow : Window
{
    public SettingsViewModel ViewModel { get; }

    public SettingsWindow(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        Title = "QuickDock Settings";

        Content = new SettingsView
        {
            ViewModel = viewModel
        };

        var hWnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        if (appWindow != null)
        {
            appWindow.Title = "QuickDock Settings";
            if (appWindow.TitleBar != null)
            {
                var darkBg = Windows.UI.Color.FromArgb(255, 11, 11, 13);
                var whiteFg = Windows.UI.Color.FromArgb(255, 255, 255, 255);
                var hoverBg = Windows.UI.Color.FromArgb(255, 23, 23, 26);
                var grayFg = Windows.UI.Color.FromArgb(255, 142, 142, 147);

                appWindow.TitleBar.BackgroundColor = darkBg;
                appWindow.TitleBar.ForegroundColor = whiteFg;
                appWindow.TitleBar.ButtonBackgroundColor = darkBg;
                appWindow.TitleBar.ButtonForegroundColor = whiteFg;
                appWindow.TitleBar.ButtonHoverBackgroundColor = hoverBg;
                appWindow.TitleBar.ButtonHoverForegroundColor = whiteFg;
                appWindow.TitleBar.InactiveBackgroundColor = darkBg;
                appWindow.TitleBar.InactiveForegroundColor = grayFg;
            }

            appWindow.Resize(new Windows.Graphics.SizeInt32(960, 680));

            // Center window on screen
            var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
            if (displayArea != null)
            {
                var centeredX = displayArea.WorkArea.X + (displayArea.WorkArea.Width - 960) / 2;
                var centeredY = displayArea.WorkArea.Y + (displayArea.WorkArea.Height - 680) / 2;
                appWindow.Move(new Windows.Graphics.PointInt32(centeredX, centeredY));
            }
        }
    }
}

