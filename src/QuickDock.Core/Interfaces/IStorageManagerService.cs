namespace QuickDock.Core.Interfaces;

public sealed class StorageBreakdownInfo
{
    public long CacheSizeBytes { get; set; }
    public long LogsSizeBytes { get; set; }
    public long DatabaseSizeBytes { get; set; }
    public long TemporarySizeBytes { get; set; }
    public long DataSizeBytes { get; set; }
    public long TotalSizeBytes => CacheSizeBytes + LogsSizeBytes + DatabaseSizeBytes + TemporarySizeBytes + DataSizeBytes;

    public string FormattedCacheSize => FormatBytes(CacheSizeBytes);
    public string FormattedLogsSize => FormatBytes(LogsSizeBytes);
    public string FormattedDatabaseSize => FormatBytes(DatabaseSizeBytes);
    public string FormattedTemporarySize => FormatBytes(TemporarySizeBytes);
    public string FormattedDataSize => FormatBytes(DataSizeBytes);
    public string FormattedTotalSize => FormatBytes(TotalSizeBytes);

    public static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F2} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }
}

public interface IStorageManagerService
{
    Task<StorageBreakdownInfo> GetStorageBreakdownAsync(CancellationToken cancellationToken = default);
    Task ClearCacheAsync(CancellationToken cancellationToken = default);
    Task ClearLogsAsync(CancellationToken cancellationToken = default);
    Task ClearTemporaryDataAsync(CancellationToken cancellationToken = default);
    Task ClearProductivityDataAsync(CancellationToken cancellationToken = default);
    void OpenDataFolder();
}

