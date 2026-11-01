using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using QuickDock.App.ViewModels;
using QuickDock.Core.Events;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Settings;
using QuickDock.Infrastructure.Windows;
using Windows.System;

namespace QuickDock.App;

public sealed partial class MainWindow : Window
{
    private static readonly UIntPtr MainWindowSubclassId = new(1001);

    private readonly IWindowPositioningService _positioningService;
    private readonly IMonitorService _monitorService;
    private readonly IFullscreenService _fullscreenService;
    private readonly IStartupService _startupService;
    private readonly INotificationService _notificationService;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly ISystemTrayService _systemTrayService;
    private readonly ISettingsManager _settingsManager;
    private readonly IEventBus _eventBus;
    private readonly ILogger<MainWindow> _logger;

    private readonly IntPtr _hWnd;
    private readonly uint _activateMsg;
    private readonly NativeMethods.SUBCLASSPROC _windowSubclassProc;

    private readonly DispatcherTimer _hoverTimer;
    private readonly DispatcherTimer _tickTimer;
    private readonly DispatcherTimer _monitorFollowTimer;
    private int _metricsCounter;
    private IntPtr _previousForegroundHwnd = IntPtr.Zero;
    private bool _isExplicitExit = false;
    private string? _currentDockMonitorId;

    public MainViewModel ViewModel { get; }

    public MainWindow(
        MainViewModel viewModel,
        IWindowPositioningService positioningService,
        IMonitorService monitorService,
        IFullscreenService fullscreenService,
        IStartupService startupService,
        INotificationService notificationService,
        IGlobalHotkeyService hotkeyService,
        ISystemTrayService systemTrayService,
        ISettingsManager settingsManager,
        IEventBus eventBus,
        ILogger<MainWindow> logger)
    {
        _logger = logger;
        _logger.LogInformation("MainWindow constructor starting...");

        ViewModel = viewModel;
        _positioningService = positioningService;
        _monitorService = monitorService;
        _fullscreenService = fullscreenService;
        _startupService = startupService;
        _notificationService = notificationService;
        _hotkeyService = hotkeyService;
        _systemTrayService = systemTrayService;
        _settingsManager = settingsManager;
        _eventBus = eventBus;

        InitializeComponent();
        _logger.LogInformation("MainWindow InitializeComponent finished.");

        _hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _logger.LogInformation("MainWindow: HWND is {hWnd}.", _hWnd);

        ViewModel.SetDispatcherQueue(DispatcherQueue);

        // Native WinUI 3 Borderless, Titlebarless, AlwaysOnTop OverlappedPresenter
        // Hide completely from taskbar and Alt+Tab switcher
        try
        {
            AppWindow.IsShownInSwitchers = false;
            AppWindow.Closing += (sender, args) =>
            {
                if (!_isExplicitExit)
                {
                    args.Cancel = true;
                    ViewModel.Collapse();
                }
            };

            if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(false, false);
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.IsAlwaysOnTop = _settingsManager.CurrentSettings.Behavior.AlwaysOnTop;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to configure OverlappedPresenter.");
        }

        _monitorService.SetDockHandle(_hWnd);
        var initialMon = _monitorService.GetTargetMonitor(
            _settingsManager.CurrentSettings.Behavior.MonitorTarget,
            _settingsManager.CurrentSettings.Behavior.FollowActiveMonitor);
        _currentDockMonitorId = initialMon.DeviceId;

        _positioningService.ConfigureDockWindow(_hWnd);

        // Register custom activation message for single-instance
        _activateMsg = NativeMethods.RegisterWindowMessageW("QUICKDOCK_ACTIVATE_INSTANCE");
        _windowSubclassProc = WindowSubclassProcedure;
        NativeMethods.SetWindowSubclass(_hWnd, _windowSubclassProc, MainWindowSubclassId, UIntPtr.Zero);

        // Register tray handle for notifications
        if (_notificationService is NotificationService concreteNotificationService)
        {
            concreteNotificationService.RegisterTrayHandle(_hWnd);
        }

        // Hover expand timer (configurable delay)
        _hoverTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(_settingsManager.CurrentSettings.Behavior.HoverDelayMs)
        };
        _hoverTimer.Tick += OnHoverTimerTick;

