using QuickDock.Core.Models;

namespace QuickDock.Core.Interfaces;

public interface IDatabaseService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    // Clipboard
    Task<List<ClipboardItem>> GetClipboardItemsAsync(int limit = 50, string? search = null, ClipboardContentType? filterType = null, CancellationToken cancellationToken = default);
    Task<ClipboardItem?> GetClipboardItemByIdAsync(string id, CancellationToken cancellationToken = default);
    Task SaveClipboardItemAsync(ClipboardItem item, CancellationToken cancellationToken = default);
    Task SetClipboardItemPinnedAsync(string id, bool isPinned, CancellationToken cancellationToken = default);
    Task DeleteClipboardItemAsync(string id, CancellationToken cancellationToken = default);
    Task ClearClipboardHistoryAsync(bool clearPinned = false, CancellationToken cancellationToken = default);
    Task PruneClipboardRetentionAsync(TimeSpan maxAge, int maxItems, CancellationToken cancellationToken = default);

    // Notes
    Task<List<NoteItem>> GetNotesAsync(string? search = null, bool includeArchived = false, CancellationToken cancellationToken = default);
    Task<NoteItem?> GetNoteByIdAsync(string id, CancellationToken cancellationToken = default);
    Task SaveNoteAsync(NoteItem note, CancellationToken cancellationToken = default);
    Task DeleteNoteAsync(string id, CancellationToken cancellationToken = default);
    Task SetNotePinnedAsync(string id, bool isPinned, CancellationToken cancellationToken = default);
    Task SetNoteArchivedAsync(string id, bool isArchived, CancellationToken cancellationToken = default);

    // Calendar
    Task<List<CalendarEventItem>> GetEventsForDateAsync(DateTime date, CancellationToken cancellationToken = default);
    Task<List<CalendarEventItem>> GetEventsForMonthAsync(int year, int month, CancellationToken cancellationToken = default);
    Task<CalendarEventItem?> GetNextUpcomingEventAsync(CancellationToken cancellationToken = default);
    Task SaveCalendarEventAsync(CalendarEventItem eventItem, CancellationToken cancellationToken = default);
    Task DeleteCalendarEventAsync(string id, CancellationToken cancellationToken = default);

    // Saved Links
    Task<List<SavedLinkItem>> GetSavedLinksAsync(string? search = null, CancellationToken cancellationToken = default);
    Task SaveSavedLinkAsync(SavedLinkItem link, CancellationToken cancellationToken = default);
    Task DeleteSavedLinkAsync(string id, CancellationToken cancellationToken = default);
    Task SetSavedLinkPinnedAsync(string id, bool isPinned, CancellationToken cancellationToken = default);

    // Temporary Messages
    Task<TemporaryMessageItem?> GetActiveTemporaryMessageAsync(CancellationToken cancellationToken = default);
    Task SaveTemporaryMessageAsync(TemporaryMessageItem message, CancellationToken cancellationToken = default);
    Task DismissTemporaryMessageAsync(string id, CancellationToken cancellationToken = default);
    Task PruneExpiredTemporaryMessagesAsync(CancellationToken cancellationToken = default);

    // App Usage / Screen Time
    Task RecordAppUsageAsync(string appName, string windowTitle, long durationSeconds, CancellationToken cancellationToken = default);
    Task<List<AppUsageRecord>> GetDailyUsageAsync(DateTime date, CancellationToken cancellationToken = default);
    Task<List<AppUsageRecord>> GetWeeklyUsageAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task ClearUsageHistoryAsync(CancellationToken cancellationToken = default);

    // Reset all data
    Task ClearAllDataAsync(CancellationToken cancellationToken = default);
}

