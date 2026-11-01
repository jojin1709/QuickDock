using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using QuickDock.App.Services;
using QuickDock.App.ViewModels;
using QuickDock.Core.Events;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;
using QuickDock.Core.Settings;
using QuickDock.Infrastructure.Windows;
using Windows.System;

namespace QuickDock.App;

public sealed partial class MainWindow : Window
{
    private static readonly UIntPtr MainWindowSubclassId = new(2001);

    private readonly IServiceProvider _serviceProvider;
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
    private readonly DispatcherTimer _tickTimer;

    private Storyboard? _expandStoryboard;
    private Storyboard? _collapseStoryboard;
    private DispatcherTimer? _clipboardPillTimer;

    private int _metricsCounter;
    private IntPtr _previousForegroundHwnd = IntPtr.Zero;
    private bool _isExplicitExit;

    public MainViewModel ViewModel { get; }
    public bool IsExpanded { get; private set; }

    private MonitorInfo? _targetMonitor;
    public MonitorInfo? TargetMonitor
    {
        get => _targetMonitor;
        set
        {
            _targetMonitor = value;
            if (_hWnd != IntPtr.Zero && value != null)
            {
                _positioningService.SetDockState(_hWnd, IsExpanded, value);
            }
        }
    }

    public bool IsPrimaryInstance { get; set; } = true;

    public void UpdatePlacement()
    {
        if (_hWnd != IntPtr.Zero && TargetMonitor != null)
        {
            _positioningService.SetDockState(_hWnd, IsExpanded, TargetMonitor);
        }
    }

