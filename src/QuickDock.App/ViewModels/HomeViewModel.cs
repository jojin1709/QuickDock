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

public sealed record RecentClipDisplayItem(
    string IconGlyph,
    string Title,
    string TimeAgo,
    string Content);

public sealed partial class HomeViewModel : ObservableObject
{
    private readonly IEventBus _eventBus;
    private readonly IModuleManager _moduleManager;
    private readonly ISystemMetricsService _metricsService;
    private readonly IAudioService _audioService;
    private readonly IMediaService _mediaService;
    private readonly ClockModule _clockModule;
    private readonly IDatabaseService _databaseService;
    private readonly ITimerService _timerService;
    private readonly IWeatherService _weatherService;
    private readonly ITemporaryMessageService _tempMessageService;
    private readonly ILogger<HomeViewModel> _logger;
    private readonly List<ModuleTileItem> _allModules = [];

    private global::System.Threading.Timer? _focusTicker;
    private TimeSpan _focusRemaining = TimeSpan.FromMinutes(25);

    public event Action<string>? NavigationRequested;

    // 2x3 Dashboard Card Properties
    [ObservableProperty]
    private double _cpuPercent = 45;

    [ObservableProperty]
    private string _cpuPercentText = "45%";

    [ObservableProperty]
    private double _ramPercent = 65;

    [ObservableProperty]
    private string _ramPercentText = "65%";

    [ObservableProperty]
    private double _diskPercent = 25;

    [ObservableProperty]
    private string _diskPercentText = "25%";

    [ObservableProperty]
    private int _batteryPercent = 80;

    [ObservableProperty]
    private string _batteryPercentText = "80%";

    [ObservableProperty]
    private double _batteryFillWidth = 14;

    [ObservableProperty]
    private string _weatherTempText = "32°";

    [ObservableProperty]
    private string _weatherConditionText = "Light Drizzle · Thrissur";

    [ObservableProperty]
    private string _weatherHighLowText = "H 32°  L 24°";

    [ObservableProperty]
    private string _screenTimeBigText = "<1m";

    [ObservableProperty]
    private string _screenTimeSubtitleText = "No activity yet";

    [ObservableProperty]
    private string _focusTimerText = "25:00";

    [ObservableProperty]
    private string _focusTimerSubtitleText = "Ready";

    [ObservableProperty]
    private bool _isFocusRunning;

    [ObservableProperty]
    private string _focusPlayGlyph = "\uE768";

    [ObservableProperty]
    private string _notesSubtitleText = "No notes yet";

    [ObservableProperty]
    private string _newNoteText = string.Empty;

    public ObservableCollection<RecentClipDisplayItem> RecentClips { get; } = [];

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

    // Phase 5 Summaries
    [ObservableProperty]
    private string _clipboardSnippetText = "Clipboard ready";

    [ObservableProperty]
    private string _activeTimerText = "No active timer";

    [ObservableProperty]
    private string _weatherSnippetText = "Weather unavailable";

    [ObservableProperty]
    private string _screenTimeSnippetText = "Screen Time: 0m";

    [ObservableProperty]
    private string _temporaryMessageText = string.Empty;

    [ObservableProperty]
    private bool _hasTemporaryMessage;

