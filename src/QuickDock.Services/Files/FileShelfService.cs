using System.Diagnostics;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.Services.Files;

public sealed class FileShelfService : IFileShelfService
{
    private readonly IDatabaseService _databaseService;
    private readonly ILogger<FileShelfService> _logger;

    public event EventHandler? ShelfChanged;

    public FileShelfService(IDatabaseService databaseService, ILogger<FileShelfService> logger)
    {
        _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<FileShelfItem>> GetItemsAsync(CancellationToken cancellationToken = default)
    {
        return await _databaseService.GetFileShelfItemsAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<FileShelfItem?> AddFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || (!File.Exists(filePath) && !Directory.Exists(filePath)))
        {
            return null;
        }

        try
        {
            var isDir = Directory.Exists(filePath);
            var name = Path.GetFileName(filePath);
            if (string.IsNullOrEmpty(name)) name = filePath;

            long size = 0;
            string type = isDir ? "Folder" : Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant();
            if (string.IsNullOrEmpty(type)) type = "File";

            if (!isDir && File.Exists(filePath))
            {
                var fi = new FileInfo(filePath);
                size = fi.Length;
            }

            var item = new FileShelfItem
            {
                Path = Path.GetFullPath(filePath),
                Name = name,
                Size = size,
                Type = type,
                AddedAt = DateTimeOffset.UtcNow,
                LastAccessed = DateTimeOffset.UtcNow,
                IsMissing = false
            };

            await _databaseService.SaveFileShelfItemAsync(item, cancellationToken).ConfigureAwait(false);
            ShelfChanged?.Invoke(this, EventArgs.Empty);
            return item;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add file to shelf: {FilePath}", filePath);
            return null;
        }
    }

    public async Task<int> AddFilesAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken = default)
    {
        int added = 0;
        foreach (var path in filePaths)
        {
            var res = await AddFileAsync(path, cancellationToken).ConfigureAwait(false);
            if (res != null) added++;
        }
        return added;
    }

    public async Task RemoveItemAsync(string id, CancellationToken cancellationToken = default)
    {
        await _databaseService.RemoveFileShelfItemAsync(id, cancellationToken).ConfigureAwait(false);
        ShelfChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task ClearShelfAsync(CancellationToken cancellationToken = default)
    {
        await _databaseService.ClearFileShelfAsync(cancellationToken).ConfigureAwait(false);
        ShelfChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<int> RemoveMissingItemsAsync(CancellationToken cancellationToken = default)
    {
        var items = await _databaseService.GetFileShelfItemsAsync(cancellationToken).ConfigureAwait(false);
        int removed = 0;
        foreach (var item in items)
        {
            if (item.IsMissing)
            {
                await _databaseService.RemoveFileShelfItemAsync(item.Id, cancellationToken).ConfigureAwait(false);
                removed++;
            }
        }

        if (removed > 0)
        {
            ShelfChanged?.Invoke(this, EventArgs.Empty);
        }
        return removed;
    }

    public Task OpenFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || (!File.Exists(filePath) && !Directory.Exists(filePath)))
        {
            _logger.LogWarning("Cannot open non-existent file: {FilePath}", filePath);
            return Task.CompletedTask;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open file: {FilePath}", filePath);
        }

        return Task.CompletedTask;
    }

    public Task RevealInExplorerAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return Task.CompletedTask;

        try
        {
            if (File.Exists(filePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{filePath}\"",
                    UseShellExecute = true
                });
            }
            else if (Directory.Exists(filePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{filePath}\"",
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reveal in explorer: {FilePath}", filePath);
        }

        return Task.CompletedTask;
    }

    public Task<bool> DeleteFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return Task.FromResult(false);
        }

        try
        {
            File.Delete(filePath);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file: {FilePath}", filePath);
            return Task.FromResult(false);
        }
    }
}

