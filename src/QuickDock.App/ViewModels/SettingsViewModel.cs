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
    private readonly IDatabaseService _databaseService;
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

    // Phase 5: Productivity Settings
    [ObservableProperty]
    private bool _clipboardHistoryEnabled = true;

    [ObservableProperty]
    private int _clipboardRetentionIndex = 3; // 0=1h, 1=6h, 2=1d, 3=7d, 4=30d

    [ObservableProperty]
    private int _clipboardMaxItems = 50;

    [ObservableProperty]
    private int _sensitiveDataActionIndex = 0; // 0=Ignore, 1=StoreNormally, 2=RequireConfirmation

    [ObservableProperty]
    private int _defaultTimerMinutes = 15;

    [ObservableProperty]
    private bool _timerSoundEnabled = true;

    [ObservableProperty]
    private int _pomodoroWorkMinutes = 25;

    [ObservableProperty]
    private int _pomodoroShortBreakMinutes = 5;

    [ObservableProperty]
    private int _pomodoroLongBreakMinutes = 15;

    [ObservableProperty]
    private bool _calendarWeekStartsOnMonday = true;

    [ObservableProperty]
    private bool _calendar24HourFormat = true;

    [ObservableProperty]
    private bool _weatherAutoLocation = true;

    [ObservableProperty]
    private string _weatherManualCity = "Thrissur";

    [ObservableProperty]
    private int _weatherRefreshMinutes = 30;

    [ObservableProperty]
    private bool _screenTimeTrackingEnabled = true;

    [ObservableProperty]
    private string _settingsStatusMessage = string.Empty;

    [ObservableProperty]
    private string _appVersion = "1.0.0 (Phase 5 Productivity)";

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
        IDatabaseService databaseService,
        IEventBus eventBus,
        ILogger<SettingsViewModel> logger)
    {
        _settingsManager = settingsManager;
        _startupService = startupService;
        _monitorService = monitorService;
        _databaseService = databaseService;
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

            // Phase 5 Settings Load
            ClipboardHistoryEnabled = settings.Clipboard.Enabled;
            ClipboardRetentionIndex = (int)settings.Clipboard.Retention;
            ClipboardMaxItems = settings.Clipboard.MaxItems;
            SensitiveDataActionIndex = (int)settings.Clipboard.SensitiveAction;

            DefaultTimerMinutes = settings.Timer.DefaultTimerMinutes;
            TimerSoundEnabled = settings.Timer.PlaySoundOnComplete;
            PomodoroWorkMinutes = settings.Timer.PomodoroWorkMinutes;
            PomodoroShortBreakMinutes = settings.Timer.PomodoroShortBreakMinutes;
            PomodoroLongBreakMinutes = settings.Timer.PomodoroLongBreakMinutes;

            CalendarWeekStartsOnMonday = settings.Calendar.WeekStartsOnMonday;
            Calendar24HourFormat = settings.Calendar.Use24HourFormat;

            WeatherAutoLocation = settings.Weather.UseAutomaticLocation;
            WeatherManualCity = settings.Weather.ManualLocationName ?? "Thrissur";
            WeatherRefreshMinutes = settings.Weather.RefreshIntervalMinutes;

            ScreenTimeTrackingEnabled = settings.ScreenTime.TrackingEnabled;

            ModuleToggles.Clear();
            var catalog = new (string Id, string Name, string Category)[]
            {
                ("quickdock.clock", "Clock & Time", "Productivity"),
                ("quickdock.clipboard", "Clipboard Manager", "Productivity"),
                ("quickdock.timer", "Timer & Stopwatch", "Productivity"),
                ("quickdock.notes", "Quick Notes", "Productivity"),
                ("quickdock.calendar", "Calendar Agenda", "Productivity"),
                ("quickdock.weather", "Weather Forecast", "Productivity"),
                ("quickdock.links", "Saved Links", "Productivity"),
                ("quickdock.screentime", "Screen Time Analytics", "Productivity"),
                ("quickdock.system", "System Controls", "System"),
                ("quickdock.volume", "Volume Control", "System"),
                ("quickdock.battery", "Battery & Power", "System"),
                ("quickdock.network", "Network Monitor", "Utilities"),
                ("quickdock.media", "Media Session", "Utilities"),
                ("quickdock.quickdrop", "QuickDrop Transfers", "Utilities"),
                ("quickdock.dev", "Developer Tools", "Developer"),
                ("quickdock.security", "Security Toolkit", "Developer"),
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
    private async Task ClearAllStoredDataAsync()
    {
        await _databaseService.ClearAllDataAsync();
        SettingsStatusMessage = "All stored local data has been cleared.";
    }

    [RelayCommand]
    private async Task ClearScreenTimeDataAsync()
    {
        await _databaseService.ClearUsageHistoryAsync();
        SettingsStatusMessage = "Screen time history cleared.";
    }

    [RelayCommand]
    private async Task SaveProductivitySettingsAsync()
    {
        var settings = _settingsManager.CurrentSettings;
        settings.Clipboard.Enabled = ClipboardHistoryEnabled;
        settings.Clipboard.Retention = (ClipboardRetention)Math.Clamp(ClipboardRetentionIndex, 0, 4);
        settings.Clipboard.MaxItems = Math.Clamp(ClipboardMaxItems, 5, 200);
        settings.Clipboard.SensitiveAction = (SensitiveDataHandling)Math.Clamp(SensitiveDataActionIndex, 0, 2);

        settings.Timer.DefaultTimerMinutes = DefaultTimerMinutes;
        settings.Timer.PlaySoundOnComplete = TimerSoundEnabled;
        settings.Timer.PomodoroWorkMinutes = PomodoroWorkMinutes;
        settings.Timer.PomodoroShortBreakMinutes = PomodoroShortBreakMinutes;
        settings.Timer.PomodoroLongBreakMinutes = PomodoroLongBreakMinutes;

        settings.Calendar.WeekStartsOnMonday = CalendarWeekStartsOnMonday;
        settings.Calendar.Use24HourFormat = Calendar24HourFormat;

        settings.Weather.UseAutomaticLocation = WeatherAutoLocation;
        settings.Weather.ManualLocationName = WeatherManualCity;
        settings.Weather.RefreshIntervalMinutes = WeatherRefreshMinutes;

        settings.ScreenTime.TrackingEnabled = ScreenTimeTrackingEnabled;

        await SaveSettingsAndPublishAsync();
        SettingsStatusMessage = "Settings saved successfully.";
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
