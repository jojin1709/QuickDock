using System.Text.Json.Serialization;

namespace QuickDock.Core.Models;

public sealed class FileShelfItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Type { get; set; } = string.Empty;
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastAccessed { get; set; } = DateTimeOffset.UtcNow;
    public bool IsMissing { get; set; }

    public string FormattedSize => FormatBytes(Size);
    public string FormattedTime => AddedAt.ToLocalTime().ToString("g");

    public static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        int unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }
        return $"{size:0.##} {units[unitIndex]}";
    }
}

public enum TransferDirection
{
    Upload,
    Download
}

public enum TransferStatus
{
    Queued,
    Connecting,
    Transferring,
    Paused,
    Completed,
    Failed,
    Cancelled
}

public sealed class TransferItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public TransferDirection Direction { get; set; } = TransferDirection.Upload;
    public string FileName { get; set; } = string.Empty;
    public long Size { get; set; }
    public long BytesTransferred { get; set; }
    public double SpeedBytesPerSecond { get; set; }
    public double ProgressPercentage => Size > 0 ? Math.Clamp((double)BytesTransferred / Size * 100.0, 0.0, 100.0) : 0.0;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public TransferStatus Status { get; set; } = TransferStatus.Queued;
    public string? Hash { get; set; }
    public string? ErrorMessage { get; set; }

    public string FormattedSize => FileShelfItem.FormatBytes(Size);
    public string FormattedTransferred => FileShelfItem.FormatBytes(BytesTransferred);
    public string FormattedSpeed => $"{FileShelfItem.FormatBytes((long)SpeedBytesPerSecond)}/s";
    public string FormattedProgress => $"{ProgressPercentage:0.#}%";
}

public enum PreviewType
{
    Image,
    Text,
    Unsupported
}

public sealed class FilePreviewInfo
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long Size { get; set; }
    public PreviewType Type { get; set; } = PreviewType.Unsupported;
    public string? Dimensions { get; set; }
    public string? TextContent { get; set; }
    public bool IsTruncated { get; set; }
    public string FormattedSize => FileShelfItem.FormatBytes(Size);
    public string Extension => System.IO.Path.GetExtension(Path).TrimStart('.').ToUpperInvariant();
}

public sealed class QuickDropSharedFile
{
    public string FileId { get; set; } = Guid.NewGuid().ToString("N");
    public string OriginalPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long Size { get; set; }
    public string? Hash { get; set; }
    public string FormattedSize => FileShelfItem.FormatBytes(Size);
}

public sealed class QuickDropSession
{
    public string Token { get; set; } = string.Empty;
    public string LocalAddress { get; set; } = string.Empty;
    public int Port { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddMinutes(10);
    public bool IsActive { get; set; }
    public Dictionary<string, QuickDropSharedFile> SharedFiles { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string PairingUrl => $"http://{LocalAddress}:{Port}/?token={Token}";
    public TimeSpan RemainingTime => ExpiresAt > DateTimeOffset.UtcNow ? ExpiresAt - DateTimeOffset.UtcNow : TimeSpan.Zero;
    public string FormattedRemainingTime => RemainingTime > TimeSpan.Zero ? RemainingTime.ToString(@"mm\:ss") : "00:00";
}

public enum ConversionFormat
{
    Png,
    Jpeg,
    Webp,
    Bmp,
    Zip,
    TextUtf8,
    TextUtf16,
    TextAscii
}

public enum ConversionStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}

public sealed class ConversionTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SourcePath { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public string SourceFormat { get; set; } = string.Empty;
    public string TargetFormat { get; set; } = string.Empty;
    public int Quality { get; set; } = 90;
    public ConversionStatus Status { get; set; } = ConversionStatus.Pending;
    public double Progress { get; set; }
    public string? ErrorMessage { get; set; }
}
