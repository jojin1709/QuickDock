using System.Text.Json.Serialization;

namespace QuickDock.Core.Settings;

public enum AppTheme
{
    System,
    Light,
    Dark
}

public enum AnimationLevel
{
    Normal,
    Reduced,
    Off
}

public enum PerformanceMode
{
    Balanced,
    LowPower,
    Minimal
}

public enum DockPosition
{
    Top,
    Bottom
}

public enum DockAlignment
{
    Center
}

public enum DockSize
{
    Compact,
    Normal,
    Large
}

public enum AutoHideMode
{
    Never,
    FullscreenOnly,
    Always
}

public enum LaunchBehavior
{
    ShowDock,
    StartMinimized,
    StartHidden
}

public enum CloseBehavior
{
    MinimizeToTray,
    ExitApplication
}

public enum BackdropType
{
    Mica,
    Acrylic,
    Solid
}

public enum TransparencyLevel
{
    Off,
    Subtle
}

public enum ReducedMotionSetting
{
    System,
    On,
    Off
}

public sealed class GeneralSettings
{
    public bool StartWithWindows { get; set; } = false;
    public bool StartMinimized { get; set; } = false;
    public LaunchBehavior LaunchBehavior { get; set; } = LaunchBehavior.ShowDock;
    public CloseBehavior CloseBehavior { get; set; } = CloseBehavior.MinimizeToTray;
}

public sealed class AppearanceSettings
{
    public AppTheme Theme { get; set; } = AppTheme.System;
    public string AccentName { get; set; } = "Blue";
    public string AccentColor { get; set; } = "#0078D4";
    public AnimationLevel Animation { get; set; } = AnimationLevel.Normal;
    public double Opacity { get; set; } = 0.98;
    public BackdropType Backdrop { get; set; } = BackdropType.Mica;
    public TransparencyLevel Transparency { get; set; } = TransparencyLevel.Subtle;
}

public sealed class BehaviorSettings
{
    public string GlobalHotkey { get; set; } = "Ctrl+Space";
    public bool AutoHide { get; set; } = true;
    public AutoHideMode AutoHideMode { get; set; } = AutoHideMode.Always;
    public int HoverDelayMs { get; set; } = 150;
    public bool HoverExpansionEnabled { get; set; } = true;
    public bool DetectFullscreen { get; set; } = true;
    public bool HideInFullscreen { get; set; } = true;
    public bool AlwaysOnTop { get; set; } = true;
    public bool FollowActiveMonitor { get; set; } = true;
    public string MonitorTarget { get; set; } = "Active";
    public bool StartWithWindows { get; set; } = false;
    public DockPosition Position { get; set; } = DockPosition.Top;
    public DockAlignment Alignment { get; set; } = DockAlignment.Center;
    public DockSize Size { get; set; } = DockSize.Normal;
}

public sealed class PerformanceSettings
{
    public PerformanceMode Mode { get; set; } = PerformanceMode.Balanced;
    public bool DetailedMonitoringVisibleOnly { get; set; } = true;
    public int BackgroundRefreshIntervalSeconds { get; set; } = 60;
}

public sealed class PrivacySettings
{
    public bool EnableClipboardHistory { get; set; } = true;
    public int ClipboardRetentionCount { get; set; } = 50;
    public bool EnableActivityTracking { get; set; } = false;
    public bool ClearDataOnExit { get; set; } = false;
    public bool AllowWeatherRequests { get; set; } = true;
    public bool AllowPublicIpLookup { get; set; } = true;
    public bool AllowHttpInspection { get; set; } = true;
    public bool AllowDnsDiagnostics { get; set; } = true;
    public bool EnableAnalytics { get; set; } = false;
    public bool ClipboardSensitiveDataDetection { get; set; } = true;
}

