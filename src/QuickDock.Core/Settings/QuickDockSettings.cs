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

public sealed class AppearanceSettings
{
    public AppTheme Theme { get; set; } = AppTheme.System;
    public string AccentColor { get; set; } = "#0078D4";
    public AnimationLevel Animation { get; set; } = AnimationLevel.Normal;
    public double Opacity { get; set; } = 0.98;
}

public sealed class BehaviorSettings
{
    public string GlobalHotkey { get; set; } = "Ctrl+Space";
    public bool AutoHide { get; set; } = true;
    public int HoverDelayMs { get; set; } = 150;
    public bool DetectFullscreen { get; set; } = true;
    public bool HideInFullscreen { get; set; } = true;
    public bool AlwaysOnTop { get; set; } = true;
    public bool FollowActiveMonitor { get; set; } = true;
    public string MonitorTarget { get; set; } = "Active";
    public bool StartWithWindows { get; set; } = false;
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
    public AppearanceSettings Appearance { get; set; } = new();
    public BehaviorSettings Behavior { get; set; } = new();
    public ClockSettings Clock { get; set; } = new();
    public PerformanceSettings Performance { get; set; } = new();
    public PrivacySettings Privacy { get; set; } = new();
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
        ["quickdock.clock"] = true,
        ["quickdock.system"] = true,
        ["quickdock.timer"] = true,
        ["quickdock.volume"] = true,
        ["quickdock.media"] = true,
        ["quickdock.clipboard"] = true,
        ["quickdock.notes"] = true,
        ["quickdock.calendar"] = true,
        ["quickdock.weather"] = true,
        ["quickdock.links"] = true,
        ["quickdock.temporarymessage"] = true,
        ["quickdock.screentime"] = true,
        ["quickdock.fileshelf"] = true,
        ["quickdock.quickdrop"] = true,
        ["quickdock.converter"] = true,
        ["quickdock.transfers"] = true,
        ["quickdock.dev"] = true,
        ["quickdock.ai"] = true
    };

    /// <summary>
    /// Custom key-value module configurations.
    /// </summary>
    public Dictionary<string, string> CustomModuleConfig { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