    public MainWindow(
        IServiceProvider serviceProvider,
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

        _serviceProvider = serviceProvider;
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

        try
        {
            InitializeComponent();
            _logger.LogInformation("MainWindow InitializeComponent finished.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Exception during MainWindow InitializeComponent!");
            throw;
        }

        _hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _logger.LogInformation("MainWindow: HWND is {hWnd}.", _hWnd);

        ViewModel.SetDispatcherQueue(DispatcherQueue);

        // Native WinUI 3 Borderless, Titlebarless, AlwaysOnTop OverlappedPresenter
        // Hide completely from taskbar and Alt+Tab switcher
        try
        {
            AppWindow.IsShownInSwitchers = false;
        }
        catch
        {
            // IsShownInSwitchers throws E_NOTIMPL on some Windows App SDK versions; WS_EX_TOOLWINDOW in positioning service handles this natively
        }

        try
        {
            AppWindow.Closing += (sender, args) =>
            {
                _logger.LogInformation("AppWindow.Closing fired! ExplicitExit={Exit}", _isExplicitExit);
                if (!_isExplicitExit)
                {
                    args.Cancel = true;
                    Collapse();
                }
            };

            AppWindow.Destroying += (sender, args) =>
            {
                _logger.LogInformation("AppWindow.Destroying fired on HWND {hWnd}!", _hWnd);
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

        // Register custom activation message for single-instance
        _activateMsg = NativeMethods.RegisterWindowMessageW("QUICKDOCK_ACTIVATE_INSTANCE");
        _windowSubclassProc = WindowSubclassProcedure;
        NativeMethods.SetWindowSubclass(_hWnd, _windowSubclassProc, MainWindowSubclassId, UIntPtr.Zero);

        // Setup fluid animations and clipboard toast notification
        SetupAnimations();
        SetupClipboardNotification();

        // Periodic clock & system metrics refresh timer
        _tickTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _tickTimer.Tick += OnTickTimer;
        _tickTimer.Start();

        // Fullscreen detection
        if (_fullscreenService is FullscreenDetectionService concreteFullscreenService)
        {
            concreteFullscreenService.SetDockHandle(_hWnd);
        }
        _fullscreenService.FullscreenStateChanged += OnFullscreenStateChanged;
        _fullscreenService.StartMonitoring();

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
                _positioningService.Reposition(_hWnd, IsExpanded, TargetMonitor);
            });
            return Task.CompletedTask;
        });
    }

    public void InitializeForMonitor(MonitorInfo monitor, bool isPrimary)
    {
        TargetMonitor = monitor;
        IsPrimaryInstance = isPrimary;

        _positioningService.ConfigureDockWindow(_hWnd, monitor);

        if (isPrimary)
        {
            NativeMethods.AddClipboardFormatListener(_hWnd);

            if (_notificationService is NotificationService concreteNotificationService)
            {
                concreteNotificationService.RegisterTrayHandle(_hWnd);
            }

            _hotkeyService.Register(_hWnd, () =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    _serviceProvider.GetService<NotchWindowManager>()?.ToggleDockOnActiveMonitor();
                });
            });

            _systemTrayService.Initialize(
                _hWnd,
                onToggleDock: () => DispatcherQueue.TryEnqueue(() =>
                {
                    _serviceProvider.GetService<NotchWindowManager>()?.ToggleDockOnActiveMonitor();
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
        }
    }

    private void SetupAnimations()
    {
        // 1. Expand Storyboard (180ms CubicEase)
        _expandStoryboard = new Storyboard();

        var expandWidthAnim = new DoubleAnimation
        {
            Duration = new Duration(TimeSpan.FromMilliseconds(180)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(expandWidthAnim, DockSurface);
        Storyboard.SetTargetProperty(expandWidthAnim, "Width");
        _expandStoryboard.Children.Add(expandWidthAnim);

        var expandHeightAnim = new DoubleAnimation
        {
            Duration = new Duration(TimeSpan.FromMilliseconds(180)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(expandHeightAnim, DockSurface);
        Storyboard.SetTargetProperty(expandHeightAnim, "Height");
        _expandStoryboard.Children.Add(expandHeightAnim);

        var idleFadeOut = new DoubleAnimation
        {
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(70)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(idleFadeOut, IdleContentGrid);
        Storyboard.SetTargetProperty(idleFadeOut, "Opacity");
        _expandStoryboard.Children.Add(idleFadeOut);

        var expandedFadeIn = new DoubleAnimation
        {
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(180)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(expandedFadeIn, ExpandedContentGrid);
        Storyboard.SetTargetProperty(expandedFadeIn, "Opacity");
        _expandStoryboard.Children.Add(expandedFadeIn);

        _expandStoryboard.Completed += (s, e) =>
        {
            IdleContentGrid.Visibility = Visibility.Collapsed;
        };

        // 2. Collapse Storyboard (180ms CubicEase)
        _collapseStoryboard = new Storyboard();

        var collapseWidthAnim = new DoubleAnimation
        {
            Duration = new Duration(TimeSpan.FromMilliseconds(180)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(collapseWidthAnim, DockSurface);
        Storyboard.SetTargetProperty(collapseWidthAnim, "Width");
        _collapseStoryboard.Children.Add(collapseWidthAnim);

        var collapseHeightAnim = new DoubleAnimation
        {
            Duration = new Duration(TimeSpan.FromMilliseconds(180)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(collapseHeightAnim, DockSurface);
        Storyboard.SetTargetProperty(collapseHeightAnim, "Height");
        _collapseStoryboard.Children.Add(collapseHeightAnim);

        var expandedFadeOut = new DoubleAnimation
        {
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(70)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(expandedFadeOut, ExpandedContentGrid);
        Storyboard.SetTargetProperty(expandedFadeOut, "Opacity");
        _collapseStoryboard.Children.Add(expandedFadeOut);

        var idleFadeIn = new DoubleAnimation
        {
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(180)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(idleFadeIn, IdleContentGrid);
        Storyboard.SetTargetProperty(idleFadeIn, "Opacity");
        _collapseStoryboard.Children.Add(idleFadeIn);

        _collapseStoryboard.Completed += (s, e) =>
        {
            ExpandedContentGrid.Visibility = Visibility.Collapsed;
            PerimeterHighlight.Opacity = 0;
            // Shrink physical HWND down to idle compact footprint so desktop receives clicks
            _positioningService.SetDockState(_hWnd, isExpanded: false, TargetMonitor);
        };
    }

    private void SetupClipboardNotification()
    {
        var clipboardService = _serviceProvider.GetService<IClipboardService>();
        if (clipboardService != null)
        {
            clipboardService.ClipboardItemAdded += (s, item) =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (IsExpanded) return; // Never cover expanded view

                    ClipboardNotificationText.Text = string.IsNullOrWhiteSpace(item.Preview) ? "Copied" : item.Preview.Trim();
                    ClipboardNotificationPill.Visibility = Visibility.Visible;
                    ClipboardNotificationPill.Opacity = 1;

                    _clipboardPillTimer?.Stop();
                    _clipboardPillTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.2) };
                    _clipboardPillTimer.Tick += (ts, te) =>
                    {
                        _clipboardPillTimer.Stop();
                        ClipboardNotificationPill.Opacity = 0;
                        ClipboardNotificationPill.Visibility = Visibility.Collapsed;
                    };
                    _clipboardPillTimer.Start();
                });
            };
        }
    }

    public void Expand()
    {
        if (IsExpanded) return;
        IsExpanded = true;
        _logger.LogInformation("Notch window expanded on monitor {Monitor}", TargetMonitor?.DeviceName ?? "Default");

        _collapseStoryboard?.Stop();

        // Enlarge HWND physically with SWP_NOACTIVATE before animating
        _positioningService.SetDockState(_hWnd, isExpanded: true, TargetMonitor);

        IdleContentGrid.Visibility = Visibility.Visible;
        ExpandedContentGrid.Visibility = Visibility.Visible;

        double currentW = DockSurface.ActualWidth > 0 ? DockSurface.ActualWidth : (double.IsNaN(DockSurface.Width) ? 290 : DockSurface.Width);
        double currentH = DockSurface.ActualHeight > 0 ? DockSurface.ActualHeight : (double.IsNaN(DockSurface.Height) ? 34 : DockSurface.Height);

        if (_expandStoryboard?.Children[0] is DoubleAnimation wAnim)
        {
            wAnim.From = currentW;
            wAnim.To = 820;
        }
        if (_expandStoryboard?.Children[1] is DoubleAnimation hAnim)
        {
            hAnim.From = currentH;
            hAnim.To = 540;
        }

        _expandStoryboard?.Begin();
        StartPerimeterGlow(0.9, 180);

        if (IsPrimaryInstance)
        {
            _systemTrayService.UpdateDockState(true);
        }

        _serviceProvider.GetService<NotchWindowManager>()?.OnWindowExpanding(this);

        _metricsCounter = 0;
        _ = Task.Run(() => ViewModel.Home.RefreshMetrics());
    }

    public void Collapse()
    {
        if (!IsExpanded) return;
        IsExpanded = false;
        _logger.LogInformation("Notch window collapsed on monitor {Monitor}", TargetMonitor?.DeviceName ?? "Default");

        _expandStoryboard?.Stop();

        IdleContentGrid.Visibility = Visibility.Visible;
        ExpandedContentGrid.Visibility = Visibility.Visible;

        double currentW = DockSurface.ActualWidth > 0 ? DockSurface.ActualWidth : (double.IsNaN(DockSurface.Width) ? 820 : DockSurface.Width);
        double currentH = DockSurface.ActualHeight > 0 ? DockSurface.ActualHeight : (double.IsNaN(DockSurface.Height) ? 540 : DockSurface.Height);

        if (_collapseStoryboard?.Children[0] is DoubleAnimation wAnim)
        {
            wAnim.From = currentW;
            wAnim.To = 290;
        }
        if (_collapseStoryboard?.Children[1] is DoubleAnimation hAnim)
        {
            hAnim.From = currentH;
            hAnim.To = 34;
        }

        _collapseStoryboard?.Begin();

        if (IsPrimaryInstance)
        {
            _systemTrayService.UpdateDockState(false);
        }

        _serviceProvider.GetService<NotchWindowManager>()?.OnWindowCollapsing(this);

        RestorePreviousForeground();
    }

    public void ToggleExpand()
    {
        if (IsExpanded) Collapse();
        else Expand();
    }

    private IntPtr WindowSubclassProcedure(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, UIntPtr dwRefData)
    {
        if (_activateMsg != 0 && uMsg == _activateMsg)
        {
            _logger.LogInformation("Activation message received from another QuickDock process.");
            DispatcherQueue.TryEnqueue(() =>
            {
                CapturePreviousForeground();
                Expand();
                BringToFront();
            });
            return IntPtr.Zero;
        }

        if (uMsg == NativeMethods.WM_CLIPBOARDUPDATE)
        {
            DispatcherQueue.TryEnqueue(async () =>
            {
                var clip = _serviceProvider.GetService<IClipboardService>();
                if (clip != null)
                {
                    await clip.ProcessCurrentClipboardAsync();
                }
            });
            return IntPtr.Zero;
        }

        if (uMsg == NativeMethods.WM_DPICHANGED)
        {
            _logger.LogInformation("WM_DPICHANGED received, updating dock dimensions and positioning.");
            DispatcherQueue.TryEnqueue(() =>
            {
                _serviceProvider.GetService<NotchWindowManager>()?.OnDpiChanged(_hWnd, TargetMonitor);
            });
            return IntPtr.Zero;
        }

        if (uMsg == NativeMethods.WM_DISPLAYCHANGE)
        {
            _logger.LogInformation("Display topology changed (uMsg=0x{Msg:X}). Reconciling monitors...", uMsg);
            DispatcherQueue.TryEnqueue(() =>
            {
                _serviceProvider.GetService<NotchWindowManager>()?.SynchronizeWindows();
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

        if (IsExpanded)
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
                    if (IsExpanded)
                    {
                        Collapse();
                    }
                    RootWindowGrid.Opacity = 0;
                    RootWindowGrid.IsHitTestVisible = false;
                }
                else
                {
                    _logger.LogInformation("Fullscreen application exited. Restoring QuickDock visually.");
                    RootWindowGrid.Opacity = 1;
                    RootWindowGrid.IsHitTestVisible = true;
                    _positioningService.Reposition(_hWnd, isExpanded: false, TargetMonitor);
                }
            }
        });
    }

    private void OnIdlePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        CapturePreviousForeground();
        BringToFront();
        Expand();
    }

    private void OnCloseButtonClick(object sender, RoutedEventArgs e)
    {
        Collapse();
    }

    private void StartPerimeterGlow(double targetOpacity, int durationMs = 180)
    {
        try
        {
            var anim = new DoubleAnimation
            {
                To = targetOpacity,
                Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(anim, PerimeterHighlight);
            Storyboard.SetTargetProperty(anim, "Opacity");
            var sb = new Storyboard();
            sb.Children.Add(anim);
            sb.Begin();
        }
        catch
        {
            PerimeterHighlight.Opacity = targetOpacity;
        }
    }

    private void OnIdlePointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (!IsExpanded)
        {
            StartPerimeterGlow(0.85, 180);
        }
    }

    private void OnIdlePointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!IsExpanded)
        {
            StartPerimeterGlow(0.0, 200);
        }
    }

    private void OnExpandedPointerEntered(object sender, PointerRoutedEventArgs e) { }
    private void OnExpandedPointerExited(object sender, PointerRoutedEventArgs e) { }

    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape && IsExpanded)
        {
            Collapse();
            e.Handled = true;
        }
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            if (IsExpanded && _settingsManager.CurrentSettings.Behavior.AutoHide)
            {
                Collapse();
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

    private Visibility IsBackVisible(string currentView) =>
        string.Equals(currentView, "Home", StringComparison.OrdinalIgnoreCase) ? Visibility.Collapsed : Visibility.Visible;

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _logger.LogInformation("MainWindow.Closed event fired on HWND {hWnd}.", _hWnd);
        _tickTimer.Stop();
        _expandStoryboard?.Stop();
        _collapseStoryboard?.Stop();
        _clipboardPillTimer?.Stop();
        _fullscreenService.StopMonitoring();
        _fullscreenService.Dispose();
        if (IsPrimaryInstance)
        {
            NativeMethods.RemoveClipboardFormatListener(_hWnd);
            _hotkeyService.Dispose();
            _systemTrayService.Dispose();
        }
        NativeMethods.RemoveWindowSubclass(_hWnd, _windowSubclassProc, MainWindowSubclassId);
    }
}