public sealed class ShortcutSettings
{
    public string OpenDock { get; set; } = "Ctrl+Space";
    public string NewTimer { get; set; } = "Ctrl+Shift+T";
    public string OpenClipboard { get; set; } = "Ctrl+Shift+V";
    public string OpenNotes { get; set; } = "Ctrl+Shift+N";
    public string OpenDevToolkit { get; set; } = "Ctrl+Shift+D";
    public string OpenNetworkSecurity { get; set; } = "Ctrl+Shift+S";
}

public sealed class NotificationSettings
{
    public bool Enabled { get; set; } = true;
    public bool TimerNotifications { get; set; } = true;
    public bool PomodoroNotifications { get; set; } = true;
    public bool TransferNotifications { get; set; } = true;
    public bool QuickDropNotifications { get; set; } = true;
    public bool SecurityNotifications { get; set; } = true;
}

public sealed class NetworkPreferencesSettings
{
    public int RequestTimeoutSeconds { get; set; } = 30;
    public int DnsTimeoutSeconds { get; set; } = 5;
    public int HttpTimeoutSeconds { get; set; } = 15;
    public int TlsTimeoutSeconds { get; set; } = 10;
    public bool ExternalIpLookupEnabled { get; set; } = true;
}

public sealed class DeveloperPreferencesSettings
{
    public string LogLevel { get; set; } = "Information";
    public bool DeveloperDiagnostics { get; set; } = false;
    public bool ShowDebugInfo { get; set; } = false;
    public int ProcessMonitorRefreshRateSeconds { get; set; } = 5;
    public string DefaultGitRepository { get; set; } = string.Empty;
    public bool ConfirmDestructiveGitActions { get; set; } = true;
}

public sealed class AccessibilitySettings
{
    public ReducedMotionSetting ReducedMotion { get; set; } = ReducedMotionSetting.System;
    public int TextScalePercent { get; set; } = 100;
    public bool HighContrast { get; set; } = false;
    public bool ShowTooltips { get; set; } = true;
}

public sealed class ClockSettings
{
    public bool Use24HourFormat { get; set; } = true;
    public bool ShowSeconds { get; set; } = false;
    public string SelectedTimeZoneId { get; set; } = string.Empty; // Empty = Local
}

public enum SensitiveDataHandling
{
    Ignore,
    StoreNormally,
    RequireConfirmation
}

public enum ClipboardRetention
{
    Hours1,
    Hours6,
    Days1,
    Days7,
    Days30
}

public sealed class ClipboardModuleSettings
{
    public bool Enabled { get; set; } = true;
    public ClipboardRetention Retention { get; set; } = ClipboardRetention.Days7;
    public int MaxItems { get; set; } = 50;
    public bool IsPaused { get; set; } = false;
    public SensitiveDataHandling SensitiveAction { get; set; } = SensitiveDataHandling.Ignore;
}

public sealed class TimerModuleSettings
{
    public int DefaultTimerMinutes { get; set; } = 15;
    public bool PlaySoundOnComplete { get; set; } = true;
    public int PomodoroWorkMinutes { get; set; } = 25;
    public int PomodoroShortBreakMinutes { get; set; } = 5;
    public int PomodoroLongBreakMinutes { get; set; } = 15;
    public int PomodoroSessionsBeforeLongBreak { get; set; } = 4;
    public bool PomodoroAutoStartBreaks { get; set; } = false;
    public bool PomodoroAutoStartWork { get; set; } = false;
}

public sealed class NotesModuleSettings
{
    public int AutosaveDebounceMs { get; set; } = 600;
}

public sealed class CalendarModuleSettings
{
    public bool WeekStartsOnMonday { get; set; } = true;
    public bool Use24HourFormat { get; set; } = true;
}

public sealed class WeatherModuleSettings
{
    public bool UseAutomaticLocation { get; set; } = true;
    public string ManualLocationName { get; set; } = "Thrissur";
    public double? ManualLatitude { get; set; } = 10.5276;
    public double? ManualLongitude { get; set; } = 76.2144;
    public int RefreshIntervalMinutes { get; set; } = 30;
    public string Provider { get; set; } = "OpenMeteo";
    public string ApiKey { get; set; } = string.Empty;
}