        // Periodic clock & system metrics refresh timer
        _tickTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _tickTimer.Tick += OnTickTimer;
        _tickTimer.Start();

        // Multi-monitor cursor tracking timer (smoothly moves dock across screens)
        _monitorFollowTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _monitorFollowTimer.Tick += OnMonitorFollowTick;
        _monitorFollowTimer.Start();

        // Register Global Hotkey (Ctrl + Space)
        _hotkeyService.Register(_hWnd, () =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (ViewModel.IsExpanded)
                {
                    ViewModel.Collapse();
                }
                else
                {
                    CapturePreviousForeground();
                    ViewModel.Expand();
                    BringToFront();
                }
            });
        });

        // Initialize System Tray
        _systemTrayService.Initialize(
            _hWnd,
            onToggleDock: () => DispatcherQueue.TryEnqueue(() =>
            {
                if (ViewModel.IsExpanded)
                {
                    ViewModel.Collapse();
                }
                else
                {
                    CapturePreviousForeground();
                    ViewModel.Expand();
                    BringToFront();
                }
            }),
            onOpenSettings: () => DispatcherQueue.TryEnqueue(() =>
            {
                CapturePreviousForeground();
                ViewModel.OpenSettings();
                BringToFront();
            }),
            onToggleStartup: () => DispatcherQueue.TryEnqueue(() =>
            {
                bool cur = _startupService.IsStartupEnabled();
                _startupService.SetStartupEnabled(!cur);
                _settingsManager.CurrentSettings.Behavior.StartWithWindows = !cur;
                _ = _settingsManager.SaveAsync(_settingsManager.CurrentSettings);
            }),
            isStartupEnabled: () => _startupService.IsStartupEnabled(),
            onTogglePerformanceMode: () => DispatcherQueue.TryEnqueue(() =>
            {
                _logger.LogInformation("Performance mode toggled from tray.");
            }),
            onAbout: () => DispatcherQueue.TryEnqueue(() =>
            {
                CapturePreviousForeground();
                ViewModel.OpenSettings();
                BringToFront();
            }),
            onRestart: () => DispatcherQueue.TryEnqueue(RestartApplication),
            onExit: () => DispatcherQueue.TryEnqueue(() =>
            {
                _isExplicitExit = true;
                Close();
            })
        );

        // Fullscreen detection
        if (_fullscreenService is FullscreenDetectionService concreteFullscreenService)
        {
            concreteFullscreenService.SetDockHandle(_hWnd);
        }
        _fullscreenService.FullscreenStateChanged += OnFullscreenStateChanged;
        _fullscreenService.StartMonitoring();

        ViewModel.DockExpansionStateChanged += OnExpansionStateChanged;

        // Auto-collapse when losing focus
        Activated += OnWindowActivated;
        Closed += OnWindowClosed;

        ApplyTheme(_settingsManager.CurrentSettings.Appearance.Theme);
        ApplyBackdrop(_settingsManager.CurrentSettings.Appearance.Backdrop);
        _eventBus.Subscribe<SettingsChangedEvent>((e, ct) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ApplyTheme(e.Settings.Appearance.Theme);
                ApplyBackdrop(e.Settings.Appearance.Backdrop);
                _positioningService.SetAlwaysOnTop(_hWnd, e.Settings.Behavior.AlwaysOnTop);
                _positioningService.Reposition(_hWnd, ViewModel.IsExpanded);
            });
            return Task.CompletedTask;
        });
    }

    private IntPtr WindowSubclassProcedure(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, UIntPtr dwRefData)
    {
        if (_activateMsg != 0 && uMsg == _activateMsg)
        {
            _logger.LogInformation("Activation message received from another QuickDock process.");
            DispatcherQueue.TryEnqueue(() =>
            {
                CapturePreviousForeground();
                if (!ViewModel.IsExpanded)
                {
                    ViewModel.Expand();
                }
                BringToFront();
            });
            return IntPtr.Zero;
        }

        if (uMsg == NativeMethods.WM_DPICHANGED)
        {
            _logger.LogInformation("WM_DPICHANGED received, updating dock dimensions and positioning.");
            DispatcherQueue.TryEnqueue(() =>
            {
                _positioningService.Reposition(_hWnd, ViewModel.IsExpanded);
            });
            return IntPtr.Zero;
        }

        return NativeMethods.DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    private void CapturePreviousForeground()
    {
        IntPtr fg = NativeMethods.GetForegroundWindow();
        if (fg != IntPtr.Zero && fg != _hWnd)
        {
            _previousForegroundHwnd = fg;
        }
    }

    private void RestorePreviousForeground()
    {
        if (_previousForegroundHwnd != IntPtr.Zero)
        {
            NativeMethods.SetForegroundWindow(_previousForegroundHwnd);
            _previousForegroundHwnd = IntPtr.Zero;
        }
    }

    private void BringToFront()
    {
        Activate();
        NativeMethods.SetForegroundWindow(_hWnd);
    }

    private void OnTickTimer(object? sender, object e)
    {
        ViewModel.Home.UpdateTime(DateTime.Now);

        if (ViewModel.IsExpanded)
        {
            _metricsCounter++;
            if (_metricsCounter >= 3)
            {
                _metricsCounter = 0;
                ViewModel.Home.RefreshMetrics();
            }
        }
    }

    private void OnFullscreenStateChanged(object? sender, bool isFullscreen)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_settingsManager.CurrentSettings.Behavior.HideInFullscreen)
            {
                if (isFullscreen)
                {
                    _logger.LogInformation("Fullscreen application active. Hiding QuickDock visually.");
                    if (ViewModel.IsExpanded)
                    {
                        ViewModel.Collapse();
                    }
                    RootWindowGrid.Opacity = 0;
                    RootWindowGrid.IsHitTestVisible = false;
                }
                else
                {
                    _logger.LogInformation("Fullscreen application exited. Restoring QuickDock visually.");
                    RootWindowGrid.Opacity = 1;
                    RootWindowGrid.IsHitTestVisible = true;
                    _positioningService.Reposition(_hWnd, ViewModel.IsExpanded);
                }
            }
        });
    }

    private void OnExpansionStateChanged(bool isExpanded)
    {
        _hoverTimer.Stop();

        // Update UI visibility
        if (isExpanded)
        {
            IdleDockBorder.Visibility = Visibility.Collapsed;
            ExpandedPanelBorder.Visibility = Visibility.Visible;
        }
        else
        {
            ExpandedPanelBorder.Visibility = Visibility.Collapsed;
            IdleDockBorder.Visibility = Visibility.Visible;
        }

        // Resize & reposition window on targeted monitor
        _positioningService.SetDockState(_hWnd, isExpanded);
        _systemTrayService.UpdateDockState(isExpanded);

        if (isExpanded)
        {
            _metricsCounter = 0;
            ViewModel.Home.RefreshMetrics();
            BringToFront();
        }
        else
        {
            RestorePreviousForeground();
        }
    }

    private void OnIdlePointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (!ViewModel.IsExpanded)
        {
            _hoverTimer.Stop();
            _hoverTimer.Interval = TimeSpan.FromMilliseconds(_settingsManager.CurrentSettings.Behavior.HoverDelayMs);
            _hoverTimer.Start();
        }
    }

    private void OnIdlePointerExited(object sender, PointerRoutedEventArgs e)
    {
        _hoverTimer.Stop();
    }

    private void OnHoverTimerTick(object? sender, object e)
    {
        _hoverTimer.Stop();
        if (!ViewModel.IsExpanded)
        {
            CapturePreviousForeground();
            ViewModel.Expand();
        }
    }

    private void OnIdlePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _hoverTimer.Stop();
        CapturePreviousForeground();
        ViewModel.Expand();
    }

    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape && ViewModel.IsExpanded)
        {
            ViewModel.Collapse();
            e.Handled = true;
        }
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            if (ViewModel.IsExpanded && _settingsManager.CurrentSettings.Behavior.AutoHide)
            {
                ViewModel.Collapse();
            }
        }
    }

    private void ApplyTheme(AppTheme theme)
    {
        if (Content is FrameworkElement root)
        {
            root.RequestedTheme = theme switch
            {
                AppTheme.Light => ElementTheme.Light,
                AppTheme.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }
    }

    private void ApplyBackdrop(BackdropType backdrop)
    {
        try
        {
            SystemBackdrop = backdrop switch
            {
                BackdropType.Acrylic => new DesktopAcrylicBackdrop(),
                BackdropType.Solid => null,
                _ => new MicaBackdrop()
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Backdrop fallback applied.");
        }
    }

    private void RestartApplication()
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (exe != null)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exe,
                    UseShellExecute = true
                });
            }
            Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart application.");
        }
    }

    private Visibility IsHomeVisible(string currentView) =>
        string.Equals(currentView, "Home", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;

    private Visibility IsSystemVisible(string currentView) =>
        string.Equals(currentView, "System", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;

    private Visibility IsSettingsVisible(string currentView) =>
        string.Equals(currentView, "Settings", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;

    private Visibility IsClipboardVisible(string currentView) =>
        string.Equals(currentView, "Clipboard", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;

    private Visibility IsTimerVisible(string currentView) =>
        string.Equals(currentView, "Timer", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;

    private Visibility IsNotesVisible(string currentView) =>
        string.Equals(currentView, "Notes", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;

    private Visibility IsCalendarVisible(string currentView) =>
        string.Equals(currentView, "Calendar", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;

    private Visibility IsWeatherVisible(string currentView) =>
        string.Equals(currentView, "Weather", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;

    private Visibility IsLinksVisible(string currentView) =>
        string.Equals(currentView, "Links", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;

    private Visibility IsScreenTimeVisible(string currentView) =>
        string.Equals(currentView, "ScreenTime", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;

    private Visibility IsFileShelfVisible(string currentView) =>
        string.Equals(currentView, "FileShelf", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;

    private Visibility IsQuickDropVisible(string currentView) =>
        string.Equals(currentView, "QuickDrop", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;

    private Visibility IsConverterVisible(string currentView) =>
        string.Equals(currentView, "Converter", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;

    private Visibility IsTransfersVisible(string currentView) =>
        string.Equals(currentView, "Transfers", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;

    private Visibility IsDeveloperVisible(string currentView) =>
        string.Equals(currentView, "Developer", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;

    private Visibility IsNetworkSecurityVisible(string currentView) =>
        string.Equals(currentView, "NetworkSecurity", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;

    public Brush GetNavBackground(string currentView, string targetView)
    {
        bool isActive = string.Equals(currentView, targetView, StringComparison.OrdinalIgnoreCase);
        return new SolidColorBrush(isActive ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Transparent);
    }

    public Brush GetNavForeground(string currentView, string targetView)
    {
        bool isActive = string.Equals(currentView, targetView, StringComparison.OrdinalIgnoreCase);
        return new SolidColorBrush(isActive ? Microsoft.UI.Colors.Black : Windows.UI.Color.FromArgb(255, 161, 161, 170));
    }

    private void OnMonitorFollowTick(object? sender, object e)
    {
        if (!ViewModel.IsExpanded && _settingsManager.CurrentSettings.Behavior.FollowActiveMonitor)
        {
            var activeMonitor = _monitorService.GetActiveMonitor();
            if (_currentDockMonitorId != null &&
                !string.Equals(_currentDockMonitorId, activeMonitor.DeviceId, StringComparison.OrdinalIgnoreCase))
            {
                _currentDockMonitorId = activeMonitor.DeviceId;
                _positioningService.Reposition(_hWnd, isExpanded: false);
            }
        }
    }

    private Visibility IsBackVisible(string currentView) =>
        string.Equals(currentView, "Home", StringComparison.OrdinalIgnoreCase) ? Visibility.Collapsed : Visibility.Visible;

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _tickTimer.Stop();
        _hoverTimer.Stop();
        _monitorFollowTimer.Stop();
        _fullscreenService.StopMonitoring();
        _fullscreenService.Dispose();
        _hotkeyService.Dispose();
        _systemTrayService.Dispose();
        NativeMethods.RemoveWindowSubclass(_hWnd, _windowSubclassProc, MainWindowSubclassId);
    }
}
