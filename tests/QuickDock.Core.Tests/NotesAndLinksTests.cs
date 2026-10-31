using Microsoft.Extensions.Logging.Abstractions;
using QuickDock.Core.Models;
using QuickDock.Infrastructure.Storage;
using QuickDock.Services.Productivity;
using Xunit;

namespace QuickDock.Core.Tests;

public class NotesAndLinksTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteDatabaseService _db;
    private readonly NotesService _notesService;
    private readonly SavedLinksService _linksService;

    public NotesAndLinksTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"quickdock_test_notes_{Guid.NewGuid():N}.db");
        _db = new SqliteDatabaseService(NullLogger<SqliteDatabaseService>.Instance, _testDbPath);
        _notesService = new NotesService(_db, NullLogger<NotesService>.Instance);
        _linksService = new SavedLinksService(_db, NullLogger<SavedLinksService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }

    [Fact]
    public async Task Notes_Create_Update_Search_Pin_Archive_Delete_Works()
    {
        var note1 = new NoteItem
        {
            Title = "Sprint Goals",
            Content = "Finish Phase 5 productivity modules and verification."
        };
        var note2 = new NoteItem
        {
            Title = "Grocery List",
            Content = "Apples, Milk, Bread"
        };

        await _notesService.SaveNoteAsync(note1);
        await _notesService.SaveNoteAsync(note2);

        var all = await _notesService.GetNotesAsync();
        Assert.Equal(2, all.Count);

        // Search
        var searched = await _notesService.GetNotesAsync("Sprint");
        Assert.Single(searched);
        Assert.Equal("Sprint Goals", searched[0].Title);

        // Update
        note1.Content = "Updated content: All tests passing.";
        await _notesService.SaveNoteAsync(note1);
        var updated = await _notesService.GetNoteByIdAsync(note1.Id);
        Assert.NotNull(updated);
        Assert.Equal("Updated content: All tests passing.", updated.Content);

        // Pin
        await _notesService.SetPinnedAsync(note2.Id, true);
        var pinnedList = await _notesService.GetNotesAsync();
        Assert.True(pinnedList[0].IsPinned); // Pinned notes appear first

        // Archive
        await _notesService.SetArchivedAsync(note2.Id, true);
        var activeOnly = await _notesService.GetNotesAsync(includeArchived: false);
        Assert.Single(activeOnly);
        Assert.Equal(note1.Id, activeOnly[0].Id);

        // Delete
        await _notesService.DeleteNoteAsync(note1.Id);
        var remaining = await _notesService.GetNotesAsync(includeArchived: false);
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task SavedLinks_Create_Pin_Search_Delete_Works()
    {
        var link1 = new SavedLinkItem
        {
            Title = "GitHub QuickDock",
            Url = "https://github.com/jojin1709/QuickDock",
            Description = "QuickDock repository"
        };
        var link2 = new SavedLinkItem
        {
            Title = "WinUI 3 Docs",
            Url = "https://learn.microsoft.com/windows/apps/winui/winui3/",
            Description = "Windows App SDK documentation"
        };

        await _linksService.SaveLinkAsync(link1);
        await _linksService.SaveLinkAsync(link2);

        var links = await _linksService.GetLinksAsync();
        Assert.Equal(2, links.Count);

        // Search
        var searchResults = await _linksService.GetLinksAsync("QuickDock");
        Assert.Single(searchResults);
        Assert.Equal("GitHub QuickDock", searchResults[0].Title);

        // Pin
        await _linksService.SetPinnedAsync(link2.Id, true);
        var sortedLinks = await _linksService.GetLinksAsync();
        Assert.True(sortedLinks[0].IsPinned);
        Assert.Equal(link2.Id, sortedLinks[0].Id);

        // Delete
        await _linksService.DeleteLinkAsync(link1.Id);
        var remaining = await _linksService.GetLinksAsync();
        Assert.Single(remaining);
        Assert.Equal(link2.Id, remaining[0].Id);
    }
}

