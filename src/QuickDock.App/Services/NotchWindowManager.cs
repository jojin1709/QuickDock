using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;
using QuickDock.Core.Settings;
using QuickDock.Infrastructure.Windows;

namespace QuickDock.App.Services;

public enum DockInteractionState
{
    Closed,
    Opening,
    Open,
    Closing,
    Pinned
}

public sealed class DockWindowState
{
    public required MainWindow Window { get; init; }
    public required MonitorInfo Monitor { get; set; }
    public required IntPtr HWnd { get; init; }
    public DockInteractionState State { get; set; } = DockInteractionState.Closed;

    public DispatcherTimer? OpenTimer { get; set; }
    public DispatcherTimer? CloseTimer { get; set; }

    public (int X, int Y, int Width, int Height) IdleBounds { get; set; }
    public (int X, int Y, int Width, int Height) ExpandedBounds { get; set; }
    public (int Left, int Right, int Top, int Bottom) TopHitZone { get; set; }
}

public sealed class NotchWindowManager : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMonitorService _monitorService;
    private readonly IWindowPositioningService _positioningService;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly ISettingsManager _settingsManager;
    private readonly ILogger<NotchWindowManager> _logger;
    private readonly DispatcherQueue _dispatcherQueue;

    private readonly Dictionary<string, DockWindowState> _dockStates = new(StringComparer.OrdinalIgnoreCase);
    private bool _initialized;
    private DispatcherTimer? _proximityTimer;

    public NotchWindowManager(
        IServiceProvider serviceProvider,
        IMonitorService monitorService,
        IWindowPositioningService positioningService,
        IGlobalHotkeyService hotkeyService,
        ISettingsManager settingsManager,
        ILogger<NotchWindowManager> logger)
    {
        _serviceProvider = serviceProvider;
        _monitorService = monitorService;
        _positioningService = positioningService;
        _hotkeyService = hotkeyService;
        _settingsManager = settingsManager;
        _logger = logger;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    }

    public IReadOnlyCollection<MainWindow> ActiveWindows => _dockStates.Values.Select(s => s.Window).ToList();

    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        _logger.LogInformation("NotchWindowManager: Initializing multi-monitor dock windows...");
        SynchronizeWindows();
        _monitorService.MonitorsChanged += OnMonitorsChanged;

        StartProximityMonitoring();
    }

    private void StartProximityMonitoring()
    {
        _proximityTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _proximityTimer.Tick += OnProximityTimerTick;
        _proximityTimer.Start();
        _logger.LogInformation("NotchWindowManager: Non-blocking 50ms cursor proximity monitoring started.");
    }

    private void OnProximityTimerTick(object? sender, object e)
    {
        if (!NativeMethods.GetCursorPos(out var pt)) return;

        int px = pt.X;
        int py = pt.Y;

        foreach (var state in _dockStates.Values)
        {
            var mon = state.Monitor;
            bool onThisMonitor = px >= mon.BoundsX && px < (mon.BoundsX + mon.BoundsWidth) &&
                                 py >= mon.BoundsY && py < (mon.BoundsY + mon.BoundsHeight);

            if (state.Window.IsExpanded)
            {
                int margin = (int)Math.Round(30 * mon.ScaleFactor);
                bool insideExpanded = onThisMonitor &&
                                      px >= (state.ExpandedBounds.X - margin) &&
                                      px <= (state.ExpandedBounds.X + state.ExpandedBounds.Width + margin) &&
                                      py >= state.ExpandedBounds.Y &&
                                      py <= (state.ExpandedBounds.Y + state.ExpandedBounds.Height + margin);

                if (!insideExpanded)
                {
                    if (_settingsManager.CurrentSettings.Behavior.AutoHide && state.State == DockInteractionState.Open)
                    {
                        state.State = DockInteractionState.Closing;
                        state.CloseTimer?.Stop();
                        state.CloseTimer?.Start();
                    }
                }
                else
                {
                    if (state.State == DockInteractionState.Closing)
                    {
                        state.State = DockInteractionState.Open;
                        state.CloseTimer?.Stop();
                    }
                }
            }
            else
            {
                bool inHitZone = onThisMonitor &&
                                 px >= state.TopHitZone.Left && px <= state.TopHitZone.Right &&
                                 py >= state.TopHitZone.Top && py <= state.TopHitZone.Bottom;

                if (inHitZone)
                {
                    if (state.State == DockInteractionState.Closed)
                    {
                        state.State = DockInteractionState.Opening;
                        state.OpenTimer?.Stop();
                        state.OpenTimer?.Start();
                    }
                }
                else
                {
                    if (state.State == DockInteractionState.Opening)
                    {
                        state.State = DockInteractionState.Closed;
                        state.OpenTimer?.Stop();
                    }
                }
            }
        }
    }

    public void OnWindowExpanding(MainWindow window)
    {
        CollapseAllExcept(window);
        foreach (var state in _dockStates.Values)
        {
            if (state.Window == window)
            {
                state.State = DockInteractionState.Open;
                state.OpenTimer?.Stop();
                state.CloseTimer?.Stop();
            }
        }
    }

    public void OnWindowCollapsing(MainWindow window)
    {
        foreach (var state in _dockStates.Values)
        {
            if (state.Window == window)
            {
                state.State = DockInteractionState.Closed;
                state.OpenTimer?.Stop();
                state.CloseTimer?.Stop();
            }
        }
    }

    public void CollapseAllExcept(MainWindow? exceptWindow)
    {
        foreach (var state in _dockStates.Values)
        {
            if (state.Window != exceptWindow && state.Window.IsExpanded)
            {
                state.OpenTimer?.Stop();
                state.CloseTimer?.Stop();
                state.State = DockInteractionState.Closed;
                state.Window.Collapse();
            }
        }
    }

    public void ToggleDockOnActiveMonitor()
    {
        var activeMonitor = _monitorService.GetActiveMonitor();
        var key = activeMonitor.DeviceId;

        if (!_dockStates.TryGetValue(key, out var state))
        {
            state = _dockStates.Values.FirstOrDefault();
        }

        if (state != null)
        {
            if (state.Window.IsExpanded)
            {
                state.Window.Collapse();
            }
            else
            {
                CollapseAllExcept(state.Window);
                state.Window.Expand();
            }
        }
    }

    private void OnMonitorsChanged(object? sender, EventArgs e)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            _logger.LogInformation("Display configuration changed. Synchronizing notch windows...");
            SynchronizeWindows();
        });
    }

    public void SynchronizeWindows()
    {
        var monitors = _monitorService.GetAllMonitors();
        _logger.LogInformation("NotchWindowManager: Synchronizing {Count} display monitors.", monitors.Count);

        var currentDeviceIds = new HashSet<string>(monitors.Select(m => m.DeviceId), StringComparer.OrdinalIgnoreCase);

        // Remove windows for disconnected displays
        var removedKeys = _dockStates.Keys.Where(k => !currentDeviceIds.Contains(k)).ToList();
        foreach (var key in removedKeys)
        {
            if (_dockStates.TryGetValue(key, out var state))
            {
                _logger.LogInformation("Closing notch window for disconnected display: {Device}", key);
                _dockStates.Remove(key);
                state.OpenTimer?.Stop();
                state.CloseTimer?.Stop();
                try { state.Window.Close(); } catch { }
            }
        }

        // Add or update windows for connected displays
        foreach (var monitor in monitors)
        {
            if (!_dockStates.TryGetValue(monitor.DeviceId, out var state))
            {
                _logger.LogInformation("Creating notch window for monitor {Device} (Primary={Primary}, Scale={Scale:F2}).", monitor.DeviceId, monitor.IsPrimary, monitor.ScaleFactor);
                try
                {
                    var win = _serviceProvider.GetRequiredService<MainWindow>();
                    win.InitializeForMonitor(monitor, monitor.IsPrimary);

                    IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(win);

                    state = new DockWindowState
                    {
                        Window = win,
                        Monitor = monitor,
                        HWnd = hWnd
                    };

                    SetupStateTimers(state);
                    RecalculateBounds(state);

                    _dockStates[monitor.DeviceId] = state;
                    win.Activate();
                    _logger.LogInformation("Notch window initialized and activated for monitor {Device}.", monitor.DeviceId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create notch window for monitor {Device}.", monitor.DeviceId);
                }
            }
            else
            {
                state.Monitor = monitor;
                state.Window.TargetMonitor = monitor;
                RecalculateBounds(state);
                state.Window.UpdatePlacement();
            }
        }
    }

    public void OnDpiChanged(IntPtr hWnd, MonitorInfo? monitor)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            var match = _dockStates.Values.FirstOrDefault(s => s.HWnd == hWnd);
            if (match != null)
            {
                if (monitor != null)
                {
                    match.Monitor = monitor;
                }
                RecalculateBounds(match);
                match.Window.UpdatePlacement();
            }
        });
    }

    private void SetupStateTimers(DockWindowState state)
    {
        int hoverDelay = Math.Max(80, _settingsManager.CurrentSettings.Behavior.HoverDelayMs);
        state.OpenTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(hoverDelay)
        };
        state.OpenTimer.Tick += (s, e) =>
        {
            state.OpenTimer.Stop();
            if (state.State == DockInteractionState.Opening && !state.Window.IsExpanded)
            {
                state.State = DockInteractionState.Open;
                CollapseAllExcept(state.Window);
                state.Window.Expand();
            }
        };

        state.CloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(180)
        };
        state.CloseTimer.Tick += (s, e) =>
        {
            state.CloseTimer.Stop();
            if (state.State == DockInteractionState.Closing && state.Window.IsExpanded)
            {
                state.State = DockInteractionState.Closed;
                state.Window.Collapse();
            }
        };
    }

    private void RecalculateBounds(DockWindowState state)
    {
        var mon = state.Monitor;
        var (idleW, idleH) = _positioningService.GetDockDimensions(false, mon.ScaleFactor);
        int idleX = mon.WorkX + (mon.WorkWidth - idleW) / 2;
        int idleY = mon.WorkY;
        state.IdleBounds = (idleX, idleY, idleW, idleH);

        var (expW, expH) = _positioningService.GetDockDimensions(true, mon.ScaleFactor);
        int expX = mon.WorkX + (mon.WorkWidth - expW) / 2;
        int expY = mon.WorkY;
        state.ExpandedBounds = (expX, expY, expW, expH);

        int hitToleranceX = (int)Math.Round(40 * mon.ScaleFactor);
        int hitDepth = (int)Math.Round(14 * mon.ScaleFactor);
        int topEdge = Math.Min(mon.BoundsY, mon.WorkY);

        state.TopHitZone = (
            Left: idleX - hitToleranceX,
            Right: idleX + idleW + hitToleranceX,
            Top: topEdge,
            Bottom: idleY + idleH + hitDepth
        );
    }

    public void Dispose()
    {
        _proximityTimer?.Stop();
        _proximityTimer = null;

        foreach (var state in _dockStates.Values)
        {
            state.OpenTimer?.Stop();
            state.CloseTimer?.Stop();
            try { state.Window.Close(); } catch { }
        }
        _dockStates.Clear();
    }
}
