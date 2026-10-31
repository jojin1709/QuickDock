using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Events;
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
    private readonly IEventBus _eventBus;
    private readonly ILogger<SettingsViewModel> _logger;

    [ObservableProperty]
    private int _selectedThemeIndex; // 0 = System, 1 = Light, 2 = Dark

    [ObservableProperty]
    private string _globalHotkey = "Ctrl+Space";

    [ObservableProperty]
    private int _hoverDelayMs = 150;

    [ObservableProperty]
    private bool _autoHide = true;

    [ObservableProperty]
    private bool _startWithWindows = false;

    [ObservableProperty]
    private string _appVersion = "1.0.0 (Phase 2 Preview)";

    [ObservableProperty]
    private string _dotNetVersion = Environment.Version.ToString();

    [ObservableProperty]
    private string _storagePath = AppStoragePaths.ConfigFile;

    public ObservableCollection<ModuleToggleItem> ModuleToggles { get; } = [];

    public SettingsViewModel(
        ISettingsManager settingsManager,
        IEventBus eventBus,
        ILogger<SettingsViewModel> logger)
    {
        _settingsManager = settingsManager;
        _eventBus = eventBus;
        _logger = logger;

        LoadSettings();
    }

    private void LoadSettings()
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
        StartWithWindows = settings.Behavior.StartWithWindows;

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

    partial void OnSelectedThemeIndexChanged(int value)
    {
        var theme = value switch
        {
            1 => AppTheme.Light,
            2 => AppTheme.Dark,
            _ => AppTheme.System
        };

        _ = SaveThemeAsync(theme);
    }

    private async Task SaveThemeAsync(AppTheme theme)
    {
        var settings = _settingsManager.CurrentSettings;
        settings.Appearance.Theme = theme;
        await _settingsManager.SaveAsync(settings);
        _logger.LogInformation("Saved theme preference: {Theme}", theme);
    }

    private async Task SaveModuleStateAsync(string moduleId, bool isEnabled)
    {
        var settings = _settingsManager.CurrentSettings;
        settings.EnabledModules[moduleId] = isEnabled;
        await _settingsManager.SaveAsync(settings);
        _logger.LogInformation("Updated module {Id} enabled status: {IsEnabled}", moduleId, isEnabled);
    }

    [RelayCommand]
    private async Task ResetSettingsAsync()
    {
        await _settingsManager.ResetToDefaultsAsync();
        LoadSettings();
        _logger.LogInformation("Settings reset to defaults.");
    }
}

