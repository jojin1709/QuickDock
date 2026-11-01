using QuickDock.Core.Models;

namespace QuickDock.Core.Interfaces;

public interface ISensitiveDataDetector
{
    bool IsSensitive(string text, out string? reason);
}

public interface IClipboardService : IDisposable
{
    event EventHandler<ClipboardItem>? ClipboardItemAdded;
    event EventHandler? ClipboardCleared;

    bool IsPaused { get; set; }
    Task<List<ClipboardItem>> GetHistoryAsync(string? search = null, ClipboardContentType? filterType = null);
    Task CopyItemToClipboardAsync(ClipboardItem item);
    Task SetPinnedAsync(string id, bool isPinned);
    Task DeleteItemAsync(string id);
    Task ClearHistoryAsync(bool clearPinned = false);
    Task StartListeningAsync();
    void StopListening();
    Task ProcessCurrentClipboardAsync();
}

public interface ITimerService : IDisposable
{
    event EventHandler<TimerItem>? TimerUpdated;
    event EventHandler<TimerItem>? TimerCompleted;
    IReadOnlyList<TimerItem> ActiveTimers { get; }

    TimerItem CreateTimer(string name, TimeSpan duration);
    void StartTimer(string id);
    void PauseTimer(string id);
    void ResumeTimer(string id);
    void ResetTimer(string id);
    void CancelTimer(string id);
}

public interface IStopwatchService : IDisposable
{
    event EventHandler<TimeSpan>? Tick;
    event EventHandler<LapItem>? LapRecorded;

    bool IsRunning { get; }
    TimeSpan ElapsedTime { get; }
    IReadOnlyList<LapItem> Laps { get; }

    void Start();
    void Pause();
    void Resume();
    void Reset();
    LapItem? RecordLap();
}

public interface IPomodoroService : IDisposable
{
    event EventHandler? PhaseChanged;
    event EventHandler<TimeSpan>? Tick;

    PomodoroPhase CurrentPhase { get; }
    int CurrentSession { get; }
    int TotalSessionsBeforeLongBreak { get; }
    TimeSpan RemainingTime { get; }
    bool IsRunning { get; }

    void Start();
    void Pause();
    void Resume();
    void Reset();
    void SkipToNextPhase();
}

public interface INotesService
{
    Task<List<NoteItem>> GetNotesAsync(string? search = null, bool includeArchived = false);
    Task<NoteItem?> GetNoteByIdAsync(string id);
    Task SaveNoteAsync(NoteItem note);
    Task DeleteNoteAsync(string id);
    Task SetPinnedAsync(string id, bool isPinned);
    Task SetArchivedAsync(string id, bool isArchived);
}

public interface ICalendarService
{
    Task<List<CalendarEventItem>> GetEventsForDateAsync(DateTime date);
    Task<List<CalendarEventItem>> GetEventsForMonthAsync(int year, int month);
    Task<CalendarEventItem?> GetNextUpcomingEventAsync();
    Task SaveEventAsync(CalendarEventItem eventItem);
    Task DeleteEventAsync(string id);
}

public interface IWeatherProvider
{
    string Name { get; }
    Task<WeatherData?> FetchWeatherAsync(string locationName, double latitude, double longitude, CancellationToken cancellationToken = default);
    Task<(double Latitude, double Longitude, string DisplayName)?> ResolveLocationAsync(string locationQuery, CancellationToken cancellationToken = default);
}

public interface IWeatherService
{
    event EventHandler<WeatherData?>? WeatherUpdated;
    WeatherData? CurrentWeather { get; }
    Task<WeatherData?> GetWeatherAsync(bool forceRefresh = false);
}

public interface ISavedLinksService
{
    Task<List<SavedLinkItem>> GetLinksAsync(string? search = null);
    Task SaveLinkAsync(SavedLinkItem link);
    Task DeleteLinkAsync(string id);
    Task SetPinnedAsync(string id, bool isPinned);
    bool OpenLink(string url);
}

public interface ITemporaryMessageService
{
    Task<TemporaryMessageItem?> GetActiveMessageAsync();
    Task SetMessageAsync(string content, TimeSpan duration);
    Task DismissMessageAsync(string id);
}

public interface IScreenTimeService : IDisposable
{
    bool IsTracking { get; }
    void StartTracking();
    void StopTracking();
    Task<List<AppUsageRecord>> GetDailyUsageAsync(DateTime date);
    Task<List<AppUsageRecord>> GetWeeklyUsageAsync(DateTime startDate, DateTime endDate);
    Task ClearHistoryAsync();
}

