using System.Diagnostics;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Infrastructure.Storage;

namespace QuickDock.Services.Storage;

public sealed class StorageManagerService : IStorageManagerService
{
    private readonly IDatabaseService _databaseService;
    private readonly ILogger<StorageManagerService> _logger;

    public StorageManagerService(IDatabaseService databaseService, ILogger<StorageManagerService> logger)
    {
        _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<StorageBreakdownInfo> GetStorageBreakdownAsync(CancellationToken cancellationToken = default)
    {
        AppStoragePaths.EnsureDirectoriesExist();

        var breakdown = new StorageBreakdownInfo
        {
            CacheSizeBytes = GetDirectorySize(AppStoragePaths.CacheDirectory),
            LogsSizeBytes = GetDirectorySize(AppStoragePaths.LogsDirectory),
            TemporarySizeBytes = GetDirectorySize(AppStoragePaths.QuickDropDirectory) + GetTempFilesSize(AppStoragePaths.RootDirectory),
            DataSizeBytes = GetDirectorySize(AppStoragePaths.DataDirectory),
            DatabaseSizeBytes = GetFileSize(AppStoragePaths.DatabaseFile)
        };

        return Task.FromResult(breakdown);
    }

    public Task ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        DeleteDirectoryContents(AppStoragePaths.CacheDirectory);
        _logger.LogInformation("Cache directory cleared.");
        return Task.CompletedTask;
    }

    public Task ClearLogsAsync(CancellationToken cancellationToken = default)
    {
        DeleteDirectoryContents(AppStoragePaths.LogsDirectory);
        _logger.LogInformation("Logs directory cleared.");
        return Task.CompletedTask;
    }

    public Task ClearTemporaryDataAsync(CancellationToken cancellationToken = default)
    {
        DeleteDirectoryContents(AppStoragePaths.QuickDropDirectory);
        DeleteTempFiles(AppStoragePaths.RootDirectory);
        _logger.LogInformation("Temporary files cleared.");
        return Task.CompletedTask;
    }

    public async Task ClearProductivityDataAsync(CancellationToken cancellationToken = default)
    {
        await _databaseService.ClearAllDataAsync().ConfigureAwait(false);
        _logger.LogInformation("All local productivity database records cleared.");
    }

    public void OpenDataFolder()
    {
        try
        {
            AppStoragePaths.EnsureDirectoriesExist();
            Process.Start(new ProcessStartInfo
            {
                FileName = AppStoragePaths.RootDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open QuickDock data directory in Windows Explorer.");
        }
    }

    private static long GetDirectorySize(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return 0;

        try
        {
            long size = 0;
            var dirInfo = new DirectoryInfo(directoryPath);
            foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                try
                {
                    size += file.Length;
                }
                catch
                {
                    // Ignore inaccessible files
                }
            }
            return size;
        }
        catch
        {
            return 0;
        }
    }

    private static long GetFileSize(string filePath)
    {
        if (!File.Exists(filePath)) return 0;
        try
        {
            return new FileInfo(filePath).Length;
        }
        catch
        {
            return 0;
        }
    }

    private static long GetTempFilesSize(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return 0;
        try
        {
            long size = 0;
            var dirInfo = new DirectoryInfo(directoryPath);
            foreach (var file in dirInfo.EnumerateFiles("*.tmp", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    size += file.Length;
                }
                catch
                {
                }
            }
            return size;
        }
        catch
        {
            return 0;
        }
    }

    private void DeleteDirectoryContents(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return;

        try
        {
            var dir = new DirectoryInfo(directoryPath);
            foreach (var file in dir.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                try
                {
                    file.Delete();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not delete locked file {Path}", file.FullName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while deleting directory contents of {Dir}", directoryPath);
        }
    }

    private void DeleteTempFiles(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return;

        try
        {
            var dir = new DirectoryInfo(directoryPath);
            foreach (var file in dir.EnumerateFiles("*.tmp", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    file.Delete();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not delete temp file {Path}", file.FullName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error deleting temp files in {Dir}", directoryPath);
        }
    }
}

