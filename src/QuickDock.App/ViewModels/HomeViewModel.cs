using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Events;
using QuickDock.Core.Interfaces;
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
    private readonly IAudioService _audioService;
    private readonly IMediaService _mediaService;
    private readonly ClockModule _clockModule;
    private readonly ILogger<HomeViewModel> _logger;
    private readonly List<ModuleTileItem> _allModules = [];

    public event Action<string>? NavigationRequested;

    [ObservableProperty]
    private string _currentTime = DateTime.Now.ToString("HH:mm");

    [ObservableProperty]
    private string _currentDate = DateTime.Now.ToString("dddd, MMMM d");

    [ObservableProperty]
    private string _cpuMetricText = "— CPU";

    [ObservableProperty]
    private string _memoryMetricText = "— RAM";

    [ObservableProperty]
    private string _batteryMetricText = "— Power";

    [ObservableProperty]
    private string _volumeMetricText = "— Volume";

    // Volume state
    [ObservableProperty]
    private double _volumeLevel = 50;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private string _audioDeviceName = "Speakers";

    [ObservableProperty]
    private string _volumeGlyph = "\uE767";

    // Media state
    [ObservableProperty]
    private string _mediaTitle = "No media playing";

    [ObservableProperty]
    private string _mediaArtist = string.Empty;

    [ObservableProperty]
    private bool _hasMediaSession;

    [ObservableProperty]
    private bool _isMediaPlaying;

    [ObservableProperty]
    private string _playPauseGlyph = "\uE768"; // Play glyph

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
        IAudioService audioService,
        IMediaService mediaService,
        ClockModule clockModule,
        ILogger<HomeViewModel> logger)
    {
        _eventBus = eventBus;
        _moduleManager = moduleManager;
        _metricsService = metricsService;
        _audioService = audioService;
        _mediaService = mediaService;
        _clockModule = clockModule;
        _logger = logger;

        InitializeModuleCatalog();
        RefreshFilteredModules();
        RefreshMetrics();
        RefreshAudioState();
        RefreshMediaState(_mediaService.CurrentMedia);

        _audioService.VolumeChanged += OnAudioVolumeChanged;
        _mediaService.MediaChanged += OnMediaChanged;
    }

    public void UpdateTime(DateTime now)
    {
        CurrentTime = _clockModule.GetFormattedTime();
        CurrentDate = _clockModule.GetFormattedDate();
    }

    public void RefreshMetrics()
    {
        var cpu = _metricsService.GetCpuUsagePercentage();
        CpuMetricText = cpu.HasValue ? $"{cpu.Value:0.#}% CPU" : "— CPU";

        var mem = _metricsService.GetMemoryMetrics();
        MemoryMetricText = mem != null ? $"{mem.UsedGigabytes:0.#} / {mem.TotalGigabytes:0.#} GB" : "— RAM";

        var bat = _metricsService.GetBatteryMetrics();
        BatteryMetricText = bat != null ? bat.StateDescription : "AC Power";

        RefreshAudioState();
    }

    private bool _isInternalVolumeUpdate;

    private void RefreshAudioState()
    {
        float vol = _audioService.GetMasterVolume();
        bool muted = _audioService.GetMuteState();

        _isInternalVolumeUpdate = true;
        try
        {
            VolumeLevel = Math.Round(vol * 100);
        }
        finally
        {
            _isInternalVolumeUpdate = false;
        }

        IsMuted = muted;
        AudioDeviceName = _audioService.GetDefaultDeviceName();
        VolumeMetricText = muted ? "Muted" : $"{VolumeLevel:0}%";
        VolumeGlyph = muted ? "\uE74F" : (vol < 0.33f ? "\uE992" : (vol < 0.66f ? "\uE993" : "\uE994"));
    }

    private void OnAudioVolumeChanged(object? sender, AudioVolumeChangedEventArgs e)
    {
        _isInternalVolumeUpdate = true;
        try
        {
            VolumeLevel = Math.Round(e.NewVolume * 100);
        }
        finally
        {
            _isInternalVolumeUpdate = false;
        }

        IsMuted = e.IsMuted;
        VolumeMetricText = e.IsMuted ? "Muted" : $"{VolumeLevel:0}%";
        VolumeGlyph = e.IsMuted ? "\uE74F" : (e.NewVolume < 0.33f ? "\uE992" : (e.NewVolume < 0.66f ? "\uE993" : "\uE994"));
    }

    private void RefreshMediaState(MediaSessionSnapshot? media)
    {
        if (media != null && media.HasSession)
        {
            HasMediaSession = true;
            MediaTitle = string.IsNullOrWhiteSpace(media.Title) ? "Active Media" : media.Title;
            MediaArtist = media.Artist;
            IsMediaPlaying = media.PlaybackStatus == MediaPlaybackStatus.Playing;
            PlayPauseGlyph = IsMediaPlaying ? "\uE769" : "\uE768"; // Pause vs Play
        }
        else
        {
            HasMediaSession = false;
            MediaTitle = "No media playing";
            MediaArtist = string.Empty;
            IsMediaPlaying = false;
            PlayPauseGlyph = "\uE768";
        }
    }

    private void OnMediaChanged(object? sender, MediaSessionSnapshot? e)
    {
        RefreshMediaState(e);
    }

    partial void OnVolumeLevelChanged(double value)
    {
        if (_isInternalVolumeUpdate) return;
        float scalar = (float)(Math.Clamp(value, 0, 100) / 100.0);
        _audioService.SetMasterVolume(scalar);
    }

    [RelayCommand]
    private void ToggleMute()
    {
        _audioService.ToggleMute();
        RefreshAudioState();
    }

    [RelayCommand]
    private void IncreaseVolume()
    {
        _audioService.IncreaseVolume(0.05f);
        RefreshAudioState();
    }

    [RelayCommand]
    private void DecreaseVolume()
    {
        _audioService.DecreaseVolume(0.05f);
        RefreshAudioState();
    }

    [RelayCommand]
    private async Task TogglePlayPauseAsync()
    {
        await _mediaService.TogglePlayPauseAsync();
    }

    [RelayCommand]
    private async Task SkipNextAsync()
    {
        await _mediaService.SkipNextAsync();
    }

    [RelayCommand]
    private async Task SkipPreviousAsync()
    {
        await _mediaService.SkipPreviousAsync();
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
        _allModules.Add(new ModuleTileItem("quickdock.system", "System", "CPU, RAM, GPU, Disk, Uptime", "\uE770", "System", "Active"));
        _allModules.Add(new ModuleTileItem("quickdock.volume", "Volume", "Speakers & master level", "\uE767", "System", "Active"));
        _allModules.Add(new ModuleTileItem("quickdock.battery", "Battery", "Power state & battery health", "\uE83F", "System", "Active"));

        // Productivity
        _allModules.Add(new ModuleTileItem("quickdock.clock", "Clock", "Local time & timezone", "\uE823", "Productivity", "Running"));
        _allModules.Add(new ModuleTileItem("quickdock.clipboard", "Clipboard", "Multi-format ring & history", "\uF0E3", "Productivity", "Ready"));
        _allModules.Add(new ModuleTileItem("quickdock.notes", "Notes", "Quick scratchpad & markdown", "\uE70B", "Productivity", "Ready"));
        _allModules.Add(new ModuleTileItem("quickdock.timer", "Timer", "Pomodoro & countdowns", "\uE916", "Productivity", "Idle"));

        // Utilities
        _allModules.Add(new ModuleTileItem("quickdock.media", "Media", "System media transport session", "\uE8D6", "Utilities", "Active"));
        _allModules.Add(new ModuleTileItem("quickdock.network", "Network", "Adapters & connectivity", "\uEC27", "Utilities", "Active"));
        _allModules.Add(new ModuleTileItem("quickdock.quickdrop", "QuickDrop", "Local file drop & transfer", "\uE898", "Utilities", "Ready"));

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

        if (item.Id == "quickdock.system")
        {
            NavigationRequested?.Invoke("System");
        }
    }
}
