using Microsoft.Extensions.Logging.Abstractions;
using QuickDock.Core.Models;
using QuickDock.Infrastructure.Storage;
using QuickDock.Services.Productivity;
using Xunit;

namespace QuickDock.Core.Tests;

public class CalendarAndTempMessageTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteDatabaseService _db;
    private readonly CalendarService _calendarService;
    private readonly TemporaryMessageService _tempMessageService;

    public CalendarAndTempMessageTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"quickdock_test_cal_{Guid.NewGuid():N}.db");
        _db = new SqliteDatabaseService(NullLogger<SqliteDatabaseService>.Instance, _testDbPath);
        _calendarService = new CalendarService(_db, NullLogger<CalendarService>.Instance);
        _tempMessageService = new TemporaryMessageService(_db, NullLogger<TemporaryMessageService>.Instance);
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
    public async Task CalendarService_Create_Query_NextEvent_Delete_Works()
    {
        var targetDate = DateTime.Today.AddDays(1);
        var ev1 = new CalendarEventItem
        {
            Title = "Team Standup",
            Description = "Daily team syncing",
            EventDate = targetDate,
            StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(10.5)
        };
        var ev2 = new CalendarEventItem
        {
            Title = "Architecture Review",
            Description = "Review Phase 5 modules",
            EventDate = targetDate,
            StartTime = TimeSpan.FromHours(14),
            EndTime = TimeSpan.FromHours(15)
        };

        await _calendarService.SaveEventAsync(ev1);
        await _calendarService.SaveEventAsync(ev2);

        // Query by date
        var dateEvents = await _calendarService.GetEventsForDateAsync(targetDate);
        Assert.Equal(2, dateEvents.Count);
        Assert.Equal("Team Standup", dateEvents[0].Title);

        // Query by month
        var monthEvents = await _calendarService.GetEventsForMonthAsync(targetDate.Year, targetDate.Month);
        Assert.True(monthEvents.Count >= 2);

        // Next upcoming event
        var next = await _calendarService.GetNextUpcomingEventAsync();
        Assert.NotNull(next);
        Assert.Equal("Team Standup", next.Title);

        // Delete
        await _calendarService.DeleteEventAsync(ev1.Id);
        var afterDelete = await _calendarService.GetEventsForDateAsync(targetDate);
        Assert.Single(afterDelete);
        Assert.Equal(ev2.Id, afterDelete[0].Id);
    }

    [Fact]
    public async Task TemporaryMessageService_Create_Active_Dismiss_Expiration()
    {
        // Set short active message
        await _tempMessageService.SetMessageAsync("Remember to push git commits", TimeSpan.FromMinutes(30));

        var active = await _tempMessageService.GetActiveMessageAsync();
        Assert.NotNull(active);
        Assert.Equal("Remember to push git commits", active.Content);

        // Dismiss
        await _tempMessageService.DismissMessageAsync(active.Id);
        var dismissedActive = await _tempMessageService.GetActiveMessageAsync();
        Assert.Null(dismissedActive);

        // Create expired message directly in db to verify pruning
        var expiredMsg = new TemporaryMessageItem
        {
            Content = "Expired old notification",
            ExpiresAt = DateTime.UtcNow.AddHours(-1)
        };
        await _db.SaveTemporaryMessageAsync(expiredMsg);

        var queried = await _tempMessageService.GetActiveMessageAsync();
        Assert.Null(queried);
    }
}

