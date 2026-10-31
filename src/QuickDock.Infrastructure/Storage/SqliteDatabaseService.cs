using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.Infrastructure.Storage;

public sealed class SqliteDatabaseService : IDatabaseService, IDisposable
{
    private readonly string _connectionString;
    private readonly ILogger<SqliteDatabaseService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _initialized;

    public SqliteDatabaseService(ILogger<SqliteDatabaseService> logger, string? dbPath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        AppStoragePaths.EnsureDirectoriesExist();
        var databasePath = dbPath ?? AppStoragePaths.DatabaseFile;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode = WAL; PRAGMA foreign_keys = ON;";
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized) return;

            using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS clipboard_items (
                    id TEXT PRIMARY KEY,
                    type INTEGER NOT NULL,
                    content TEXT NOT NULL,
                    preview TEXT NOT NULL,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    is_pinned INTEGER NOT NULL DEFAULT 0,
                    is_sensitive INTEGER NOT NULL DEFAULT 0,
                    image_path TEXT
                );
                CREATE INDEX IF NOT EXISTS idx_clipboard_created ON clipboard_items(created_at);
                CREATE INDEX IF NOT EXISTS idx_clipboard_pinned ON clipboard_items(is_pinned);

                CREATE TABLE IF NOT EXISTS notes (
                    id TEXT PRIMARY KEY,
                    title TEXT NOT NULL,
                    content TEXT NOT NULL,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    is_pinned INTEGER NOT NULL DEFAULT 0,
                    is_archived INTEGER NOT NULL DEFAULT 0
                );
                CREATE INDEX IF NOT EXISTS idx_notes_updated ON notes(updated_at);
                CREATE INDEX IF NOT EXISTS idx_notes_pinned ON notes(is_pinned);

