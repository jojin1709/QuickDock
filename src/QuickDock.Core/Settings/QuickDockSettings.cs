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
    public string MonitorTarget { get; set; } = "Primary";
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

public sealed class QuickDockSettings
{
    public AppearanceSettings Appearance { get; set; } = new();
    public BehaviorSettings Behavior { get; set; } = new();
    public PerformanceSettings Performance { get; set; } = new();
    public PrivacySettings Privacy { get; set; } = new();

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
        ["quickdock.dev"] = true,
        ["quickdock.quickdrop"] = true,
        ["quickdock.ai"] = true
    };

    /// <summary>
    /// Custom key-value module configurations.
    /// </summary>
    public Dictionary<string, string> CustomModuleConfig { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
