using QuickDock.Core.Modules;
using QuickDock.Core.Settings;

namespace QuickDock.Core.Events;

public sealed record ClipboardChangedEvent(
    string ContentType,
    long DataSizeBytes,
    DateTimeOffset Timestamp) : IEvent
{
    public ClipboardChangedEvent(string contentType, long dataSizeBytes)
        : this(contentType, dataSizeBytes, DateTimeOffset.UtcNow) { }
}

public sealed record VolumeChangedEvent(
    float VolumePercent,
    bool IsMuted,
    DateTimeOffset Timestamp) : IEvent
{
    public VolumeChangedEvent(float volumePercent, bool isMuted)
        : this(volumePercent, isMuted, DateTimeOffset.UtcNow) { }
}

public sealed record MediaChangedEvent(
    string? Title,
    string? Artist,
    string? Album,
    bool IsPlaying,
    DateTimeOffset Timestamp) : IEvent
{
    public MediaChangedEvent(string? title, string? artist, string? album, bool isPlaying)
        : this(title, artist, album, isPlaying, DateTimeOffset.UtcNow) { }
}

public sealed record PowerChangedEvent(
    int BatteryPercent,
    bool IsCharging,
    bool IsPowerSaverActive,
    DateTimeOffset Timestamp) : IEvent
{
    public PowerChangedEvent(int batteryPercent, bool isCharging, bool isPowerSaverActive)
        : this(batteryPercent, isCharging, isPowerSaverActive, DateTimeOffset.UtcNow) { }
}

public sealed record DisplayChangedEvent(
    string PrimaryMonitorId,
    int DisplayCount,
    DateTimeOffset Timestamp) : IEvent
{
    public DisplayChangedEvent(string primaryMonitorId, int displayCount)
        : this(primaryMonitorId, displayCount, DateTimeOffset.UtcNow) { }
}

public sealed record NetworkChangedEvent(
    bool IsConnected,
    string? AdapterName,
    string? LocalIpAddress,
    DateTimeOffset Timestamp) : IEvent
{
    public NetworkChangedEvent(bool isConnected, string? adapterName, string? localIpAddress)
        : this(isConnected, adapterName, localIpAddress, DateTimeOffset.UtcNow) { }
}

public sealed record FullscreenChangedEvent(
    bool IsFullscreenAppActive,
    string? WindowTitle,
    DateTimeOffset Timestamp) : IEvent
{
    public FullscreenChangedEvent(bool isFullscreenAppActive, string? windowTitle)
        : this(isFullscreenAppActive, windowTitle, DateTimeOffset.UtcNow) { }
}

public sealed record TimerUpdatedEvent(
    string TimerId,
    TimeSpan RemainingTime,
    bool IsCompleted,
    DateTimeOffset Timestamp) : IEvent
{
    public TimerUpdatedEvent(string timerId, TimeSpan remainingTime, bool isCompleted)
        : this(timerId, remainingTime, isCompleted, DateTimeOffset.UtcNow) { }
}

public sealed record FileReceivedEvent(
    string FileName,
    string FilePath,
    long SizeBytes,
    string SenderInfo,
    DateTimeOffset Timestamp) : IEvent
{
    public FileReceivedEvent(string fileName, string filePath, long sizeBytes, string senderInfo)
        : this(fileName, filePath, sizeBytes, senderInfo, DateTimeOffset.UtcNow) { }
}

public sealed record NotificationReceivedEvent(
    string Title,
    string Message,
    string? SourceModuleId,
    DateTimeOffset Timestamp) : IEvent
{
    public NotificationReceivedEvent(string title, string message, string? sourceModuleId = null)
        : this(title, message, sourceModuleId, DateTimeOffset.UtcNow) { }
}

public sealed record SettingsChangedEvent(
    QuickDockSettings Settings,
    string? ChangedSection,
    DateTimeOffset Timestamp) : IEvent
{
    public SettingsChangedEvent(QuickDockSettings settings, string? changedSection = null)
        : this(settings, changedSection, DateTimeOffset.UtcNow) { }
}

public sealed record ModuleStateChangedEvent(
    string ModuleId,
    ModuleState OldState,
    ModuleState NewState,
    string? ErrorMessage,
    DateTimeOffset Timestamp) : IEvent
{
    public ModuleStateChangedEvent(string moduleId, ModuleState oldState, ModuleState newState, string? errorMessage = null)
        : this(moduleId, oldState, newState, errorMessage, DateTimeOffset.UtcNow) { }
}

public sealed record AppVisibilityChangedEvent(
    bool IsExpanded,
    string Trigger,
    DateTimeOffset Timestamp) : IEvent
{
    public AppVisibilityChangedEvent(bool isExpanded, string trigger)
        : this(isExpanded, trigger, DateTimeOffset.UtcNow) { }
}