                CREATE TABLE IF NOT EXISTS calendar_events (
                    id TEXT PRIMARY KEY,
                    title TEXT NOT NULL,
                    description TEXT NOT NULL,
                    event_date TEXT NOT NULL,
                    start_time TEXT NOT NULL,
                    end_time TEXT NOT NULL,
                    created_at TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_calendar_date ON calendar_events(event_date);

                CREATE TABLE IF NOT EXISTS saved_links (
                    id TEXT PRIMARY KEY,
                    title TEXT NOT NULL,
                    url TEXT NOT NULL,
                    description TEXT NOT NULL,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    is_pinned INTEGER NOT NULL DEFAULT 0
                );
                CREATE INDEX IF NOT EXISTS idx_links_updated ON saved_links(updated_at);

                CREATE TABLE IF NOT EXISTS temporary_messages (
                    id TEXT PRIMARY KEY,
                    content TEXT NOT NULL,
                    created_at TEXT NOT NULL,
                    expires_at TEXT NOT NULL,
                    is_dismissed INTEGER NOT NULL DEFAULT 0
                );
                CREATE INDEX IF NOT EXISTS idx_temp_msg_expires ON temporary_messages(expires_at);

                CREATE TABLE IF NOT EXISTS app_usage (
                    id TEXT PRIMARY KEY,
                    usage_date TEXT NOT NULL,
                    app_name TEXT NOT NULL,
                    window_title TEXT NOT NULL,
                    duration_seconds INTEGER NOT NULL DEFAULT 0,
                    updated_at TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_usage_date_app ON app_usage(usage_date, app_name);

                CREATE TABLE IF NOT EXISTS file_shelf (
                    id TEXT PRIMARY KEY,
                    path TEXT NOT NULL,
                    name TEXT NOT NULL,
                    size INTEGER NOT NULL,
                    type TEXT NOT NULL,
                    added_at TEXT NOT NULL,
                    last_accessed TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_shelf_added ON file_shelf(added_at);

                CREATE TABLE IF NOT EXISTS transfer_history (
                    id TEXT PRIMARY KEY,
                    direction TEXT NOT NULL,
                    filename TEXT NOT NULL,
                    size INTEGER NOT NULL,
                    timestamp TEXT NOT NULL,
                    status TEXT NOT NULL,
                    hash TEXT,
                    error_message TEXT
                );
                CREATE INDEX IF NOT EXISTS idx_transfer_timestamp ON transfer_history(timestamp);
            ";
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            _initialized = true;
            _logger.LogInformation("SqliteDatabaseService initialized successfully.");
        }
        finally
        {
            _lock.Release();
        }
    }

    #region Clipboard

    public async Task<List<ClipboardItem>> GetClipboardItemsAsync(int limit = 50, string? search = null, ClipboardContentType? filterType = null, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var list = new List<ClipboardItem>();

        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();

        var query = "SELECT id, type, content, preview, created_at, updated_at, is_pinned, is_sensitive, image_path FROM clipboard_items WHERE 1=1";
        if (!string.IsNullOrWhiteSpace(search))
        {
            query += " AND (content LIKE @search OR preview LIKE @search)";
            cmd.Parameters.AddWithValue("@search", $"%{search}%");
        }
        if (filterType.HasValue)
        {
            query += " AND type = @filterType";
            cmd.Parameters.AddWithValue("@filterType", (int)filterType.Value);
        }
        query += " ORDER BY is_pinned DESC, created_at DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("@limit", limit);
        cmd.CommandText = query;

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new ClipboardItem
            {
                Id = reader.GetString(0),
                Type = (ClipboardContentType)reader.GetInt32(1),
                Content = reader.GetString(2),
                Preview = reader.GetString(3),
                CreatedAt = DateTime.Parse(reader.GetString(4)),
                UpdatedAt = DateTime.Parse(reader.GetString(5)),
                IsPinned = reader.GetInt32(6) == 1,
                IsSensitive = reader.GetInt32(7) == 1,
                ImageThumbnailPath = reader.IsDBNull(8) ? null : reader.GetString(8)
            });
        }
        return list;
    }

    public async Task<ClipboardItem?> GetClipboardItemByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, type, content, preview, created_at, updated_at, is_pinned, is_sensitive, image_path FROM clipboard_items WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return new ClipboardItem
            {
                Id = reader.GetString(0),
                Type = (ClipboardContentType)reader.GetInt32(1),
                Content = reader.GetString(2),
                Preview = reader.GetString(3),
                CreatedAt = DateTime.Parse(reader.GetString(4)),
                UpdatedAt = DateTime.Parse(reader.GetString(5)),
                IsPinned = reader.GetInt32(6) == 1,
                IsSensitive = reader.GetInt32(7) == 1,
                ImageThumbnailPath = reader.IsDBNull(8) ? null : reader.GetString(8)
            };
        }
        return null;
    }

    public async Task SaveClipboardItemAsync(ClipboardItem item, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO clipboard_items (id, type, content, preview, created_at, updated_at, is_pinned, is_sensitive, image_path)
            VALUES (@id, @type, @content, @preview, @created_at, @updated_at, @is_pinned, @is_sensitive, @image_path)
            ON CONFLICT(id) DO UPDATE SET
                content = excluded.content,
                preview = excluded.preview,
                updated_at = excluded.updated_at,
                is_pinned = excluded.is_pinned,
                is_sensitive = excluded.is_sensitive,
                image_path = excluded.image_path;
        ";
        cmd.Parameters.AddWithValue("@id", item.Id);
        cmd.Parameters.AddWithValue("@type", (int)item.Type);
        cmd.Parameters.AddWithValue("@content", item.Content);
        cmd.Parameters.AddWithValue("@preview", item.Preview);
        cmd.Parameters.AddWithValue("@created_at", item.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@updated_at", item.UpdatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@is_pinned", item.IsPinned ? 1 : 0);
        cmd.Parameters.AddWithValue("@is_sensitive", item.IsSensitive ? 1 : 0);
        cmd.Parameters.AddWithValue("@image_path", (object?)item.ImageThumbnailPath ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SetClipboardItemPinnedAsync(string id, bool isPinned, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE clipboard_items SET is_pinned = @is_pinned WHERE id = @id";
        cmd.Parameters.AddWithValue("@is_pinned", isPinned ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteClipboardItemAsync(string id, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM clipboard_items WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearClipboardHistoryAsync(bool clearPinned = false, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = clearPinned ? "DELETE FROM clipboard_items" : "DELETE FROM clipboard_items WHERE is_pinned = 0";
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task PruneClipboardRetentionAsync(TimeSpan maxAge, int maxItems, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        
        // Prune older than maxAge for unpinned items
        var cutoff = DateTime.UtcNow.Subtract(maxAge).ToString("o");
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM clipboard_items WHERE is_pinned = 0 AND created_at < @cutoff";
            cmd.Parameters.AddWithValue("@cutoff", cutoff);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        // Prune excess unpinned items exceeding maxItems
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                DELETE FROM clipboard_items 
                WHERE is_pinned = 0 AND id NOT IN (
                    SELECT id FROM clipboard_items 
                    WHERE is_pinned = 0 
                    ORDER BY created_at DESC 
                    LIMIT @maxItems
                );
            ";
            cmd.Parameters.AddWithValue("@maxItems", maxItems);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    #endregion

    #region Notes

    public async Task<List<NoteItem>> GetNotesAsync(string? search = null, bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var list = new List<NoteItem>();

        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();

        var query = "SELECT id, title, content, created_at, updated_at, is_pinned, is_archived FROM notes WHERE 1=1";
        if (!includeArchived)
        {
            query += " AND is_archived = 0";
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            query += " AND (title LIKE @search OR content LIKE @search)";
            cmd.Parameters.AddWithValue("@search", $"%{search}%");
        }
        query += " ORDER BY is_pinned DESC, updated_at DESC";
        cmd.CommandText = query;

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new NoteItem
            {
                Id = reader.GetString(0),
                Title = reader.GetString(1),
                Content = reader.GetString(2),
                CreatedAt = DateTime.Parse(reader.GetString(3)),
                UpdatedAt = DateTime.Parse(reader.GetString(4)),
                IsPinned = reader.GetInt32(5) == 1,
                IsArchived = reader.GetInt32(6) == 1
            });
        }
        return list;
    }

    public async Task<NoteItem?> GetNoteByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, title, content, created_at, updated_at, is_pinned, is_archived FROM notes WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return new NoteItem
            {
                Id = reader.GetString(0),
                Title = reader.GetString(1),
                Content = reader.GetString(2),
                CreatedAt = DateTime.Parse(reader.GetString(3)),
                UpdatedAt = DateTime.Parse(reader.GetString(4)),
                IsPinned = reader.GetInt32(5) == 1,
                IsArchived = reader.GetInt32(6) == 1
            };
        }
        return null;
    }

    public async Task SaveNoteAsync(NoteItem note, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO notes (id, title, content, created_at, updated_at, is_pinned, is_archived)
            VALUES (@id, @title, @content, @created_at, @updated_at, @is_pinned, @is_archived)
            ON CONFLICT(id) DO UPDATE SET
                title = excluded.title,
                content = excluded.content,
                updated_at = excluded.updated_at,
                is_pinned = excluded.is_pinned,
                is_archived = excluded.is_archived;
        ";
        cmd.Parameters.AddWithValue("@id", note.Id);
        cmd.Parameters.AddWithValue("@title", note.Title);
        cmd.Parameters.AddWithValue("@content", note.Content);
        cmd.Parameters.AddWithValue("@created_at", note.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@updated_at", note.UpdatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@is_pinned", note.IsPinned ? 1 : 0);
        cmd.Parameters.AddWithValue("@is_archived", note.IsArchived ? 1 : 0);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteNoteAsync(string id, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM notes WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SetNotePinnedAsync(string id, bool isPinned, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE notes SET is_pinned = @is_pinned WHERE id = @id";
        cmd.Parameters.AddWithValue("@is_pinned", isPinned ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SetNoteArchivedAsync(string id, bool isArchived, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE notes SET is_archived = @is_archived WHERE id = @id";
        cmd.Parameters.AddWithValue("@is_archived", isArchived ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Calendar

    public async Task<List<CalendarEventItem>> GetEventsForDateAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var list = new List<CalendarEventItem>();
        var dateStr = date.ToString("yyyy-MM-dd");

        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, title, description, event_date, start_time, end_time, created_at FROM calendar_events WHERE event_date = @date ORDER BY start_time ASC";
        cmd.Parameters.AddWithValue("@date", dateStr);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new CalendarEventItem
            {
                Id = reader.GetString(0),
                Title = reader.GetString(1),
                Description = reader.GetString(2),
                EventDate = DateTime.Parse(reader.GetString(3)),
                StartTime = TimeSpan.Parse(reader.GetString(4)),
                EndTime = TimeSpan.Parse(reader.GetString(5)),
                CreatedAt = DateTime.Parse(reader.GetString(6))
            });
        }
        return list;
    }

    public async Task<List<CalendarEventItem>> GetEventsForMonthAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var list = new List<CalendarEventItem>();
        var prefix = $"{year:D4}-{month:D2}%";

        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, title, description, event_date, start_time, end_time, created_at FROM calendar_events WHERE event_date LIKE @prefix ORDER BY event_date ASC, start_time ASC";
        cmd.Parameters.AddWithValue("@prefix", prefix);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new CalendarEventItem
            {
                Id = reader.GetString(0),
                Title = reader.GetString(1),
                Description = reader.GetString(2),
                EventDate = DateTime.Parse(reader.GetString(3)),
                StartTime = TimeSpan.Parse(reader.GetString(4)),
                EndTime = TimeSpan.Parse(reader.GetString(5)),
                CreatedAt = DateTime.Parse(reader.GetString(6))
            });
        }
        return list;
    }

    public async Task<CalendarEventItem?> GetNextUpcomingEventAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var todayStr = DateTime.Today.ToString("yyyy-MM-dd");

        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, title, description, event_date, start_time, end_time, created_at FROM calendar_events WHERE event_date >= @today ORDER BY event_date ASC, start_time ASC LIMIT 1";
        cmd.Parameters.AddWithValue("@today", todayStr);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return new CalendarEventItem
            {
                Id = reader.GetString(0),
                Title = reader.GetString(1),
                Description = reader.GetString(2),
                EventDate = DateTime.Parse(reader.GetString(3)),
                StartTime = TimeSpan.Parse(reader.GetString(4)),
                EndTime = TimeSpan.Parse(reader.GetString(5)),
                CreatedAt = DateTime.Parse(reader.GetString(6))
            };
        }
        return null;
    }

    public async Task SaveCalendarEventAsync(CalendarEventItem eventItem, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO calendar_events (id, title, description, event_date, start_time, end_time, created_at)
            VALUES (@id, @title, @description, @event_date, @start_time, @end_time, @created_at)
            ON CONFLICT(id) DO UPDATE SET
                title = excluded.title,
                description = excluded.description,
                event_date = excluded.event_date,
                start_time = excluded.start_time,
                end_time = excluded.end_time;
        ";
        cmd.Parameters.AddWithValue("@id", eventItem.Id);
        cmd.Parameters.AddWithValue("@title", eventItem.Title);
        cmd.Parameters.AddWithValue("@description", eventItem.Description);
        cmd.Parameters.AddWithValue("@event_date", eventItem.EventDate.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@start_time", eventItem.StartTime.ToString(@"hh\:mm\:ss"));
        cmd.Parameters.AddWithValue("@end_time", eventItem.EndTime.ToString(@"hh\:mm\:ss"));
        cmd.Parameters.AddWithValue("@created_at", eventItem.CreatedAt.ToString("o"));

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteCalendarEventAsync(string id, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM calendar_events WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Saved Links

    public async Task<List<SavedLinkItem>> GetSavedLinksAsync(string? search = null, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var list = new List<SavedLinkItem>();

        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();

        var query = "SELECT id, title, url, description, created_at, updated_at, is_pinned FROM saved_links WHERE 1=1";
        if (!string.IsNullOrWhiteSpace(search))
        {
            query += " AND (title LIKE @search OR url LIKE @search OR description LIKE @search)";
            cmd.Parameters.AddWithValue("@search", $"%{search}%");
        }
        query += " ORDER BY is_pinned DESC, updated_at DESC";
        cmd.CommandText = query;

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new SavedLinkItem
            {
                Id = reader.GetString(0),
                Title = reader.GetString(1),
                Url = reader.GetString(2),
                Description = reader.GetString(3),
                CreatedAt = DateTime.Parse(reader.GetString(4)),
                UpdatedAt = DateTime.Parse(reader.GetString(5)),
                IsPinned = reader.GetInt32(6) == 1
            });
        }
        return list;
    }

    public async Task SaveSavedLinkAsync(SavedLinkItem link, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO saved_links (id, title, url, description, created_at, updated_at, is_pinned)
            VALUES (@id, @title, @url, @description, @created_at, @updated_at, @is_pinned)
            ON CONFLICT(id) DO UPDATE SET
                title = excluded.title,
                url = excluded.url,
                description = excluded.description,
                updated_at = excluded.updated_at,
                is_pinned = excluded.is_pinned;
        ";
        cmd.Parameters.AddWithValue("@id", link.Id);
        cmd.Parameters.AddWithValue("@title", link.Title);
        cmd.Parameters.AddWithValue("@url", link.Url);
        cmd.Parameters.AddWithValue("@description", link.Description);
        cmd.Parameters.AddWithValue("@created_at", link.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@updated_at", link.UpdatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@is_pinned", link.IsPinned ? 1 : 0);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteSavedLinkAsync(string id, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM saved_links WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SetSavedLinkPinnedAsync(string id, bool isPinned, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE saved_links SET is_pinned = @is_pinned WHERE id = @id";
        cmd.Parameters.AddWithValue("@is_pinned", isPinned ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Temporary Messages

    public async Task<TemporaryMessageItem?> GetActiveTemporaryMessageAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var nowStr = DateTime.UtcNow.ToString("o");

        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, content, created_at, expires_at, is_dismissed FROM temporary_messages WHERE is_dismissed = 0 AND expires_at > @now ORDER BY created_at DESC LIMIT 1";
        cmd.Parameters.AddWithValue("@now", nowStr);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return new TemporaryMessageItem
            {
                Id = reader.GetString(0),
                Content = reader.GetString(1),
                CreatedAt = DateTime.Parse(reader.GetString(2)),
                ExpiresAt = DateTime.Parse(reader.GetString(3)),
                IsDismissed = reader.GetInt32(4) == 1
            };
        }
        return null;
    }

    public async Task SaveTemporaryMessageAsync(TemporaryMessageItem message, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO temporary_messages (id, content, created_at, expires_at, is_dismissed)
            VALUES (@id, @content, @created_at, @expires_at, @is_dismissed)
            ON CONFLICT(id) DO UPDATE SET
                content = excluded.content,
                expires_at = excluded.expires_at,
                is_dismissed = excluded.is_dismissed;
        ";
        cmd.Parameters.AddWithValue("@id", message.Id);
        cmd.Parameters.AddWithValue("@content", message.Content);
        cmd.Parameters.AddWithValue("@created_at", message.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@expires_at", message.ExpiresAt.ToString("o"));
        cmd.Parameters.AddWithValue("@is_dismissed", message.IsDismissed ? 1 : 0);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DismissTemporaryMessageAsync(string id, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE temporary_messages SET is_dismissed = 1 WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task PruneExpiredTemporaryMessagesAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var nowStr = DateTime.UtcNow.ToString("o");

        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM temporary_messages WHERE expires_at < @now OR is_dismissed = 1";
        cmd.Parameters.AddWithValue("@now", nowStr);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region App Usage / Screen Time

    public async Task RecordAppUsageAsync(string appName, string windowTitle, long durationSeconds, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appName) || durationSeconds <= 0) return;

        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var todayStr = DateTime.Today.ToString("yyyy-MM-dd");
        var nowStr = DateTime.UtcNow.ToString("o");

        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO app_usage (id, usage_date, app_name, window_title, duration_seconds, updated_at)
            VALUES (@id, @usage_date, @app_name, @window_title, @duration_seconds, @updated_at)
            ON CONFLICT(id) DO UPDATE SET
                duration_seconds = duration_seconds + excluded.duration_seconds,
                window_title = excluded.window_title,
                updated_at = excluded.updated_at;
        ";
        // Deterministic ID for (date + app_name) so that it aggregates per app per day
        var recordId = $"{todayStr}_{appName.ToLowerInvariant()}";
        cmd.Parameters.AddWithValue("@id", recordId);
        cmd.Parameters.AddWithValue("@usage_date", todayStr);
        cmd.Parameters.AddWithValue("@app_name", appName);
        cmd.Parameters.AddWithValue("@window_title", windowTitle ?? string.Empty);
        cmd.Parameters.AddWithValue("@duration_seconds", durationSeconds);
        cmd.Parameters.AddWithValue("@updated_at", nowStr);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AppUsageRecord>> GetDailyUsageAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var list = new List<AppUsageRecord>();
        var dateStr = date.ToString("yyyy-MM-dd");

        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, usage_date, app_name, window_title, duration_seconds, updated_at FROM app_usage WHERE usage_date = @date ORDER BY duration_seconds DESC";
        cmd.Parameters.AddWithValue("@date", dateStr);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new AppUsageRecord
            {
                Id = reader.GetString(0),
                Date = reader.GetString(1),
                AppName = reader.GetString(2),
                WindowTitle = reader.GetString(3),
                DurationSeconds = reader.GetInt64(4),
                UpdatedAt = DateTime.Parse(reader.GetString(5))
            });
        }
        return list;
    }

    public async Task<List<AppUsageRecord>> GetWeeklyUsageAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var list = new List<AppUsageRecord>();
        var startStr = startDate.ToString("yyyy-MM-dd");
        var endStr = endDate.ToString("yyyy-MM-dd");

        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT app_name, MAX(window_title), SUM(duration_seconds) as total_duration
            FROM app_usage
            WHERE usage_date >= @startStr AND usage_date <= @endStr
            GROUP BY app_name
            ORDER BY total_duration DESC;
        ";
        cmd.Parameters.AddWithValue("@startStr", startStr);
        cmd.Parameters.AddWithValue("@endStr", endStr);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new AppUsageRecord
            {
                Id = Guid.NewGuid().ToString(),
                Date = $"{startStr}..{endStr}",
                AppName = reader.GetString(0),
                WindowTitle = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                DurationSeconds = reader.GetInt64(2),
                UpdatedAt = DateTime.UtcNow
            });
        }
        return list;
    }

    public async Task ClearUsageHistoryAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM app_usage";
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region File Shelf

    public async Task<List<FileShelfItem>> GetFileShelfItemsAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var list = new List<FileShelfItem>();

        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, path, name, size, type, added_at, last_accessed FROM file_shelf ORDER BY added_at DESC";

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var item = new FileShelfItem
            {
                Id = reader.GetString(0),
                Path = reader.GetString(1),
                Name = reader.GetString(2),
                Size = reader.GetInt64(3),
                Type = reader.GetString(4),
                AddedAt = DateTimeOffset.Parse(reader.GetString(5)),
                LastAccessed = DateTimeOffset.Parse(reader.GetString(6)),
                IsMissing = !File.Exists(reader.GetString(1)) && !Directory.Exists(reader.GetString(1))
            };
            list.Add(item);
        }

        return list;
    }

    public async Task<FileShelfItem?> GetFileShelfItemByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, path, name, size, type, added_at, last_accessed FROM file_shelf WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return new FileShelfItem
            {
                Id = reader.GetString(0),
                Path = reader.GetString(1),
                Name = reader.GetString(2),
                Size = reader.GetInt64(3),
                Type = reader.GetString(4),
                AddedAt = DateTimeOffset.Parse(reader.GetString(5)),
                LastAccessed = DateTimeOffset.Parse(reader.GetString(6)),
                IsMissing = !File.Exists(reader.GetString(1)) && !Directory.Exists(reader.GetString(1))
            };
        }
        return null;
    }

    public async Task SaveFileShelfItemAsync(FileShelfItem item, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO file_shelf (id, path, name, size, type, added_at, last_accessed)
            VALUES (@id, @path, @name, @size, @type, @added_at, @last_accessed)
            ON CONFLICT(id) DO UPDATE SET
                path = excluded.path,
                name = excluded.name,
                size = excluded.size,
                type = excluded.type,
                last_accessed = excluded.last_accessed;
        ";
        cmd.Parameters.AddWithValue("@id", item.Id);
        cmd.Parameters.AddWithValue("@path", item.Path);
        cmd.Parameters.AddWithValue("@name", item.Name);
        cmd.Parameters.AddWithValue("@size", item.Size);
        cmd.Parameters.AddWithValue("@type", item.Type);
        cmd.Parameters.AddWithValue("@added_at", item.AddedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@last_accessed", item.LastAccessed.ToString("O"));

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveFileShelfItemAsync(string id, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM file_shelf WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearFileShelfAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM file_shelf";
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateFileShelfAccessAsync(string id, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE file_shelf SET last_accessed = @now WHERE id = @id";
        cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveMissingShelfItemsAsync(CancellationToken cancellationToken = default)
    {
        var items = await GetFileShelfItemsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var item in items)
        {
            if (item.IsMissing)
            {
                await RemoveFileShelfItemAsync(item.Id, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    #endregion

    #region Transfer History

    public async Task<List<TransferItem>> GetTransferHistoryAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var list = new List<TransferItem>();

        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, direction, filename, size, timestamp, status, hash, error_message
            FROM transfer_history
            ORDER BY timestamp DESC
            LIMIT @limit";
        cmd.Parameters.AddWithValue("@limit", limit);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var item = new TransferItem
            {
                Id = reader.GetString(0),
                Direction = Enum.TryParse<TransferDirection>(reader.GetString(1), out var dir) ? dir : TransferDirection.Upload,
                FileName = reader.GetString(2),
                Size = reader.GetInt64(3),
                Timestamp = DateTimeOffset.Parse(reader.GetString(4)),
                Status = Enum.TryParse<TransferStatus>(reader.GetString(5), out var st) ? st : TransferStatus.Completed,
                Hash = reader.IsDBNull(6) ? null : reader.GetString(6),
                ErrorMessage = reader.IsDBNull(7) ? null : reader.GetString(7),
                BytesTransferred = reader.GetInt64(3) // completed size
            };
            list.Add(item);
        }

        return list;
    }

    public async Task SaveTransferHistoryAsync(TransferItem item, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO transfer_history (id, direction, filename, size, timestamp, status, hash, error_message)
            VALUES (@id, @direction, @filename, @size, @timestamp, @status, @hash, @error_message)
            ON CONFLICT(id) DO UPDATE SET
                status = excluded.status,
                hash = excluded.hash,
                error_message = excluded.error_message;
        ";
        cmd.Parameters.AddWithValue("@id", item.Id);
        cmd.Parameters.AddWithValue("@direction", item.Direction.ToString());
        cmd.Parameters.AddWithValue("@filename", item.FileName);
        cmd.Parameters.AddWithValue("@size", item.Size);
        cmd.Parameters.AddWithValue("@timestamp", item.Timestamp.ToString("O"));
        cmd.Parameters.AddWithValue("@status", item.Status.ToString());
        cmd.Parameters.AddWithValue("@hash", (object?)item.Hash ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@error_message", (object?)item.ErrorMessage ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearTransferHistoryAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM transfer_history";
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    #endregion

    public async Task ClearAllDataAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        using var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            DELETE FROM clipboard_items;
            DELETE FROM notes;
            DELETE FROM calendar_events;
            DELETE FROM saved_links;
            DELETE FROM temporary_messages;
            DELETE FROM app_usage;
            DELETE FROM file_shelf;
            DELETE FROM transfer_history;
        ";
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _lock.Dispose();
    }
}

