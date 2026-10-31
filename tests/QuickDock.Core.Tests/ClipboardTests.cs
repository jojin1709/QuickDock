using Microsoft.Extensions.Logging.Abstractions;
using QuickDock.Core.Models;
using QuickDock.Infrastructure.Storage;
using QuickDock.Services.Clipboard;
using Xunit;

namespace QuickDock.Core.Tests;

public class ClipboardTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteDatabaseService _db;
    private readonly SensitiveDataDetector _detector;

    public ClipboardTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"quickdock_test_clip_{Guid.NewGuid():N}.db");
        _db = new SqliteDatabaseService(NullLogger<SqliteDatabaseService>.Instance, _testDbPath);
        _detector = new SensitiveDataDetector();
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }

    [Theory]
    [InlineData("AKIAIOSFODNN7EXAMPLE", true, "AWS Access Key")]
    [InlineData("ghp_1234567890abcdefghijklmnopqrstuvwxyz", true, "GitHub Personal Access Token")]
    [InlineData("sk-abcdefghijklmnopqrstuvwxyz1234567890", true, "API Secret Key")]
    [InlineData("-----BEGIN RSA PRIVATE KEY-----\nMIIEowIBAAKCAQEA0...", true, "Private Key")]
    [InlineData("Hello world this is just normal text", false, null)]
    [InlineData("https://github.com/jojin1709/QuickDock", false, null)]
    public void SensitiveDataDetector_DetectsObviousSecrets(string text, bool expectedSensitive, string? expectedSnippet)
    {
        bool isSensitive = _detector.IsSensitive(text, out var reason);

        Assert.Equal(expectedSensitive, isSensitive);
        if (expectedSensitive && expectedSnippet != null)
        {
            Assert.Contains(expectedSnippet, reason);
        }
    }

    [Fact]
    public async Task ClipboardItems_Create_Search_Pin_Delete_Works()
    {
        await _db.InitializeAsync();

        var item1 = new ClipboardItem
        {
            Type = ClipboardContentType.Text,
            Content = "npm install express",
            Preview = "npm install express"
        };
        var item2 = new ClipboardItem
        {
            Type = ClipboardContentType.Url,
            Content = "https://github.com/dotnet/winui",
            Preview = "https://github.com/dotnet/winui"
        };

        await _db.SaveClipboardItemAsync(item1);
        await _db.SaveClipboardItemAsync(item2);

        // Fetch
        var items = await _db.GetClipboardItemsAsync(10);
        Assert.Equal(2, items.Count);

        // Search
        var searched = await _db.GetClipboardItemsAsync(10, search: "express");
        Assert.Single(searched);
        Assert.Equal("npm install express", searched[0].Content);

        // Pin
        await _db.SetClipboardItemPinnedAsync(item1.Id, true);
        var pinned = await _db.GetClipboardItemByIdAsync(item1.Id);
        Assert.NotNull(pinned);
        Assert.True(pinned.IsPinned);

        // Delete
        await _db.DeleteClipboardItemAsync(item2.Id);
        var remaining = await _db.GetClipboardItemsAsync(10);
        Assert.Single(remaining);
        Assert.Equal(item1.Id, remaining[0].Id);
    }

    [Fact]
    public async Task ClipboardRetention_PrunesUnpinnedOldAndExcessItems()
    {
        await _db.InitializeAsync();

        // Add 5 items with past dates
        for (int i = 0; i < 5; i++)
        {
            await _db.SaveClipboardItemAsync(new ClipboardItem
            {
                Content = $"Old item {i}",
                Preview = $"Old item {i}",
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                IsPinned = false
            });
        }

        // Add 1 pinned item that is old
        var pinnedOld = new ClipboardItem
        {
            Content = "Important Pinned Old Item",
            Preview = "Important Pinned Old Item",
            CreatedAt = DateTime.UtcNow.AddDays(-15),
            IsPinned = true
        };
        await _db.SaveClipboardItemAsync(pinnedOld);

        // Add 2 fresh items
        await _db.SaveClipboardItemAsync(new ClipboardItem
        {
            Content = "Fresh 1",
            Preview = "Fresh 1",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveClipboardItemAsync(new ClipboardItem
        {
            Content = "Fresh 2",
            Preview = "Fresh 2",
            CreatedAt = DateTime.UtcNow
        });

        // Prune older than 7 days, max items 10
        await _db.PruneClipboardRetentionAsync(TimeSpan.FromDays(7), 10);

        var remaining = await _db.GetClipboardItemsAsync(20);
        // The 5 unpinned old items should be pruned; the pinned item and 2 fresh items remain
        Assert.Equal(3, remaining.Count);
        Assert.Contains(remaining, x => x.Content == "Important Pinned Old Item");
        Assert.Contains(remaining, x => x.Content == "Fresh 1");
        Assert.Contains(remaining, x => x.Content == "Fresh 2");
    }
}

