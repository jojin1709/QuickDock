using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using QuickDock.App.ViewModels;
using QuickDock.Core.Events;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Settings;
using Windows.System;

namespace QuickDock.App;

public sealed partial class MainWindow : Window
{
    private readonly IWindowPositioningService _positioningService;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly ISystemTrayService _systemTrayService;
    private readonly ISettingsManager _settingsManager;
    private readonly IEventBus _eventBus;
    private readonly ILogger<MainWindow> _logger;

    private readonly IntPtr _hWnd;
    private readonly DispatcherTimer _hoverTimer;
    private readonly DispatcherTimer _tickTimer;
    private int _metricsCounter;

    public MainViewModel ViewModel { get; }

    public MainWindow(
        MainViewModel viewModel,
        IWindowPositioningService positioningService,
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
        _hotkeyService = hotkeyService;
        _systemTrayService = systemTrayService;
        _settingsManager = settingsManager;
        _eventBus = eventBus;

        InitializeComponent();
        _logger.LogInformation("MainWindow InitializeComponent finished.");

        _hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        try
        {
            SystemBackdrop = new MicaBackdrop();
        }
        catch
        {
            // Mica fallback
        }

        // Configure dock window layout and positioning
        _positioningService.ConfigureDockWindow(_hWnd);

        // Hover expand timer (150ms delay)
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

        // Register Global Hotkey (Ctrl + Space)
        _hotkeyService.Register(_hWnd, () =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ViewModel.ToggleExpand();
                BringToFront();
            });
        });

        // Initialize System Tray
        _systemTrayService.Initialize(
            _hWnd,
            onToggleDock: () => DispatcherQueue.TryEnqueue(() =>
            {
                ViewModel.ToggleExpand();
                BringToFront();
            }),
            onOpenSettings: () => DispatcherQueue.TryEnqueue(() =>
            {
                ViewModel.OpenSettings();
                BringToFront();
            }),
            onTogglePerformanceMode: () => DispatcherQueue.TryEnqueue(() =>
            {
                _logger.LogInformation("Performance mode toggled from tray.");
            }),
            onRestart: () => DispatcherQueue.TryEnqueue(RestartApplication),
            onExit: () => DispatcherQueue.TryEnqueue(() =>
            {
                Close();
            })
        );

        ViewModel.DockExpansionStateChanged += OnExpansionStateChanged;

        // Auto-collapse when losing focus
        Activated += OnWindowActivated;
        Closed += OnWindowClosed;

        ApplyTheme(_settingsManager.CurrentSettings.Appearance.Theme);
        _eventBus.Subscribe<SettingsChangedEvent>((e, ct) =>
        {
            DispatcherQueue.TryEnqueue(() => ApplyTheme(e.Settings.Appearance.Theme));
            return Task.CompletedTask;
        });
    }

    private void BringToFront()
    {
        Activate();
        Infrastructure.Windows.NativeMethods.SetForegroundWindow(_hWnd);
    }

    private void OnTickTimer(object? sender, object e)
    {
        ViewModel.Home.UpdateTime(DateTime.Now);

        _metricsCounter++;
        if (_metricsCounter >= 3)
        {
            _metricsCounter = 0;
            ViewModel.Home.RefreshMetrics();
        }
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

        // Resize & reposition window on primary screen
        _positioningService.SetDockState(_hWnd, isExpanded);
        _systemTrayService.UpdateDockState(isExpanded);

        if (isExpanded)
        {
            BringToFront();
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
            ViewModel.Expand();
        }
    }

    private void OnIdlePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _hoverTimer.Stop();
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

    private Visibility IsSettingsVisible(string currentView) =>
        string.Equals(currentView, "Settings", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _tickTimer.Stop();
        _hoverTimer.Stop();
        _hotkeyService.Dispose();
        _systemTrayService.Dispose();
    }
}
