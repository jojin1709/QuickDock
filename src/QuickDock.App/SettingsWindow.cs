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
