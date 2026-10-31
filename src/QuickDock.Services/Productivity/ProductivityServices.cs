using System.Diagnostics;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.Services.Productivity;

public sealed class NotesService : INotesService
{
    private readonly IDatabaseService _databaseService;
    private readonly ILogger<NotesService> _logger;

    public NotesService(IDatabaseService databaseService, ILogger<NotesService> logger)
    {
        _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<List<NoteItem>> GetNotesAsync(string? search = null, bool includeArchived = false)
    {
        return _databaseService.GetNotesAsync(search, includeArchived);
    }

    public Task<NoteItem?> GetNoteByIdAsync(string id)
    {
        return _databaseService.GetNoteByIdAsync(id);
    }

    public Task SaveNoteAsync(NoteItem note)
    {
        note.UpdatedAt = DateTime.UtcNow;
        return _databaseService.SaveNoteAsync(note);
    }

    public Task DeleteNoteAsync(string id)
    {
        return _databaseService.DeleteNoteAsync(id);
    }

    public Task SetPinnedAsync(string id, bool isPinned)
    {
        return _databaseService.SetNotePinnedAsync(id, isPinned);
    }

    public Task SetArchivedAsync(string id, bool isArchived)
    {
        return _databaseService.SetNoteArchivedAsync(id, isArchived);
    }
}

public sealed class CalendarService : ICalendarService
{
    private readonly IDatabaseService _databaseService;
    private readonly ILogger<CalendarService> _logger;

    public CalendarService(IDatabaseService databaseService, ILogger<CalendarService> logger)
    {
        _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<List<CalendarEventItem>> GetEventsForDateAsync(DateTime date)
    {
        return _databaseService.GetEventsForDateAsync(date);
    }

    public Task<List<CalendarEventItem>> GetEventsForMonthAsync(int year, int month)
    {
        return _databaseService.GetEventsForMonthAsync(year, month);
    }

    public Task<CalendarEventItem?> GetNextUpcomingEventAsync()
    {
        return _databaseService.GetNextUpcomingEventAsync();
    }

    public Task SaveEventAsync(CalendarEventItem eventItem)
    {
        return _databaseService.SaveCalendarEventAsync(eventItem);
    }

    public Task DeleteEventAsync(string id)
    {
        return _databaseService.DeleteCalendarEventAsync(id);
    }
}

public sealed class SavedLinksService : ISavedLinksService
{
    private readonly IDatabaseService _databaseService;
    private readonly ILogger<SavedLinksService> _logger;

    public SavedLinksService(IDatabaseService databaseService, ILogger<SavedLinksService> logger)
    {
        _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<List<SavedLinkItem>> GetLinksAsync(string? search = null)
    {
        return _databaseService.GetSavedLinksAsync(search);
    }

    public Task SaveLinkAsync(SavedLinkItem link)
    {
        link.UpdatedAt = DateTime.UtcNow;
        return _databaseService.SaveSavedLinkAsync(link);
    }

    public Task DeleteLinkAsync(string id)
    {
        return _databaseService.DeleteSavedLinkAsync(id);
    }

    public Task SetPinnedAsync(string id, bool isPinned)
    {
        return _databaseService.SetSavedLinkPinnedAsync(id, isPinned);
    }

    public bool OpenLink(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        // Security check: only allow safe http and https protocols
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            _logger.LogWarning("Blocked opening invalid or unsafe link URI: {Url}", url);
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch default browser for link.");
            return false;
        }
    }
}

public sealed class TemporaryMessageService : ITemporaryMessageService
{
    private readonly IDatabaseService _databaseService;
    private readonly ILogger<TemporaryMessageService> _logger;

    public TemporaryMessageService(IDatabaseService databaseService, ILogger<TemporaryMessageService> logger)
    {
        _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TemporaryMessageItem?> GetActiveMessageAsync()
    {
        await _databaseService.PruneExpiredTemporaryMessagesAsync();
        return await _databaseService.GetActiveTemporaryMessageAsync();
    }

    public async Task SetMessageAsync(string content, TimeSpan duration)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        var msg = new TemporaryMessageItem
        {
            Content = content.Trim(),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(duration),
            IsDismissed = false
        };
        await _databaseService.SaveTemporaryMessageAsync(msg);
    }

    public Task DismissMessageAsync(string id)
    {
        return _databaseService.DismissTemporaryMessageAsync(id);
    }
}

