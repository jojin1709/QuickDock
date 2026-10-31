using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Events;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Modules;
using QuickDock.Core.Settings;
using QuickDock.Infrastructure.Storage;

namespace QuickDock.App.ViewModels;

public sealed class SettingsNavItem
{
    public string Key { get; }
    public string Title { get; }
    public string IconGlyph { get; }

    public SettingsNavItem(string key, string title, string iconGlyph)
    {
        Key = key;
        Title = title;
        IconGlyph = iconGlyph;
    }
}

public sealed class SettingSearchItem
{
    public string SectionKey { get; }
    public string SectionName { get; }
    public string Title { get; }
    public string Description { get; }
    public string Keywords { get; }

    public SettingSearchItem(string sectionKey, string sectionName, string title, string description, string keywords)
    {
        SectionKey = sectionKey;
        SectionName = sectionName;
        Title = title;
        Description = description;
        Keywords = keywords;
    }
}

public sealed partial class ModuleSettingItem : ObservableObject
{
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public string Category { get; }
    public bool CanConfigure { get; }
    public string? ConfigureSectionKey { get; }

    [ObservableProperty]
    private bool _isEnabled;

    public ModuleSettingItem(string id, string name, string description, string category, bool isEnabled, bool canConfigure = false, string? configureSectionKey = null)
    {
        Id = id;
        Name = name;
        Description = description;
        Category = category;
        _isEnabled = isEnabled;
        CanConfigure = canConfigure;
        ConfigureSectionKey = configureSectionKey;
    }
}

public sealed partial class ShortcutDisplayItem : ObservableObject
{
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public string DefaultKeyGesture { get; }
    public bool IsGlobal { get; }

    [ObservableProperty]
    private string _currentKeyGesture;

