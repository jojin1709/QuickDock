using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Events;
using QuickDock.Core.Modules;
using QuickDock.Modules.Clock;
using QuickDock.Services.System;

namespace QuickDock.App.ViewModels;

public sealed record ModuleTileItem(
    string Id,
    string Title,
    string Subtitle,
    string Glyph,
    string Category,
    string StatusText);

public sealed partial class HomeViewModel : ObservableObject
{
    private readonly IEventBus _eventBus;
    private readonly IModuleManager _moduleManager;
    private readonly ISystemMetricsService _metricsService;
    private readonly ILogger<HomeViewModel> _logger;
    private readonly List<ModuleTileItem> _allModules = [];

    [ObservableProperty]
    private string _currentTime = DateTime.Now.ToString("HH:mm");

    [ObservableProperty]
    private string _currentDate = DateTime.Now.ToString("dddd, MMM d");

    [ObservableProperty]
    private string _cpuMetricText = "— CPU";

    [ObservableProperty]
    private string _memoryMetricText = "— RAM";

    [ObservableProperty]
    private string _batteryMetricText = "— Power";

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    public ObservableCollection<ModuleTileItem> FilteredProductivityModules { get; } = [];
    public ObservableCollection<ModuleTileItem> FilteredUtilityModules { get; } = [];
    public ObservableCollection<ModuleTileItem> FilteredDeveloperModules { get; } = [];
    public ObservableCollection<ModuleTileItem> FilteredSystemModules { get; } = [];

    public HomeViewModel(
        IEventBus eventBus,
        IModuleManager moduleManager,
        ISystemMetricsService metricsService,
        ILogger<HomeViewModel> logger)
    {
        _eventBus = eventBus;
        _moduleManager = moduleManager;
        _metricsService = metricsService;
        _logger = logger;

        InitializeModuleCatalog();
        RefreshFilteredModules();
        RefreshMetrics();
    }

    public void UpdateTime(DateTime now)
    {
        CurrentTime = now.ToString("HH:mm");
        CurrentDate = now.ToString("dddd, MMM d");
    }

    public void RefreshMetrics()
    {
        var cpu = _metricsService.GetCpuUsagePercentage();
        CpuMetricText = cpu.HasValue ? $"{cpu.Value:0.#}% CPU" : "— CPU";

        var mem = _metricsService.GetMemoryMetrics();
        MemoryMetricText = mem != null ? $"{mem.UsedGigabytes:0.#} / {mem.TotalGigabytes:0.#} GB" : "— RAM";

        var bat = _metricsService.GetBatteryMetrics();
        BatteryMetricText = bat != null
            ? $"{(bat.IsCharging ? "⚡ " : "")}{bat.Percentage}%"
            : "AC Power";
    }

    partial void OnSearchQueryChanged(string value)
    {
        RefreshFilteredModules();
    }

    private void RefreshFilteredModules()
    {
        FilteredProductivityModules.Clear();
        FilteredUtilityModules.Clear();
        FilteredDeveloperModules.Clear();
        FilteredSystemModules.Clear();

        string query = SearchQuery.Trim().ToLowerInvariant();

        foreach (var item in _allModules)
        {
            if (string.IsNullOrEmpty(query) ||
                item.Title.ToLowerInvariant().Contains(query) ||
                item.Subtitle.ToLowerInvariant().Contains(query))
            {
                switch (item.Category)
                {
                    case "Productivity":
                        FilteredProductivityModules.Add(item);
                        break;
                    case "Utilities":
                        FilteredUtilityModules.Add(item);
                        break;
                    case "Developer":
                        FilteredDeveloperModules.Add(item);
                        break;
                    case "System":
                        FilteredSystemModules.Add(item);
                        break;
                }
            }
        }
    }

    private void InitializeModuleCatalog()
    {
        _allModules.Clear();

        // System
        _allModules.Add(new ModuleTileItem("quickdock.system", "System", "Performance & Resources", "\uE770", "System", "Active"));
        _allModules.Add(new ModuleTileItem("quickdock.volume", "Volume", "Audio output & devices", "\uE767", "System", "Active"));
        _allModules.Add(new ModuleTileItem("quickdock.display", "Display", "Monitors & resolution", "\uE7F4", "System", "Active"));

        // Productivity
        _allModules.Add(new ModuleTileItem("quickdock.clock", "Clock", "Time, timer & stopwatches", "\uE823", "Productivity", "Running"));
        _allModules.Add(new ModuleTileItem("quickdock.clipboard", "Clipboard", "Multi-format ring & history", "\uF0E3", "Productivity", "Ready"));
        _allModules.Add(new ModuleTileItem("quickdock.notes", "Notes", "Quick scratchpad & markdown", "\uE70B", "Productivity", "Ready"));
        _allModules.Add(new ModuleTileItem("quickdock.timer", "Timer", "Pomodoro & countdowns", "\uE916", "Productivity", "Idle"));

        // Utilities
        _allModules.Add(new ModuleTileItem("quickdock.quickdrop", "QuickDrop", "Local file drop & transfer", "\uE898", "Utilities", "Ready"));
        _allModules.Add(new ModuleTileItem("quickdock.files", "Files", "Recent files & pinboard", "\uE838", "Utilities", "Ready"));
        _allModules.Add(new ModuleTileItem("quickdock.weather", "Weather", "Forecast & atmospheric data", "\uE9BE", "Utilities", "Ready"));
        _allModules.Add(new ModuleTileItem("quickdock.network", "Network", "Adapters & speed monitor", "\uEC27", "Utilities", "Active"));

        // Developer & AI
        _allModules.Add(new ModuleTileItem("quickdock.dev", "Dev Tools", "Formatters, UUID, base64", "\uEBE8", "Developer", "Ready"));
        _allModules.Add(new ModuleTileItem("quickdock.security", "Security", "Hash verify & permissions", "\uEA18", "Developer", "Ready"));
        _allModules.Add(new ModuleTileItem("quickdock.ai", "AI Services", "Local and cloud assistants", "\uE946", "Developer", "Optional"));
    }

    [RelayCommand]
    private void SelectModule(ModuleTileItem? item)
    {
        if (item == null) return;
        _logger.LogInformation("Selected module: {Id} ({Title})", item.Id, item.Title);
    }
}

