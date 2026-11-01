namespace QuickDock.Core.Models;

public enum ClipboardContentType
{
    Text,
    Url,
    Image,
    Files
}

public sealed class ClipboardItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public ClipboardContentType Type { get; set; } = ClipboardContentType.Text;
    public string Content { get; set; } = string.Empty;
    public string Preview { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsPinned { get; set; }
    public bool IsSensitive { get; set; }
    public string? ImageThumbnailPath { get; set; }
    public bool HasImageThumbnail => !string.IsNullOrEmpty(ImageThumbnailPath);
    public string ThumbnailVisibility => !string.IsNullOrEmpty(ImageThumbnailPath) ? "Visible" : "Collapsed";
    public string FormattedTime => CreatedAt.ToLocalTime().ToString("g");
}

public sealed class NoteItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsPinned { get; set; }
    public bool IsArchived { get; set; }
    public string FormattedTime => UpdatedAt.ToLocalTime().ToString("g");
}

public sealed class CalendarEventItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime EventDate { get; set; } = DateTime.Today;
    public TimeSpan StartTime { get; set; } = TimeSpan.FromHours(9);
    public TimeSpan EndTime { get; set; } = TimeSpan.FromHours(10);
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string FormattedTime => $"{StartTime:hh\\:mm} - {EndTime:hh\\:mm}";
}

public sealed class SavedLinkItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsPinned { get; set; }
}

public sealed class TemporaryMessageItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(2);
    public bool IsDismissed { get; set; }
}

public sealed class AppUsageRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Date { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");
    public string AppName { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
    public long DurationSeconds { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum TimerState
{
    Stopped,
    Running,
    Paused,
    Completed
}

public sealed class TimerItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Timer";
    public TimeSpan TotalDuration { get; set; }
    public TimeSpan RemainingTime { get; set; }
    public TimerState State { get; set; } = TimerState.Stopped;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string FormattedTime => $"{RemainingTime:mm\\:ss}";
}

public sealed class LapItem
{
    public int LapNumber { get; set; }
    public TimeSpan LapTime { get; set; }
    public TimeSpan SplitTime { get; set; }
    public string FormattedLapTime => $"{LapTime:mm\\:ss\\.ff}";
    public string FormattedSplitTime => $"{SplitTime:hh\\:mm\\:ss\\.ff}";
}

public enum PomodoroPhase
{
    Work,
    ShortBreak,
    LongBreak
}

public sealed class WeatherData
{
    public string LocationName { get; set; } = string.Empty;
    public double TemperatureC { get; set; }
    public double ApparentTemperatureC { get; set; }
    public int RelativeHumidity { get; set; }
    public double WindSpeedKmh { get; set; }
    public string Condition { get; set; } = "Clear";
    public int WeatherCode { get; set; }
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
    public bool IsCached { get; set; }
}