    public ShortcutDisplayItem(ShortcutItem item)
    {
        Id = item.Id;
        Name = item.Name;
        Description = item.Description;
        _currentKeyGesture = item.CurrentKeyGesture;
        DefaultKeyGesture = item.DefaultKeyGesture;
        IsGlobal = item.IsGlobal;
    }
}

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsManager _settingsManager;
    private readonly IStartupService _startupService;
    private readonly IMonitorService _monitorService;
    private readonly IDatabaseService _databaseService;
    private readonly IStorageManagerService _storageManager;
    private readonly IShortcutManagerService _shortcutManager;
    private readonly IModuleManager _moduleManager;
    private readonly IEventBus _eventBus;
    private readonly ILogger<SettingsViewModel> _logger;

    private readonly List<SettingSearchItem> _searchIndex = [];
    private bool _isLoading;

    // Navigation
    [ObservableProperty]
    private string _selectedSection = "General";

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    public ObservableCollection<SettingsNavItem> NavItems { get; } = [];
    public ObservableCollection<SettingSearchItem> SearchResults { get; } = [];

    // General
    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _startMinimized;

    [ObservableProperty]
    private int _launchBehaviorIndex; // 0=ShowDock, 1=StartMinimized, 2=StartHidden

    [ObservableProperty]
    private int _closeBehaviorIndex; // 0=MinimizeToTray, 1=ExitApplication

    // Appearance
    [ObservableProperty]
    private int _selectedThemeIndex; // 0=System, 1=Light, 2=Dark

    [ObservableProperty]
    private int _selectedAccentIndex; // 0=Blue, 1=Purple, 2=Green, 3=Orange, 4=Red, 5=Custom

    [ObservableProperty]
    private string _accentCustomHex = "#0078D4";

    [ObservableProperty]
    private int _transparencyIndex; // 0=Off, 1=Subtle

    [ObservableProperty]
    private int _backdropIndex; // 0=Mica, 1=Acrylic, 2=Solid

    [ObservableProperty]
    private int _animationIndex; // 0=System, 1=Reduced, 2=Off

    // Dock
    [ObservableProperty]
    private int _dockPositionIndex; // 0=Top, 1=Bottom

    [ObservableProperty]
    private int _dockSizeIndex; // 0=Compact, 1=Normal, 2=Large

    [ObservableProperty]
    private bool _hoverExpansionEnabled = true;

    [ObservableProperty]
    private int _hoverDelayMs = 150;

    [ObservableProperty]
    private bool _alwaysOnTop = true;

    [ObservableProperty]
    private int _autoHideModeIndex = 2; // 0=Never, 1=FullscreenOnly, 2=Always

    [ObservableProperty]
    private int _selectedMonitorIndex = 0;

    public ObservableCollection<string> AvailableMonitors { get; } = [];

    // Modules
    public ObservableCollection<ModuleSettingItem> ProductivityModules { get; } = [];
    public ObservableCollection<ModuleSettingItem> FilesModules { get; } = [];
    public ObservableCollection<ModuleSettingItem> DeveloperModules { get; } = [];
    public ObservableCollection<ModuleSettingItem> NetworkSecurityModules { get; } = [];

    // Shortcuts
    public ObservableCollection<ShortcutDisplayItem> ShortcutsList { get; } = [];

    [ObservableProperty]
    private ShortcutDisplayItem? _selectedShortcutForEdit;

    [ObservableProperty]
    private string _editingKeyGesture = string.Empty;

    [ObservableProperty]
    private string _shortcutStatusMessage = string.Empty;

    // Notifications
    [ObservableProperty]
    private bool _notificationsEnabled = true;

    [ObservableProperty]
    private bool _timerNotificationsEnabled = true;

    [ObservableProperty]
    private bool _pomodoroNotificationsEnabled = true;

    [ObservableProperty]
    private bool _transferNotificationsEnabled = true;

    [ObservableProperty]
    private bool _quickDropNotificationsEnabled = true;

    [ObservableProperty]
    private bool _securityNotificationsEnabled = true;

    // Privacy
    [ObservableProperty]
    private bool _allowWeatherRequests = true;

    [ObservableProperty]
    private bool _allowPublicIpLookup = true;

    [ObservableProperty]
    private bool _allowHttpInspection = true;

    [ObservableProperty]
    private bool _allowDnsDiagnostics = true;

    [ObservableProperty]
    private bool _enableAnalytics = false;

    [ObservableProperty]
    private bool _clipboardSensitiveDataDetection = true;

    // Storage
    [ObservableProperty]
    private string _storageCacheSize = "Calculating...";

    [ObservableProperty]
    private string _storageLogsSize = "Calculating...";

    [ObservableProperty]
    private string _storageDatabaseSize = "Calculating...";

    [ObservableProperty]
    private string _storageTempSize = "Calculating...";

    [ObservableProperty]
    private string _storageDataSize = "Calculating...";

    [ObservableProperty]
    private string _storageTotalSize = "Calculating...";

    [ObservableProperty]
    private string _storageStatusMessage = string.Empty;

    // Network
    [ObservableProperty]
    private int _networkRequestTimeoutSeconds = 30;

    [ObservableProperty]
    private int _networkDnsTimeoutSeconds = 5;

    [ObservableProperty]
    private int _networkHttpTimeoutSeconds = 15;

    [ObservableProperty]
    private int _networkTlsTimeoutSeconds = 10;

    [ObservableProperty]
    private bool _externalIpLookupEnabled = true;

    // Developer
    [ObservableProperty]
    private int _logLevelIndex = 0; // 0=Information, 1=Debug, 2=Warning, 3=Error

    [ObservableProperty]
    private bool _developerDiagnosticsEnabled = false;

    [ObservableProperty]
    private bool _showDebugInfoEnabled = false;

    [ObservableProperty]
    private int _processRefreshRateSeconds = 5;

    [ObservableProperty]
    private string _defaultGitRepository = string.Empty;

    [ObservableProperty]
    private bool _confirmDestructiveGitActions = true;

    // Accessibility
    [ObservableProperty]
    private int _reducedMotionIndex = 0; // 0=System, 1=On, 2=Off

    [ObservableProperty]
    private int _textScaleIndex = 0; // 0=100%, 1=110%, 2=125%, 3=150%

    [ObservableProperty]
    private bool _showTooltipsEnabled = true;

    // About
    [ObservableProperty]
    private string _appVersion = "1.0.0";

    [ObservableProperty]
    private string _developerName = "JOJIN JOHN";

    [ObservableProperty]
    private string _copyrightText = "Copyright © 2026 JOJIN JOHN. All rights reserved.";

    [ObservableProperty]
    private string _licenseText = "Proprietary (All Rights Reserved)";

    [ObservableProperty]
    private string _gitHubRepoUrl = "https://github.com/jojin1709/QuickDock";

    [ObservableProperty]
    private string _linkedInUrl = "https://www.linkedin.com/in/jojin-john/";

    [ObservableProperty]
    private string _docsUrl = "docs/RELEASE_1.0.0.md";

    [ObservableProperty]
    private string _dotNetVersion = Environment.Version.ToString();

    [ObservableProperty]
    private string _storagePath = AppStoragePaths.ConfigFile;

    [ObservableProperty]
    private string _aboutUpdateMessage = string.Empty;

    // General status and import/export payload
    [ObservableProperty]
    private string _settingsStatusMessage = string.Empty;

    [ObservableProperty]
    private string _importExportJsonText = string.Empty;

    public SettingsViewModel(
        ISettingsManager settingsManager,
        IStartupService startupService,
        IMonitorService monitorService,
        IDatabaseService databaseService,
        IStorageManagerService storageManager,
        IShortcutManagerService shortcutManager,
        IModuleManager moduleManager,
        IEventBus eventBus,
        ILogger<SettingsViewModel> logger)
    {
        _settingsManager = settingsManager;
        _startupService = startupService;
        _monitorService = monitorService;
        _databaseService = databaseService;
        _storageManager = storageManager;
        _shortcutManager = shortcutManager;
        _moduleManager = moduleManager;
        _eventBus = eventBus;
        _logger = logger;

        InitializeNavigation();
        InitializeSearchIndex();
        LoadSettings();
    }

    private void InitializeNavigation()
    {
        NavItems.Clear();
        NavItems.Add(new SettingsNavItem("General", "General", "\uE713"));
        NavItems.Add(new SettingsNavItem("Appearance", "Appearance", "\uE790"));
        NavItems.Add(new SettingsNavItem("Dock", "Dock", "\uE8B7"));
        NavItems.Add(new SettingsNavItem("Modules", "Modules", "\uE74C"));
        NavItems.Add(new SettingsNavItem("Shortcuts", "Shortcuts", "\uE765"));
        NavItems.Add(new SettingsNavItem("Notifications", "Notifications", "\uEA8F"));
        NavItems.Add(new SettingsNavItem("Privacy", "Privacy", "\uE72E"));
        NavItems.Add(new SettingsNavItem("Storage", "Storage", "\uEDA2"));
        NavItems.Add(new SettingsNavItem("Network", "Network", "\uE839"));
        NavItems.Add(new SettingsNavItem("Developer", "Developer", "\uE7BE"));
        NavItems.Add(new SettingsNavItem("Accessibility", "Accessibility", "\uE776"));
        NavItems.Add(new SettingsNavItem("About", "About", "\uE946"));
    }

    private void InitializeSearchIndex()
    {
        _searchIndex.Clear();
        void Add(string secKey, string secName, string title, string desc, string kw) =>
            _searchIndex.Add(new SettingSearchItem(secKey, secName, title, desc, kw));

        Add("General", "General", "Start with Windows", "Launch QuickDock automatically when signing into Windows.", "startup boot windows login");
        Add("General", "General", "Start Minimized", "Keep dock in compact idle state upon launch.", "launch idle tray background");
        Add("General", "General", "Close Behavior", "Choose whether close button exits or minimizes to tray.", "close exit minimize tray");

        Add("Appearance", "Appearance", "Theme", "Select System default, Light mode, or Dark mode.", "theme light dark system appearance styling");
        Add("Appearance", "Appearance", "Accent Color", "Configure primary brand accent color across interface.", "accent color blue purple green orange red custom");
        Add("Appearance", "Appearance", "Backdrop & Transparency", "Choose Mica, Acrylic, or Solid window material.", "backdrop mica acrylic solid transparency glass blur");
        Add("Appearance", "Appearance", "Animation Level", "Configure transition effects or reduce motion.", "animation motion speed effects");

        Add("Dock", "Dock", "Dock Position", "Anchor QuickDock to the top or bottom of the screen.", "position top bottom anchor location");
        Add("Dock", "Dock", "Dock Sizing", "Adjust capsule width and height between Compact, Normal, and Large.", "size scale dimensions compact large width");
        Add("Dock", "Dock", "Hover Expansion & Delay", "Configure idle dock expansion on pointer hover.", "hover delay expand ms millisecond");
        Add("Dock", "Dock", "Always on Top", "Keep dock above standard desktop windows.", "topmost float always top zindex");
        Add("Dock", "Dock", "Auto-Hide & Fullscreen", "Suppress or collapse dock when focus is lost or playing fullscreen games.", "autohide fullscreen collapse game hide");
        Add("Dock", "Dock", "Target Monitor", "Follow active monitor or dock to specific display.", "monitor display screen multi display");

        Add("Modules", "Modules", "Productivity Modules", "Manage Clipboard, Timer, Notes, Calendar, Weather, Links, and Screen Time.", "modules clipboard timer notes calendar weather screentime enable disable");
        Add("Modules", "Modules", "File Transfer Modules", "Manage File Shelf, QuickDrop, Converter, and Transfers.", "files shelf quickdrop transfer converter");
        Add("Modules", "Modules", "Developer Modules", "Manage Git, Docker, Terminal, Dev Tools, HTTP, Ports, and Processes.", "git docker terminal http ports processes dev");
        Add("Modules", "Modules", "Network & Security Modules", "Manage Network diagnostics, DNS, TLS, Firewall, and Security audit.", "network security dns tls firewall defender");

        Add("Shortcuts", "Shortcuts", "Global Hotkey", "Default hotkey to toggle QuickDock expand and collapse.", "shortcut hotkey keybinding key ctrl space");
        Add("Shortcuts", "Shortcuts", "Productivity Shortcuts", "Keyboard keybindings for timers, notes, clipboard, and toolkits.", "shortcut keybind timer notes dev tools");

        Add("Notifications", "Notifications", "System Toast Notifications", "Master toggle and per-category notification filters.", "notifications toasts alert timer pomodoro transfer security");

        Add("Privacy", "Privacy", "External Network Requests", "Allow or block Weather, Public IP lookup, HTTP inspection, and DNS queries.", "privacy external telemetry network weather ip");
        Add("Privacy", "Privacy", "Sensitive Data Shield", "Mask API keys, JWTs, and private tokens in clipboard history.", "clipboard sensitive tokens masking security");

        Add("Storage", "Storage", "Application Storage & Cache", "Inspect cache, logs, database, and temporary files footprint.", "storage disk size clean clear cache logs database temp data");

        Add("Network", "Network", "Request Timeouts", "Configure DNS, HTTP, and TLS timeout boundaries.", "network timeout dns http tls latency");

        Add("Developer", "Developer", "Logging Level & Diagnostics", "Configure logging verbosity and developer diagnostic tools.", "developer logging loglevel debug trace info");

        Add("Accessibility", "Accessibility", "Reduced Motion & Text Scaling", "Adjust text zoom factor and respect system accessibility.", "accessibility text scale zoom font contrast reduced motion");

        Add("About", "About", "QuickDock Information", "View version, author, license, updates, and release documentation.", "about version author jojin john copyright github license update linkedin");
    }

    public void LoadSettings()
    {
        _isLoading = true;
        try
        {
            var s = _settingsManager.CurrentSettings;

            // General
            StartWithWindows = _startupService.IsStartupEnabled();
            StartMinimized = s.General.StartMinimized;
            LaunchBehaviorIndex = (int)s.General.LaunchBehavior;
            CloseBehaviorIndex = (int)s.General.CloseBehavior;

            // Appearance
            SelectedThemeIndex = s.Appearance.Theme switch
            {
                AppTheme.Light => 1,
                AppTheme.Dark => 2,
                _ => 0
            };

            SelectedAccentIndex = s.Appearance.AccentName switch
            {
                "Purple" => 1,
                "Green" => 2,
                "Orange" => 3,
                "Red" => 4,
                "Custom" => 5,
                _ => 0
            };
            AccentCustomHex = s.Appearance.AccentColor;
            TransparencyIndex = (int)s.Appearance.Transparency;
            BackdropIndex = (int)s.Appearance.Backdrop;
            AnimationIndex = s.Appearance.Animation switch
            {
                AnimationLevel.Reduced => 1,
                AnimationLevel.Off => 2,
                _ => 0
            };

            // Dock
            DockPositionIndex = (int)s.Behavior.Position;
            DockSizeIndex = (int)s.Behavior.Size;
            HoverExpansionEnabled = s.Behavior.HoverExpansionEnabled;
            HoverDelayMs = s.Behavior.HoverDelayMs;
            AlwaysOnTop = s.Behavior.AlwaysOnTop;
            AutoHideModeIndex = (int)s.Behavior.AutoHideMode;

            // Monitor
            AvailableMonitors.Clear();
            AvailableMonitors.Add("Follow Active Monitor");
            AvailableMonitors.Add("Primary Monitor");
            var monitors = _monitorService.GetAllMonitors();
            for (int i = 0; i < monitors.Count; i++)
            {
                AvailableMonitors.Add(monitors[i].DeviceName);
            }

            if (s.Behavior.FollowActiveMonitor)
            {
                SelectedMonitorIndex = 0;
            }
            else if (string.Equals(s.Behavior.MonitorTarget, "Primary", StringComparison.OrdinalIgnoreCase))
            {
                SelectedMonitorIndex = 1;
            }
            else
            {
                int matchIdx = -1;
                for (int i = 0; i < monitors.Count; i++)
                {
                    if (string.Equals(monitors[i].DeviceId, s.Behavior.MonitorTarget, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(monitors[i].DeviceName, s.Behavior.MonitorTarget, StringComparison.OrdinalIgnoreCase))
                    {
                        matchIdx = i + 2;
                        break;
                    }
                }
                SelectedMonitorIndex = matchIdx >= 0 ? matchIdx : 0;
            }

            // Modules
            PopulateModules(s);

            // Shortcuts
            PopulateShortcuts();

            // Notifications
            NotificationsEnabled = s.Notifications.Enabled;
            TimerNotificationsEnabled = s.Notifications.TimerNotifications;
            PomodoroNotificationsEnabled = s.Notifications.PomodoroNotifications;
            TransferNotificationsEnabled = s.Notifications.TransferNotifications;
            QuickDropNotificationsEnabled = s.Notifications.QuickDropNotifications;
            SecurityNotificationsEnabled = s.Notifications.SecurityNotifications;

            // Privacy
            AllowWeatherRequests = s.Privacy.AllowWeatherRequests;
            AllowPublicIpLookup = s.Privacy.AllowPublicIpLookup;
            AllowHttpInspection = s.Privacy.AllowHttpInspection;
            AllowDnsDiagnostics = s.Privacy.AllowDnsDiagnostics;
            EnableAnalytics = false; // Always false by design
            ClipboardSensitiveDataDetection = s.Privacy.ClipboardSensitiveDataDetection;

            // Network
            NetworkRequestTimeoutSeconds = s.Network.RequestTimeoutSeconds;
            NetworkDnsTimeoutSeconds = s.Network.DnsTimeoutSeconds;
            NetworkHttpTimeoutSeconds = s.Network.HttpTimeoutSeconds;
            NetworkTlsTimeoutSeconds = s.Network.TlsTimeoutSeconds;
            ExternalIpLookupEnabled = s.Network.ExternalIpLookupEnabled;

            // Developer
            LogLevelIndex = s.Developer.LogLevel.ToLowerInvariant() switch
            {
                "debug" => 1,
                "warning" => 2,
                "error" => 3,
                _ => 0
            };
            DeveloperDiagnosticsEnabled = s.Developer.DeveloperDiagnostics;
            ShowDebugInfoEnabled = s.Developer.ShowDebugInfo;
            ProcessRefreshRateSeconds = s.Developer.ProcessMonitorRefreshRateSeconds;
            DefaultGitRepository = s.Developer.DefaultGitRepository;
            ConfirmDestructiveGitActions = s.Developer.ConfirmDestructiveGitActions;

            // Accessibility
            ReducedMotionIndex = (int)s.Accessibility.ReducedMotion;
            TextScaleIndex = s.Accessibility.TextScalePercent switch
            {
                110 => 1,
                125 => 2,
                150 => 3,
                _ => 0
            };
            ShowTooltipsEnabled = s.Accessibility.ShowTooltips;

            // Refresh storage asynchronously
            _ = RefreshStorageStatsAsync();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void PopulateModules(QuickDockSettings s)
    {
        ProductivityModules.Clear();
        FilesModules.Clear();
        DeveloperModules.Clear();
        NetworkSecurityModules.Clear();

        void AddProd(string id, string name, string desc, bool canCfg = false, string? cfgSec = null)
        {
            bool enabled = !s.EnabledModules.TryGetValue(id, out bool val) || val;
            var item = new ModuleSettingItem(id, name, desc, "Productivity", enabled, canCfg, cfgSec);
            item.PropertyChanged += async (o, e) =>
            {
                if (e.PropertyName == nameof(ModuleSettingItem.IsEnabled))
                    await HandleModuleToggledAsync(item.Id, item.IsEnabled);
            };
            ProductivityModules.Add(item);
        }

        void AddFile(string id, string name, string desc, bool canCfg = false, string? cfgSec = null)
        {
            bool enabled = !s.EnabledModules.TryGetValue(id, out bool val) || val;
            var item = new ModuleSettingItem(id, name, desc, "Files", enabled, canCfg, cfgSec);
            item.PropertyChanged += async (o, e) =>
            {
                if (e.PropertyName == nameof(ModuleSettingItem.IsEnabled))
                    await HandleModuleToggledAsync(item.Id, item.IsEnabled);
            };
            FilesModules.Add(item);
        }

        void AddDev(string id, string name, string desc, bool canCfg = false, string? cfgSec = null)
        {
            bool enabled = !s.EnabledModules.TryGetValue(id, out bool val) || val;
            var item = new ModuleSettingItem(id, name, desc, "Developer", enabled, canCfg, cfgSec);
            item.PropertyChanged += async (o, e) =>
            {
                if (e.PropertyName == nameof(ModuleSettingItem.IsEnabled))
                    await HandleModuleToggledAsync(item.Id, item.IsEnabled);
            };
            DeveloperModules.Add(item);
        }

        void AddNetSec(string id, string name, string desc, bool canCfg = false, string? cfgSec = null)
        {
            bool enabled = !s.EnabledModules.TryGetValue(id, out bool val) || val;
            var item = new ModuleSettingItem(id, name, desc, "NetworkSecurity", enabled, canCfg, cfgSec);
            item.PropertyChanged += async (o, e) =>
            {
                if (e.PropertyName == nameof(ModuleSettingItem.IsEnabled))
                    await HandleModuleToggledAsync(item.Id, item.IsEnabled);
            };
            NetworkSecurityModules.Add(item);
        }

        // 1. Productivity (10)
        AddProd("quickdock.clipboard", "Clipboard Manager", "Clipboard history with sensitive credential masking and pinning.", true, "Privacy");
        AddProd("quickdock.timer", "Timer & Stopwatch", "Multi-timer countdowns, precision stopwatch, and audio alarms.", true, "Notifications");
        AddProd("quickdock.notes", "Quick Notes", "Local markdown notes with debounced SQLite auto-saving.");
        AddProd("quickdock.calendar", "Calendar Agenda", "Event scheduler with date pickers and priority tags.");
        AddProd("quickdock.weather", "Weather Forecast", "Weather metrics via Open-Meteo with offline disk cache.", true, "Privacy");
        AddProd("quickdock.links", "Saved Links", "Categorized bookmarks library with default browser launcher.");
        AddProd("quickdock.temporarymessage", "Temporary Message", "Ephemeral dock banner widget with customizable expiration.");
        AddProd("quickdock.screentime", "Screen Time", "Low-frequency foreground application usage analytics.");

        // 2. Files (4)
        AddFile("quickdock.fileshelf", "File Shelf", "Drag and drop staging area preserving original files.");
        AddFile("quickDrop.quickdrop", "QuickDrop", "Local Wi-Fi HTTP direct device transfer with dynamic QR pairing.", true, "Storage");
        AddFile("quickdock.converter", "File Converter", "Format transcoding for images, archives, and text encodings.");
        AddFile("quickdock.transfers", "Transfer Manager", "Real-time streaming transfer pipeline with SHA-256 integrity.");

        // 3. Developer (7)
        AddDev("quickdock.git", "Git Toolkit", "Local Git branch status, staging, diffs, and push/pull.", true, "Developer");
        AddDev("quickdock.docker", "Docker Toolkit", "Docker container lifecycle, logs, inspection, and image viewer.");
        AddDev("quickdock.terminal", "Terminal Launcher", "One-click launcher for Windows Terminal, PowerShell, and Command Prompt.");
        AddDev("quickdock.devtools", "Developer Utilities", "Offline JSON, JWT, Base64, Hashing, UUID, Timestamp, Regex, and QR tools.");
        AddDev("quickdock.http", "HTTP / REST Client", "Local REST API client with method selection and response metrics.");
        AddDev("quickdock.localdev", "Localhost & Ports", "Native inspection of listening TCP ports and localhost URLs.", true, "Developer");
        AddDev("quickdock.processes", "Process Monitor", "Developer process memory inspection, port mapping, and termination.", true, "Developer");

        // 4. Network & Security (5 registered modules / 14 features)
        AddNetSec("quickdock.networkdiag", "Network Diagnostics", "Local network adapters, IPv4/IPv6 classification, and ping path tests.", true, "Network");
        AddNetSec("quickdock.securitydiag", "Security Diagnostics", "Defensive host checks, UAC, Secure Boot, and audit report generator.", true, "Privacy");
        AddNetSec("quickdock.tls", "TLS & Certificate Inspector", "Direct TLS handshake negotiation and certificate chain inspection.", true, "Network");
        AddNetSec("quickdock.httpsecurity", "HTTP Security Headers", "Web application security header inspection (CSP, HSTS, XFO).", true, "Network");
        AddNetSec("quickdock.firewall", "Firewall & Defender Status", "Windows Defender Firewall profiles and Defender service status.");
    }

    private void PopulateShortcuts()
    {
        ShortcutsList.Clear();
        foreach (var item in _shortcutManager.GetShortcuts())
        {
            ShortcutsList.Add(new ShortcutDisplayItem(item));
        }
    }

    private async Task HandleModuleToggledAsync(string moduleId, bool isEnabled)
    {
        var settings = _settingsManager.CurrentSettings;
        settings.EnabledModules[moduleId] = isEnabled;

        if (isEnabled)
        {
            await _moduleManager.EnableModuleAsync(moduleId);
        }
        else
        {
            await _moduleManager.DisableModuleAsync(moduleId);
        }

        await _settingsManager.SaveAsync(settings, "Modules");
        _logger.LogInformation("Module {Id} updated to IsEnabled={IsEnabled}", moduleId, isEnabled);
    }

    partial void OnSearchQueryChanged(string value)
    {
        SearchResults.Clear();
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        string query = value.Trim().ToLowerInvariant();
        var matches = _searchIndex
            .Where(x => x.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        x.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        x.SectionName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        x.Keywords.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(12);

        foreach (var m in matches)
        {
            SearchResults.Add(m);
        }
    }

    [RelayCommand]
    private void SelectSearchResult(SettingSearchItem? item)
    {
        if (item == null) return;
        SelectedSection = item.SectionKey;
        SearchQuery = string.Empty;
    }

    [RelayCommand]
    private void SelectSection(string sectionKey)
    {
        if (!string.IsNullOrWhiteSpace(sectionKey))
        {
            SelectedSection = sectionKey;
        }
    }

    // Property Change Handlers
    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_isLoading) return;
        bool ok = _startupService.SetStartupEnabled(value);
        if (ok)
        {
            var s = _settingsManager.CurrentSettings;
            s.General.StartWithWindows = value;
            s.Behavior.StartWithWindows = value;
            _ = SaveSettingsAndPublishAsync("General");
        }
    }

    partial void OnStartMinimizedChanged(bool value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.General.StartMinimized = value;
        _ = SaveSettingsAndPublishAsync("General");
    }

    partial void OnLaunchBehaviorIndexChanged(int value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.General.LaunchBehavior = (LaunchBehavior)Math.Clamp(value, 0, 2);
        _ = SaveSettingsAndPublishAsync("General");
    }

    partial void OnCloseBehaviorIndexChanged(int value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.General.CloseBehavior = (CloseBehavior)Math.Clamp(value, 0, 1);
        _ = SaveSettingsAndPublishAsync("General");
    }

    partial void OnSelectedThemeIndexChanged(int value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Appearance.Theme = value switch
        {
            1 => AppTheme.Light,
            2 => AppTheme.Dark,
            _ => AppTheme.System
        };
        _ = SaveSettingsAndPublishAsync("Appearance");
    }

    partial void OnSelectedAccentIndexChanged(int value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        var (name, hex) = value switch
        {
            1 => ("Purple", "#8764B8"),
            2 => ("Green", "#107C41"),
            3 => ("Orange", "#CA5010"),
            4 => ("Red", "#D13438"),
            5 => ("Custom", AccentCustomHex),
            _ => ("Blue", "#0078D4")
        };
        s.Appearance.AccentName = name;
        s.Appearance.AccentColor = hex;
        _ = SaveSettingsAndPublishAsync("Appearance");
    }

    partial void OnTransparencyIndexChanged(int value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Appearance.Transparency = (TransparencyLevel)Math.Clamp(value, 0, 1);
        _ = SaveSettingsAndPublishAsync("Appearance");
    }

    partial void OnBackdropIndexChanged(int value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Appearance.Backdrop = (BackdropType)Math.Clamp(value, 0, 2);
        _ = SaveSettingsAndPublishAsync("Appearance");
    }

    partial void OnAnimationIndexChanged(int value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Appearance.Animation = value switch
        {
            1 => AnimationLevel.Reduced,
            2 => AnimationLevel.Off,
            _ => AnimationLevel.Normal
        };
        _ = SaveSettingsAndPublishAsync("Appearance");
    }

    partial void OnDockPositionIndexChanged(int value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Behavior.Position = (DockPosition)Math.Clamp(value, 0, 1);
        _ = SaveSettingsAndPublishAsync("Dock");
    }

    partial void OnDockSizeIndexChanged(int value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Behavior.Size = (DockSize)Math.Clamp(value, 0, 2);
        _ = SaveSettingsAndPublishAsync("Dock");
    }

    partial void OnHoverExpansionEnabledChanged(bool value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Behavior.HoverExpansionEnabled = value;
        _ = SaveSettingsAndPublishAsync("Dock");
    }

    partial void OnHoverDelayMsChanged(int value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Behavior.HoverDelayMs = Math.Clamp(value, 50, 1000);
        _ = SaveSettingsAndPublishAsync("Dock");
    }

    partial void OnAlwaysOnTopChanged(bool value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Behavior.AlwaysOnTop = value;
        _ = SaveSettingsAndPublishAsync("Dock");
    }

    partial void OnAutoHideModeIndexChanged(int value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Behavior.AutoHideMode = (AutoHideMode)Math.Clamp(value, 0, 2);
        s.Behavior.AutoHide = s.Behavior.AutoHideMode != AutoHideMode.Never;
        s.Behavior.HideInFullscreen = s.Behavior.AutoHideMode != AutoHideMode.Never;
        _ = SaveSettingsAndPublishAsync("Dock");
    }

    partial void OnSelectedMonitorIndexChanged(int value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        if (value == 0)
        {
            s.Behavior.FollowActiveMonitor = true;
            s.Behavior.MonitorTarget = "Active";
        }
        else if (value == 1)
        {
            s.Behavior.FollowActiveMonitor = false;
            s.Behavior.MonitorTarget = "Primary";
        }
        else
        {
            int monIdx = value - 2;
            var monitors = _monitorService.GetAllMonitors();
            if (monIdx >= 0 && monIdx < monitors.Count)
            {
                s.Behavior.FollowActiveMonitor = false;
                s.Behavior.MonitorTarget = monitors[monIdx].DeviceId;
            }
        }
        _ = SaveSettingsAndPublishAsync("Dock");
    }

    // Notifications
    partial void OnNotificationsEnabledChanged(bool value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Notifications.Enabled = value;
        _ = SaveSettingsAndPublishAsync("Notifications");
    }

    partial void OnTimerNotificationsEnabledChanged(bool value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Notifications.TimerNotifications = value;
        _ = SaveSettingsAndPublishAsync("Notifications");
    }

    partial void OnPomodoroNotificationsEnabledChanged(bool value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Notifications.PomodoroNotifications = value;
        _ = SaveSettingsAndPublishAsync("Notifications");
    }

    partial void OnTransferNotificationsEnabledChanged(bool value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Notifications.TransferNotifications = value;
        _ = SaveSettingsAndPublishAsync("Notifications");
    }

    partial void OnQuickDropNotificationsEnabledChanged(bool value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Notifications.QuickDropNotifications = value;
        _ = SaveSettingsAndPublishAsync("Notifications");
    }

    partial void OnSecurityNotificationsEnabledChanged(bool value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Notifications.SecurityNotifications = value;
        _ = SaveSettingsAndPublishAsync("Notifications");
    }

    // Privacy
    partial void OnAllowWeatherRequestsChanged(bool value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Privacy.AllowWeatherRequests = value;
        _ = SaveSettingsAndPublishAsync("Privacy");
    }

    partial void OnAllowPublicIpLookupChanged(bool value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Privacy.AllowPublicIpLookup = value;
        _ = SaveSettingsAndPublishAsync("Privacy");
    }

    partial void OnAllowHttpInspectionChanged(bool value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Privacy.AllowHttpInspection = value;
        _ = SaveSettingsAndPublishAsync("Privacy");
    }

    partial void OnAllowDnsDiagnosticsChanged(bool value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Privacy.AllowDnsDiagnostics = value;
        _ = SaveSettingsAndPublishAsync("Privacy");
    }

    partial void OnClipboardSensitiveDataDetectionChanged(bool value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Privacy.ClipboardSensitiveDataDetection = value;
        _ = SaveSettingsAndPublishAsync("Privacy");
    }

    // Network
    partial void OnNetworkRequestTimeoutSecondsChanged(int value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Network.RequestTimeoutSeconds = Math.Clamp(value, 5, 120);
        _ = SaveSettingsAndPublishAsync("Network");
    }

    partial void OnNetworkDnsTimeoutSecondsChanged(int value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Network.DnsTimeoutSeconds = Math.Clamp(value, 1, 30);
        _ = SaveSettingsAndPublishAsync("Network");
    }

    partial void OnNetworkHttpTimeoutSecondsChanged(int value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Network.HttpTimeoutSeconds = Math.Clamp(value, 2, 60);
        _ = SaveSettingsAndPublishAsync("Network");
    }

    partial void OnNetworkTlsTimeoutSecondsChanged(int value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Network.TlsTimeoutSeconds = Math.Clamp(value, 2, 30);
        _ = SaveSettingsAndPublishAsync("Network");
    }

    partial void OnExternalIpLookupEnabledChanged(bool value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Network.ExternalIpLookupEnabled = value;
        _ = SaveSettingsAndPublishAsync("Network");
    }

    // Developer
    partial void OnLogLevelIndexChanged(int value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Developer.LogLevel = value switch
        {
            1 => "Debug",
            2 => "Warning",
            3 => "Error",
            _ => "Information"
        };
        _ = SaveSettingsAndPublishAsync("Developer");
    }

    partial void OnDeveloperDiagnosticsEnabledChanged(bool value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Developer.DeveloperDiagnostics = value;
        _ = SaveSettingsAndPublishAsync("Developer");
    }

    partial void OnShowDebugInfoEnabledChanged(bool value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Developer.ShowDebugInfo = value;
        _ = SaveSettingsAndPublishAsync("Developer");
    }

    partial void OnProcessRefreshRateSecondsChanged(int value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Developer.ProcessMonitorRefreshRateSeconds = Math.Clamp(value, 2, 30);
        _ = SaveSettingsAndPublishAsync("Developer");
    }

    partial void OnDefaultGitRepositoryChanged(string value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Developer.DefaultGitRepository = value;
        _ = SaveSettingsAndPublishAsync("Developer");
    }

    partial void OnConfirmDestructiveGitActionsChanged(bool value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Developer.ConfirmDestructiveGitActions = value;
        _ = SaveSettingsAndPublishAsync("Developer");
    }

    // Accessibility
    partial void OnReducedMotionIndexChanged(int value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Accessibility.ReducedMotion = (ReducedMotionSetting)Math.Clamp(value, 0, 2);
        _ = SaveSettingsAndPublishAsync("Accessibility");
    }

    partial void OnTextScaleIndexChanged(int value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Accessibility.TextScalePercent = value switch
        {
            1 => 110,
            2 => 125,
            3 => 150,
            _ => 100
        };
        _ = SaveSettingsAndPublishAsync("Accessibility");
    }

    partial void OnShowTooltipsEnabledChanged(bool value)
    {
        if (_isLoading) return;
        var s = _settingsManager.CurrentSettings;
        s.Accessibility.ShowTooltips = value;
        _ = SaveSettingsAndPublishAsync("Accessibility");
    }

    private async Task SaveSettingsAndPublishAsync(string sectionName)
    {
        var s = _settingsManager.CurrentSettings;
        await _settingsManager.SaveAsync(s, sectionName);
        await _eventBus.PublishAsync(new SettingsChangedEvent(s, sectionName));
        SettingsStatusMessage = $"Changes to {sectionName} saved immediately.";
    }

    // Storage Actions
    [RelayCommand]
    public async Task RefreshStorageStatsAsync()
    {
        try
        {
            var info = await _storageManager.GetStorageBreakdownAsync();
            StorageCacheSize = info.FormattedCacheSize;
            StorageLogsSize = info.FormattedLogsSize;
            StorageDatabaseSize = info.FormattedDatabaseSize;
            StorageTempSize = info.FormattedTemporarySize;
            StorageDataSize = info.FormattedDataSize;
            StorageTotalSize = info.FormattedTotalSize;
            StorageStatusMessage = "Storage metrics refreshed.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh storage breakdown.");
        }
    }

    [RelayCommand]
    public async Task ClearCacheAsync()
    {
        await _storageManager.ClearCacheAsync();
        await RefreshStorageStatsAsync();
        StorageStatusMessage = "Cache directory cleared successfully.";
    }

    [RelayCommand]
    public async Task ClearLogsAsync()
    {
        await _storageManager.ClearLogsAsync();
        await RefreshStorageStatsAsync();
        StorageStatusMessage = "Logs directory cleared.";
    }

    [RelayCommand]
    public async Task ClearTemporaryDataAsync()
    {
        await _storageManager.ClearTemporaryDataAsync();
        await RefreshStorageStatsAsync();
        StorageStatusMessage = "Temporary staging files cleared.";
    }

    [RelayCommand]
    public async Task ClearProductivityDataAsync()
    {
        await _storageManager.ClearProductivityDataAsync();
        await RefreshStorageStatsAsync();
        StorageStatusMessage = "Productivity records (Notes, Calendar, Screen Time) cleared.";
    }

    [RelayCommand]
    public void OpenDataFolder()
    {
        _storageManager.OpenDataFolder();
    }

    // Shortcuts Actions
    [RelayCommand]
    public void SelectShortcutForEdit(ShortcutDisplayItem? item)
    {
        SelectedShortcutForEdit = item;
        EditingKeyGesture = item?.CurrentKeyGesture ?? string.Empty;
        ShortcutStatusMessage = string.Empty;
    }

    [RelayCommand]
    public void SaveEditedShortcut()
    {
        if (SelectedShortcutForEdit == null) return;

        var res = _shortcutManager.UpdateShortcut(SelectedShortcutForEdit.Id, EditingKeyGesture);
        if (res.Success)
        {
            SelectedShortcutForEdit.CurrentKeyGesture = EditingKeyGesture;
            ShortcutStatusMessage = $"Shortcut '{SelectedShortcutForEdit.Name}' successfully updated to '{EditingKeyGesture}'.";
            SelectedShortcutForEdit = null;
        }
        else
        {
            ShortcutStatusMessage = res.ErrorMessage ?? "Failed to update shortcut.";
        }
    }

    [RelayCommand]
    public void ResetAllShortcuts()
    {
        _shortcutManager.ResetToDefaults();
        PopulateShortcuts();
        ShortcutStatusMessage = "All shortcuts restored to defaults.";
    }

    // Backup & Import/Export Actions
    [RelayCommand]
    public async Task ExportSettingsAsync()
    {
        try
        {
            string json = await _settingsManager.ExportSettingsJsonAsync();
            ImportExportJsonText = json;
            SettingsStatusMessage = "Settings configuration exported to JSON successfully.";
        }
        catch (Exception ex)
        {
            SettingsStatusMessage = $"Export error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task ImportSettingsAsync()
    {
        if (string.IsNullOrWhiteSpace(ImportExportJsonText))
        {
            SettingsStatusMessage = "Please paste valid JSON configuration into the payload box.";
            return;
        }

        var res = await _settingsManager.ImportSettingsJsonAsync(ImportExportJsonText);
        if (res.Success)
        {
            LoadSettings();
            SettingsStatusMessage = $"Import successful ({res.ImportedKeysCount} modules restored).";
        }
        else
        {
            SettingsStatusMessage = res.ErrorMessage ?? "Import failed.";
        }
    }

    // Reset Actions
    [RelayCommand]
    public async Task ResetCurrentSectionAsync()
    {
        await _settingsManager.ResetSectionAsync(SelectedSection);
        LoadSettings();
        SettingsStatusMessage = $"Section '{SelectedSection}' reset to defaults.";
    }

    [RelayCommand]
    public async Task ResetAllSettingsAsync()
    {
        await _settingsManager.ResetToDefaultsAsync();
        _startupService.SetStartupEnabled(false);
        LoadSettings();
        await _eventBus.PublishAsync(new SettingsChangedEvent(_settingsManager.CurrentSettings, "Reset"));
        SettingsStatusMessage = "All QuickDock settings have been restored to defaults.";
    }

    [RelayCommand]
    public void CheckForUpdates()
    {
        AboutUpdateMessage = "Update checking will be available in the release system.";
    }
}