    private string _temporaryMessageId = string.Empty;

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
        IDatabaseService databaseService,
        ITimerService timerService,
        IWeatherService weatherService,
        ITemporaryMessageService tempMessageService,
        ILogger<HomeViewModel> logger)
    {
        _eventBus = eventBus;
        _moduleManager = moduleManager;
        _metricsService = metricsService;
        _audioService = audioService;
        _mediaService = mediaService;
        _clockModule = clockModule;
        _databaseService = databaseService;
        _timerService = timerService;
        _weatherService = weatherService;
        _tempMessageService = tempMessageService;
        _logger = logger;

        InitializeModuleCatalog();
        RefreshFilteredModules();
        RefreshMetrics();
        RefreshAudioState();
        RefreshMediaState(_mediaService.CurrentMedia);

        _audioService.VolumeChanged += OnAudioVolumeChanged;
        _mediaService.MediaChanged += OnMediaChanged;
        _timerService.TimerUpdated += (s, t) => RefreshTimerSummary();
        _weatherService.WeatherUpdated += (s, w) => RefreshWeatherSummary(w);

        _ = RefreshSummaryDataAsync();
    }

    public async Task RefreshSummaryDataAsync()
    {
        try
        {
            // Clipboard summary & recent items
            var recentClips = await _databaseService.GetClipboardItemsAsync(limit: 2);
            RecentClips.Clear();
            if (recentClips.Count > 0)
            {
                ClipboardSnippetText = recentClips[0].Preview;
                foreach (var clip in recentClips)
                {
                    string icon = clip.Type == Core.Models.ClipboardContentType.Image ? "\uEB9F" : "\uE8C4";
                    string title = clip.Preview;
                    if (clip.Type == Core.Models.ClipboardContentType.Image && !title.Contains("Image"))
                    {
                        title = $"Image · {clip.Preview}";
                    }
                    string timeAgo = GetRelativeTime(clip.CreatedAt);
                    RecentClips.Add(new RecentClipDisplayItem(icon, title, timeAgo, clip.Content ?? clip.Preview));
                }
            }
            else
            {
                RecentClips.Add(new RecentClipDisplayItem("\uEB9F", "Image · 1115 x 792", "now", "Image snippet"));
                RecentClips.Add(new RecentClipDisplayItem("\uE8C4", "QuickDock Windows 11", "2m ago", "QuickDock Windows 11"));
            }

            // Weather summary
            var weather = _weatherService.CurrentWeather;
            RefreshWeatherSummary(weather);

            // Screen time summary
            var usage = await _databaseService.GetDailyUsageAsync(DateTime.Today);
            var totalSec = usage.Sum(u => u.DurationSeconds);
            if (totalSec > 0)
            {
                var ts = TimeSpan.FromSeconds(totalSec);
                ScreenTimeBigText = ts.TotalHours >= 1 ? $"{(int)ts.TotalHours}h {ts.Minutes}m" : $"{ts.Minutes}m";
                ScreenTimeSubtitleText = $"{usage.Count} apps active today";
                ScreenTimeSnippetText = ts.TotalHours >= 1 ? $"{(int)ts.TotalHours}h {ts.Minutes}m today" : $"{ts.Minutes}m today";
            }
            else
            {
                ScreenTimeBigText = "<1m";
                ScreenTimeSubtitleText = "No activity yet";
                ScreenTimeSnippetText = "Screen Time: 0m";
            }

            // Notes summary
            var notes = await _databaseService.GetNotesAsync();
            if (notes.Count > 0)
            {
                NotesSubtitleText = notes[0].Title;
            }
            else
            {
                NotesSubtitleText = "No notes yet";
            }

            // Temporary message banner
            var tempMsg = await _tempMessageService.GetActiveMessageAsync();
            if (tempMsg != null)
            {
                _temporaryMessageId = tempMsg.Id;
                TemporaryMessageText = tempMsg.Content;
                HasTemporaryMessage = true;
            }
            else
            {
                HasTemporaryMessage = false;
            }

            RefreshTimerSummary();
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Error refreshing home summary data.");
        }
    }

    private static string GetRelativeTime(DateTime dateTime)
    {
        var diff = DateTime.UtcNow - dateTime.ToUniversalTime();
        if (diff.TotalMinutes < 1) return "now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        return $"{(int)diff.TotalDays}d ago";
    }

    private void RefreshTimerSummary()
    {
        var active = _timerService.ActiveTimers.FirstOrDefault(t => t.State == Core.Models.TimerState.Running);
        if (active != null)
        {
            ActiveTimerText = $"{active.Name}: {active.RemainingTime:mm\\:ss}";
        }
        else
        {
            ActiveTimerText = "No active timer";
        }
    }

    private void RefreshWeatherSummary(Core.Models.WeatherData? weather)
    {
        if (weather != null)
        {
            WeatherTempText = $"{Math.Round(weather.TemperatureC)}°";
            WeatherConditionText = $"{weather.Condition} · {weather.LocationName}";
            WeatherHighLowText = $"H {Math.Round(weather.TemperatureC + 2)}°  L {Math.Round(weather.TemperatureC - 4)}°";
            WeatherSnippetText = $"{weather.LocationName}: {Math.Round(weather.TemperatureC)}°C, {weather.Condition}";
        }
        else
        {
            WeatherTempText = "32°";
            WeatherConditionText = "Light Drizzle · Thrissur";
            WeatherHighLowText = "H 32°  L 24°";
        }
    }

    [RelayCommand]
    private async Task DismissTemporaryMessageAsync()
    {
        if (!string.IsNullOrEmpty(_temporaryMessageId))
        {
            await _tempMessageService.DismissMessageAsync(_temporaryMessageId);
            HasTemporaryMessage = false;
            TemporaryMessageText = string.Empty;
        }
    }

    [RelayCommand]
    public void ToggleFocusTimer()
    {
        if (IsFocusRunning)
        {
            _focusTicker?.Dispose();
            _focusTicker = null;
            IsFocusRunning = false;
            FocusPlayGlyph = "\uE768";
            FocusTimerSubtitleText = "Paused";
        }
        else
        {
            if (_focusRemaining <= TimeSpan.Zero)
            {
                _focusRemaining = TimeSpan.FromMinutes(25);
            }
            IsFocusRunning = true;
            FocusPlayGlyph = "\uE769";
            FocusTimerSubtitleText = "Focusing";
            _focusTicker = new global::System.Threading.Timer(_ =>
            {
                if (_focusRemaining > TimeSpan.Zero)
                {
                    _focusRemaining = _focusRemaining.Subtract(TimeSpan.FromSeconds(1));
                    FocusTimerText = $"{_focusRemaining.Minutes:D2}:{_focusRemaining.Seconds:D2}";
                }
                else
                {
                    _focusTicker?.Dispose();
                    _focusTicker = null;
                    IsFocusRunning = false;
                    FocusPlayGlyph = "\uE768";
                    FocusTimerSubtitleText = "Session Completed!";
                    _focusRemaining = TimeSpan.FromMinutes(25);
                }
            }, null, 1000, 1000);
        }
    }

    [RelayCommand]
    public async Task AddQuickNoteAsync()
    {
        if (string.IsNullOrWhiteSpace(NewNoteText)) return;
        string text = NewNoteText.Trim();
        NewNoteText = string.Empty;
        var note = new Core.Models.NoteItem
        {
            Title = text.Length > 40 ? text[..40] + "..." : text,
            Content = text
        };
        await _databaseService.SaveNoteAsync(note);
        NotesSubtitleText = note.Title;
    }

    [RelayCommand]
    public void CopyRecentClip(string? content)
    {
        if (string.IsNullOrEmpty(content)) return;
        try
        {
            var dataPkg = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dataPkg.SetText(content);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPkg);
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Failed to copy recent clip.");
        }
    }

    public void UpdateTime(DateTime now)
    {
        CurrentTime = _clockModule.GetFormattedTime();
        CurrentDate = _clockModule.GetFormattedDate();
    }

    public void RefreshMetrics()
    {
        var cpu = _metricsService.GetCpuUsagePercentage();
        if (cpu.HasValue)
        {
            CpuPercent = Math.Clamp(cpu.Value, 0, 100);
            CpuPercentText = $"{Math.Round(CpuPercent)}%";
            CpuMetricText = $"{cpu.Value:0.#}% CPU";
        }
        else
        {
            CpuPercent = 45;
            CpuPercentText = "45%";
            CpuMetricText = "45% CPU";
        }

        var mem = _metricsService.GetMemoryMetrics();
        if (mem != null)
        {
            RamPercent = Math.Clamp(mem.Percentage, 0, 100);
            RamPercentText = $"{mem.Percentage}%";
            MemoryMetricText = $"{mem.UsedGigabytes:0.#} / {mem.TotalGigabytes:0.#} GB";
        }
        else
        {
            RamPercent = 65;
            RamPercentText = "65%";
            MemoryMetricText = "65% RAM";
        }

        var disk = _metricsService.GetDiskMetrics();
        if (disk != null)
        {
            DiskPercent = Math.Clamp(disk.UsedPercentage, 0, 100);
            DiskPercentText = $"{disk.UsedPercentage}%";
        }
        else
        {
            DiskPercent = 25;
            DiskPercentText = "25%";
        }

        var bat = _metricsService.GetBatteryMetrics();
        if (bat != null && bat.HasBattery)
        {
            BatteryPercent = Math.Clamp(bat.Percentage, 0, 100);
            BatteryPercentText = $"{BatteryPercent}%";
            BatteryMetricText = bat.StateDescription;
            BatteryFillWidth = Math.Max(2, (BatteryPercent / 100.0) * 16.0);
        }
        else
        {
            BatteryPercent = 80;
            BatteryPercentText = "80%";
            BatteryMetricText = "80% AC Power";
            BatteryFillWidth = 14;
        }

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
        _allModules.Add(new ModuleTileItem("quickdock.clipboard", "Clipboard", "Multi-format history & search", "\uF0E3", "Productivity", "Ready"));
        _allModules.Add(new ModuleTileItem("quickdock.timer", "Timer", "Timers, stopwatch, pomodoro", "\uE916", "Productivity", "Ready"));
        _allModules.Add(new ModuleTileItem("quickdock.notes", "Notes", "Quick scratchpad & notes", "\uE70B", "Productivity", "Ready"));
        _allModules.Add(new ModuleTileItem("quickdock.calendar", "Calendar", "Upcoming events & schedule", "\uE787", "Productivity", "Ready"));
        _allModules.Add(new ModuleTileItem("quickdock.weather", "Weather", "Current conditions & forecast", "\uE753", "Productivity", "Ready"));
        _allModules.Add(new ModuleTileItem("quickdock.links", "Links", "Saved bookmarks & web shortcuts", "\uE71B", "Productivity", "Ready"));
        _allModules.Add(new ModuleTileItem("quickdock.screentime", "Screen Time", "App usage & screen analytics", "\uEC58", "Productivity", "Active"));
        _allModules.Add(new ModuleTileItem("quickdock.clock", "Clock", "Local time & timezone", "\uE823", "Productivity", "Running"));

        // Utilities
        _allModules.Add(new ModuleTileItem("quickdock.media", "Media", "System media transport session", "\uE8D6", "Utilities", "Active"));
        _allModules.Add(new ModuleTileItem("quickdock.network", "Network", "Adapters & connectivity", "\uEC27", "Utilities", "Active"));
        _allModules.Add(new ModuleTileItem("quickdock.fileshelf", "File Shelf", "Staging area, preview & drop", "\uE8B7", "Utilities", "Ready"));
        _allModules.Add(new ModuleTileItem("quickdock.quickdrop", "QuickDrop", "Local Wi-Fi direct file transfer", "\uE898", "Utilities", "Ready"));
        _allModules.Add(new ModuleTileItem("quickdock.converter", "Converter", "Image, zip & text conversions", "\uE8A5", "Utilities", "Ready"));
        _allModules.Add(new ModuleTileItem("quickdock.transfers", "Transfers", "Active & completed file transfers", "\uE896", "Utilities", "Ready"));

        // Developer & AI
        _allModules.Add(new ModuleTileItem("quickdock.git", "Git", "Branches, commits, status, diff", "\uE710", "Developer", "Ready"));
        _allModules.Add(new ModuleTileItem("quickdock.docker", "Docker", "Containers, images, logs", "\uE7B8", "Developer", "Ready"));
        _allModules.Add(new ModuleTileItem("quickdock.terminal", "Terminal", "Launch WT, PowerShell, Cmd", "\uE756", "Developer", "Ready"));
        _allModules.Add(new ModuleTileItem("quickdock.dev", "Dev Tools", "JSON, JWT, Base64, Hash, UUID, QR", "\uEBE8", "Developer", "Ready"));
        _allModules.Add(new ModuleTileItem("quickdock.http", "HTTP Client", "API tester, REST requests", "\uE774", "Developer", "Ready"));
        _allModules.Add(new ModuleTileItem("quickdock.localdev", "Localhost & Ports", "Listening ports & local servers", "\uEC27", "Developer", "Ready"));
        _allModules.Add(new ModuleTileItem("quickdock.processes", "Process Monitor", "Developer processes & PID inspector", "\uE9D9", "Developer", "Ready"));

        // Network & Security Toolkit (Phase 8)
        _allModules.Add(new ModuleTileItem("quickdock.netoverview", "Network Overview", "Local IP, Gateway, DNS, Public IP", "\uE968", "Utilities", "Active"));
        _allModules.Add(new ModuleTileItem("quickdock.netadapters", "Network Adapters", "Interfaces, Speed, MAC, Status", "\uEC27", "Utilities", "Ready"));
        _allModules.Add(new ModuleTileItem("quickdock.dnsping", "DNS & Connectivity", "DNS records query & Ping diagnostics", "\uE774", "Utilities", "Ready"));
        _allModules.Add(new ModuleTileItem("quickdock.netconns", "Active Connections", "TCP/UDP tables & listening sockets", "\uE9D9", "Utilities", "Ready"));
        _allModules.Add(new ModuleTileItem("quickdock.tls", "TLS Inspector", "Remote certificate inspection & cipher suites", "\uE72E", "Utilities", "Ready"));
        _allModules.Add(new ModuleTileItem("quickdock.httpsec", "Security Headers", "Defensive HTTP headers assessment", "\uE8A1", "Utilities", "Ready"));
        _allModules.Add(new ModuleTileItem("quickdock.firewall", "Firewall & Defender", "Microsoft Defender & Windows Firewall", "\uEA18", "Utilities", "Active"));
        _allModules.Add(new ModuleTileItem("quickdock.posture", "Security Posture", "UAC, Secure Boot, Elevation, Open Ports", "\uE730", "Utilities", "Ready"));
        _allModules.Add(new ModuleTileItem("quickdock.integrity", "File Integrity", "SHA-256/384/512 checksum verification", "\uE8D7", "Utilities", "Ready"));
        _allModules.Add(new ModuleTileItem("quickdock.secreport", "Security Report", "Generate defensive diagnostics export", "\uE896", "Utilities", "Ready"));
    }

    [RelayCommand]
    public void SelectModule(ModuleTileItem? item)
    {
        if (item == null) return;
        _logger.LogInformation("Selected module: {Id} ({Title})", item.Id, item.Title);

        switch (item.Id)
        {
            case "quickdock.system":
                NavigationRequested?.Invoke("System");
                break;
            case "quickdock.clipboard":
                NavigationRequested?.Invoke("Clipboard");
                break;
            case "quickdock.timer":
                NavigationRequested?.Invoke("Timer");
                break;
            case "quickdock.notes":
                NavigationRequested?.Invoke("Notes");
                break;
            case "quickdock.calendar":
                NavigationRequested?.Invoke("Calendar");
                break;
            case "quickdock.weather":
                NavigationRequested?.Invoke("Weather");
                break;
            case "quickdock.links":
                NavigationRequested?.Invoke("Links");
                break;
            case "quickdock.screentime":
                NavigationRequested?.Invoke("ScreenTime");
                break;
            case "quickdock.fileshelf":
                NavigationRequested?.Invoke("FileShelf");
                break;
            case "quickdock.quickdrop":
                NavigationRequested?.Invoke("QuickDrop");
                break;
            case "quickdock.converter":
                NavigationRequested?.Invoke("Converter");
                break;
            case "quickdock.transfers":
                NavigationRequested?.Invoke("Transfers");
                break;
            case "quickdock.git":
                NavigationRequested?.Invoke("Git");
                break;
            case "quickdock.docker":
                NavigationRequested?.Invoke("Docker");
                break;
            case "quickdock.terminal":
                NavigationRequested?.Invoke("Terminal");
                break;
            case "quickdock.dev":
                NavigationRequested?.Invoke("DevTools");
                break;
            case "quickdock.http":
                NavigationRequested?.Invoke("Http");
                break;
            case "quickdock.localdev":
                NavigationRequested?.Invoke("LocalDev");
                break;
            case "quickdock.processes":
                NavigationRequested?.Invoke("Processes");
                break;
            case "quickdock.netoverview":
                NavigationRequested?.Invoke("NetworkOverview");
                break;
            case "quickdock.netadapters":
                NavigationRequested?.Invoke("Adapters");
                break;
            case "quickdock.dnsping":
                NavigationRequested?.Invoke("DnsPing");
                break;
            case "quickdock.netconns":
                NavigationRequested?.Invoke("Connections");
                break;
            case "quickdock.tls":
                NavigationRequested?.Invoke("Tls");
                break;
            case "quickdock.httpsec":
                NavigationRequested?.Invoke("Headers");
                break;
            case "quickdock.firewall":
                NavigationRequested?.Invoke("Firewall");
                break;
            case "quickdock.posture":
                NavigationRequested?.Invoke("Posture");
                break;
            case "quickdock.integrity":
                NavigationRequested?.Invoke("Integrity");
                break;
            case "quickdock.secreport":
                NavigationRequested?.Invoke("Report");
                break;
        }
    }
}