public sealed class ScreenTimeModuleSettings
{
    public bool TrackingEnabled { get; set; } = true;
    public int SamplingIntervalSeconds { get; set; } = 2;
}

public sealed class FileShelfModuleSettings
{
    public int MaxItems { get; set; } = 50;
    public bool AutoRemoveMissing { get; set; } = false;
}

public sealed class QuickDropModuleSettings
{
    public bool IsEnabled { get; set; } = true;
    public int SessionTimeoutMinutes { get; set; } = 10;
    public long MaxFileSizeBytes { get; set; } = 2147483648L; // 2 GB
    public int MaxSimultaneousTransfers { get; set; } = 3;
    public bool AutoCloseAfterTransfer { get; set; } = false;
    public int PreferredPort { get; set; } = 48271;
}

public sealed class FileConverterModuleSettings
{
    public string? DefaultOutputDirectory { get; set; }
    public bool OverwriteExisting { get; set; } = false;
    public int DefaultImageQuality { get; set; } = 90;
}

public sealed class QuickDockSettings
{
    public GeneralSettings General { get; set; } = new();
    public AppearanceSettings Appearance { get; set; } = new();
    public BehaviorSettings Behavior { get; set; } = new();
    public ClockSettings Clock { get; set; } = new();
    public PerformanceSettings Performance { get; set; } = new();
    public PrivacySettings Privacy { get; set; } = new();
    public ShortcutSettings Shortcuts { get; set; } = new();
    public NotificationSettings Notifications { get; set; } = new();
    public NetworkPreferencesSettings Network { get; set; } = new();
    public DeveloperPreferencesSettings Developer { get; set; } = new();
    public AccessibilitySettings Accessibility { get; set; } = new();
    public ClipboardModuleSettings Clipboard { get; set; } = new();
    public TimerModuleSettings Timer { get; set; } = new();
    public NotesModuleSettings Notes { get; set; } = new();
    public CalendarModuleSettings Calendar { get; set; } = new();
    public WeatherModuleSettings Weather { get; set; } = new();
    public ScreenTimeModuleSettings ScreenTime { get; set; } = new();
    public FileShelfModuleSettings FileShelf { get; set; } = new();
    public QuickDropModuleSettings QuickDrop { get; set; } = new();
    public FileConverterModuleSettings FileConverter { get; set; } = new();

    /// <summary>
    /// Key: ModuleId, Value: whether it is enabled.
    /// </summary>
    public Dictionary<string, bool> EnabledModules { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        // Core Utilities
        ["quickdock.clock"] = true,
        ["quickdock.system"] = true,
        ["quickdock.volume"] = true,
        ["quickdock.media"] = true,
        ["quickdock.battery"] = true,
        ["quickdock.network"] = true,

        // Productivity
        ["quickdock.clipboard"] = true,
        ["quickdock.timer"] = true,
        ["quickdock.notes"] = true,
        ["quickdock.calendar"] = true,
        ["quickdock.weather"] = true,
        ["quickdock.links"] = true,
        ["quickdock.temporarymessage"] = true,
        ["quickdock.screentime"] = true,

        // Files
        ["quickdock.fileshelf"] = true,
        ["quickdock.quickdrop"] = true,
        ["quickdock.converter"] = true,
        ["quickdock.transfers"] = true,

        // Developer
        ["quickdock.git"] = true,
        ["quickdock.docker"] = true,
        ["quickdock.terminal"] = true,
        ["quickdock.devtools"] = true,
        ["quickdock.http"] = true,
        ["quickdock.localdev"] = true,
        ["quickdock.processes"] = true,

        // Network & Security
        ["quickdock.networkdiag"] = true,
        ["quickdock.securitydiag"] = true,
        ["quickdock.tls"] = true,
        ["quickdock.httpsecurity"] = true,
        ["quickdock.firewall"] = true
    };

    /// <summary>
    /// Custom key-value module configurations.
    /// </summary>
    public Dictionary<string, string> CustomModuleConfig { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

