using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Events;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Settings;
using QuickDock.Infrastructure.Storage;

namespace QuickDock.App.ViewModels;

public sealed partial class ModuleToggleItem : ObservableObject
{
    public string Id { get; }
    public string Name { get; }
    public string Category { get; }

    [ObservableProperty]
    private bool _isEnabled;

    public ModuleToggleItem(string id, string name, string category, bool isEnabled)
    {
        Id = id;
        Name = name;
        Category = category;
        _isEnabled = isEnabled;
    }
}

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsManager _settingsManager;
    private readonly IStartupService _startupService;
    private readonly IMonitorService _monitorService;
    private readonly IEventBus _eventBus;
    private readonly ILogger<SettingsViewModel> _logger;

    private bool _isLoading;

    [ObservableProperty]
    private int _selectedThemeIndex; // 0 = System, 1 = Light, 2 = Dark

    [ObservableProperty]
    private string _globalHotkey = "Ctrl+Space";

    [ObservableProperty]
    private int _hoverDelayMs = 150;

    [ObservableProperty]
    private bool _autoHide = true;

    [ObservableProperty]
    private bool _alwaysOnTop = true;

    [ObservableProperty]
    private bool _hideInFullscreen = true;

    [ObservableProperty]
    private int _selectedMonitorIndex = 0;

    [ObservableProperty]
    private bool _startWithWindows = false;

    [ObservableProperty]
    private string _appVersion = "1.0.0 (Phase 3 Integration)";

    [ObservableProperty]
    private string _dotNetVersion = Environment.Version.ToString();

    [ObservableProperty]
    private string _storagePath = AppStoragePaths.ConfigFile;

    public ObservableCollection<string> AvailableMonitors { get; } = [];
    public ObservableCollection<ModuleToggleItem> ModuleToggles { get; } = [];

    public SettingsViewModel(
        ISettingsManager settingsManager,
        IStartupService startupService,
        IMonitorService monitorService,
        IEventBus eventBus,
        ILogger<SettingsViewModel> logger)
    {
        _settingsManager = settingsManager;
        _startupService = startupService;
        _monitorService = monitorService;
        _eventBus = eventBus;
        _logger = logger;

        LoadSettings();
    }

    public void LoadSettings()
    {
        _isLoading = true;
        try
        {
            var settings = _settingsManager.CurrentSettings;

            SelectedThemeIndex = settings.Appearance.Theme switch
            {
                AppTheme.Light => 1,
                AppTheme.Dark => 2,
                _ => 0
            };

            GlobalHotkey = settings.Behavior.GlobalHotkey;
            HoverDelayMs = settings.Behavior.HoverDelayMs;
            AutoHide = settings.Behavior.AutoHide;
            AlwaysOnTop = settings.Behavior.AlwaysOnTop;
            HideInFullscreen = settings.Behavior.HideInFullscreen;

            // Load Windows startup state
            StartWithWindows = _startupService.IsStartupEnabled();

            // Populate monitor options
            AvailableMonitors.Clear();
            AvailableMonitors.Add("Follow Active Monitor");
            AvailableMonitors.Add("Primary Monitor");

            var monitors = _monitorService.GetAllMonitors();
            for (int i = 0; i < monitors.Count; i++)
            {
                AvailableMonitors.Add(monitors[i].DeviceName);
            }

            if (settings.Behavior.FollowActiveMonitor)
            {
                SelectedMonitorIndex = 0;
            }
            else if (string.Equals(settings.Behavior.MonitorTarget, "Primary", StringComparison.OrdinalIgnoreCase))
            {
                SelectedMonitorIndex = 1;
            }
            else
            {
                int matchIdx = -1;
                for (int i = 0; i < monitors.Count; i++)
                {
                    if (string.Equals(monitors[i].DeviceId, settings.Behavior.MonitorTarget, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(monitors[i].DeviceName, settings.Behavior.MonitorTarget, StringComparison.OrdinalIgnoreCase))
                    {
                        matchIdx = i + 2; // offset by Active and Primary
                        break;
                    }
                }
                SelectedMonitorIndex = matchIdx >= 0 ? matchIdx : 0;
            }

            ModuleToggles.Clear();
            var catalog = new (string Id, string Name, string Category)[]
            {
                ("quickdock.clock", "Clock & Timer", "Productivity"),
                ("quickdock.clipboard", "Clipboard Manager", "Productivity"),
                ("quickdock.notes", "Quick Notes", "Productivity"),
                ("quickdock.system", "System Controls", "System"),
                ("quickdock.volume", "Volume Control", "System"),
                ("quickdock.quickdrop", "QuickDrop Transfers", "Utilities"),
                ("quickdock.weather", "Weather Forecast", "Utilities"),
                ("quickdock.network", "Network Monitor", "Utilities"),
                ("quickdock.dev", "Developer Tools", "Developer"),
                ("quickdock.ai", "AI Services", "Developer")
            };

            foreach (var (id, name, cat) in catalog)
            {
                bool isEnabled = !settings.EnabledModules.TryGetValue(id, out bool val) || val;
                var item = new ModuleToggleItem(id, name, cat, isEnabled);
                item.PropertyChanged += async (s, e) =>
                {
                    if (e.PropertyName == nameof(ModuleToggleItem.IsEnabled))
                    {
                        await SaveModuleStateAsync(item.Id, item.IsEnabled);
                    }
                };
                ModuleToggles.Add(item);
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    partial void OnSelectedThemeIndexChanged(int value)
    {
        if (_isLoading) return;

        var theme = value switch
        {
            1 => AppTheme.Light,
            2 => AppTheme.Dark,
            _ => AppTheme.System
        };

        _ = SaveThemeAsync(theme);
    }

    partial void OnAlwaysOnTopChanged(bool value)
    {
        if (_isLoading) return;
        var settings = _settingsManager.CurrentSettings;
        settings.Behavior.AlwaysOnTop = value;
        _ = SaveSettingsAndPublishAsync();
    }

    partial void OnHideInFullscreenChanged(bool value)
    {
        if (_isLoading) return;
        var settings = _settingsManager.CurrentSettings;
        settings.Behavior.HideInFullscreen = value;
        _ = SaveSettingsAndPublishAsync();
    }

    partial void OnAutoHideChanged(bool value)
    {
        if (_isLoading) return;
        var settings = _settingsManager.CurrentSettings;
        settings.Behavior.AutoHide = value;
        _ = SaveSettingsAndPublishAsync();
    }

    partial void OnSelectedMonitorIndexChanged(int value)
    {
        if (_isLoading) return;

        var settings = _settingsManager.CurrentSettings;
        if (value == 0)
        {
            settings.Behavior.FollowActiveMonitor = true;
            settings.Behavior.MonitorTarget = "Active";
        }
        else if (value == 1)
        {
            settings.Behavior.FollowActiveMonitor = false;
            settings.Behavior.MonitorTarget = "Primary";
        }
        else
        {
            int monIdx = value - 2;
            var monitors = _monitorService.GetAllMonitors();
            if (monIdx >= 0 && monIdx < monitors.Count)
            {
                settings.Behavior.FollowActiveMonitor = false;
                settings.Behavior.MonitorTarget = monitors[monIdx].DeviceId;
            }
        }

        _ = SaveSettingsAndPublishAsync();
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_isLoading) return;

        bool success = _startupService.SetStartupEnabled(value);
        if (success)
        {
            var settings = _settingsManager.CurrentSettings;
            settings.Behavior.StartWithWindows = value;
            _ = SaveSettingsAndPublishAsync();
            _logger.LogInformation("Start with Windows set to {Value}", value);
        }
    }

    private async Task SaveThemeAsync(AppTheme theme)
    {
        var settings = _settingsManager.CurrentSettings;
        settings.Appearance.Theme = theme;
        await SaveSettingsAndPublishAsync();
        _logger.LogInformation("Saved theme preference: {Theme}", theme);
    }

    private async Task SaveModuleStateAsync(string moduleId, bool isEnabled)
    {
        var settings = _settingsManager.CurrentSettings;
        settings.EnabledModules[moduleId] = isEnabled;
        await SaveSettingsAndPublishAsync();
        _logger.LogInformation("Updated module {Id} enabled status: {IsEnabled}", moduleId, isEnabled);
    }

    private async Task SaveSettingsAndPublishAsync()
    {
        var settings = _settingsManager.CurrentSettings;
        await _settingsManager.SaveAsync(settings);
        await _eventBus.PublishAsync(new SettingsChangedEvent(settings));
    }

    [RelayCommand]
    private async Task ResetSettingsAsync()
    {
        await _settingsManager.ResetToDefaultsAsync();
        _startupService.SetStartupEnabled(false);
        LoadSettings();
        await _eventBus.PublishAsync(new SettingsChangedEvent(_settingsManager.CurrentSettings));
        _logger.LogInformation("Settings reset to defaults.");
    }
}
