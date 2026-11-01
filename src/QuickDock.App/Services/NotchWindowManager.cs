using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;
using QuickDock.Core.Settings;

namespace QuickDock.App.Services;

/// <summary>
/// Manages independent top-center notch windows across all connected displays.
/// </summary>
public sealed class NotchWindowManager
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMonitorService _monitorService;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly ISettingsManager _settingsManager;
    private readonly ILogger<NotchWindowManager> _logger;
    private readonly DispatcherQueue _dispatcherQueue;

    private readonly Dictionary<string, MainWindow> _windows = new(StringComparer.OrdinalIgnoreCase);
    private bool _initialized;

    public NotchWindowManager(
        IServiceProvider serviceProvider,
        IMonitorService monitorService,
        IGlobalHotkeyService hotkeyService,
        ISettingsManager settingsManager,
        ILogger<NotchWindowManager> logger)
    {
        _serviceProvider = serviceProvider;
        _monitorService = monitorService;
        _hotkeyService = hotkeyService;
        _settingsManager = settingsManager;
        _logger = logger;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    }

    public IReadOnlyCollection<MainWindow> ActiveWindows => _windows.Values;

    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        _logger.LogInformation("NotchWindowManager: Initializing multi-monitor windows...");

        SynchronizeWindows();
        _monitorService.MonitorsChanged += OnMonitorsChanged;
    }

    public void ToggleDockOnActiveMonitor()
    {
        var activeMonitor = _monitorService.GetActiveMonitor();
        var key = activeMonitor.DeviceId;

        MainWindow? target = null;
        if (!_windows.TryGetValue(key, out target))
        {
            target = _windows.Values.FirstOrDefault();
        }

        if (target != null)
        {
            if (target.ViewModel.IsExpanded)
            {
                target.ViewModel.Collapse();
            }
            else
            {
                // Collapse other windows first
                CollapseAllExcept(target);
                target.ViewModel.Expand();
            }
        }
    }

    public void CollapseAllExcept(MainWindow? exceptWindow)
    {
        foreach (var win in _windows.Values)
        {
            if (win != exceptWindow && win.ViewModel.IsExpanded)
            {
                win.ViewModel.Collapse();
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
        _logger.LogInformation("Detected {Count} active monitors.", monitors.Count);

        var currentDeviceIds = new HashSet<string>(monitors.Select(m => m.DeviceId), StringComparer.OrdinalIgnoreCase);

        // Remove windows for disconnected monitors
        var removedKeys = _windows.Keys.Where(k => !currentDeviceIds.Contains(k)).ToList();
        foreach (var key in removedKeys)
        {
            if (_windows.TryGetValue(key, out var win))
            {
                _logger.LogInformation("Closing notch window for disconnected display: {Device}", key);
                _windows.Remove(key);
                try { win.Close(); } catch { }
            }
        }

        // Add or update windows for connected monitors
        foreach (var monitor in monitors)
        {
            if (!_windows.TryGetValue(monitor.DeviceId, out var win))
            {
                _logger.LogInformation("Creating notch window for monitor {Device} (Primary={Primary}).", monitor.DeviceId, monitor.IsPrimary);
                win = _serviceProvider.GetRequiredService<MainWindow>();
                win.TargetMonitor = monitor;
                win.IsPrimaryInstance = monitor.IsPrimary;
                _windows[monitor.DeviceId] = win;
                win.Activate();
            }
            else
            {
                win.TargetMonitor = monitor;
                win.UpdatePlacement();
            }
        }
    }
}
