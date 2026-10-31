using Microsoft.Extensions.Logging.Abstractions;
using QuickDock.Core.Models;
using QuickDock.Infrastructure.Storage;
using QuickDock.Services.Files;
using Xunit;

namespace QuickDock.Core.Tests;

public class FileShelfTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteDatabaseService _db;
    private readonly FileShelfService _service;
    private readonly List<string> _tempFiles = new();

    public FileShelfTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"quickdock_shelf_test_{Guid.NewGuid():N}.db");
        _db = new SqliteDatabaseService(NullLogger<SqliteDatabaseService>.Instance, _testDbPath);
        _service = new FileShelfService(_db, NullLogger<FileShelfService>.Instance);
    }

    private string CreateTempFile(string name = "test.txt", string content = "QuickDock File Shelf")
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}_{name}");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    [Fact]
    public async Task AddFile_ValidFile_PersistsInShelf()
    {
        // Arrange
        var file = CreateTempFile("report.pdf", "Dummy PDF content");

        // Act
        var item = await _service.AddFileAsync(file);
        var allItems = await _service.GetItemsAsync();

        // Assert
        Assert.NotNull(item);
        Assert.Equal(Path.GetFileName(file), item.Name);
        Assert.False(item.IsMissing);
        Assert.Contains(allItems, i => i.Id == item.Id && i.Path == file);
    }

    [Fact]
    public async Task RemoveMissingItems_DetectsAndPurgesDeletedFiles()
    {
        // Arrange
        var file1 = CreateTempFile("keep.txt");
        var file2 = CreateTempFile("delete_me.txt");

        var item1 = await _service.AddFileAsync(file1);
        var item2 = await _service.AddFileAsync(file2);

        // Delete file2 from disk externally
        File.Delete(file2);

        // Act
        int removedCount = await _service.RemoveMissingItemsAsync();
        var remaining = await _service.GetItemsAsync();

        // Assert
        Assert.Equal(1, removedCount);
        Assert.Contains(remaining, i => i.Id == item1!.Id);
        Assert.DoesNotContain(remaining, i => i.Id == item2!.Id);
    }

    [Fact]
    public async Task ClearShelf_RemovesAllItems()
    {
        // Arrange
        var file1 = CreateTempFile("doc1.txt");
        var file2 = CreateTempFile("doc2.txt");
        await _service.AddFileAsync(file1);
        await _service.AddFileAsync(file2);

        // Act
        await _service.ClearShelfAsync();
        var allItems = await _service.GetItemsAsync();

        // Assert
        Assert.Empty(allItems);
    }

    public void Dispose()
    {
        _db.Dispose();
        foreach (var file in _tempFiles)
        {
            if (File.Exists(file))
            {
                try { File.Delete(file); } catch { }
            }
        }
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }
}
