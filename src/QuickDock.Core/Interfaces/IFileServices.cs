using QuickDock.Core.Models;

namespace QuickDock.Core.Interfaces;

public interface IFileShelfService
{
    event EventHandler? ShelfChanged;
    Task<List<FileShelfItem>> GetItemsAsync(CancellationToken cancellationToken = default);
    Task<FileShelfItem?> AddFileAsync(string filePath, CancellationToken cancellationToken = default);
    Task<int> AddFilesAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken = default);
    Task RemoveItemAsync(string id, CancellationToken cancellationToken = default);
    Task ClearShelfAsync(CancellationToken cancellationToken = default);
    Task<int> RemoveMissingItemsAsync(CancellationToken cancellationToken = default);
    Task OpenFileAsync(string filePath);
    Task RevealInExplorerAsync(string filePath);
    Task<bool> DeleteFileAsync(string filePath);
}

public interface IFilePreviewService
{
    Task<FilePreviewInfo> GeneratePreviewAsync(string filePath, CancellationToken cancellationToken = default);
}

public interface IFileHashService
{
    Task<string> ComputeSha256Async(string filePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    Task<string> ComputeSha256Async(Stream stream, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
}

public interface IFileConversionService
{
    bool IsSupportedImageConversion(string sourceExt, string targetExt);
    Task<string> ConvertImageAsync(string sourcePath, string targetFormat, int quality = 90, string? outputDir = null, CancellationToken cancellationToken = default);
    Task<string> CreateZipArchiveAsync(IEnumerable<string> sourcePaths, string targetZipPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    Task<int> ExtractZipArchiveAsync(string zipFilePath, string destinationDirectory, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    Task<string> ConvertTextEncodingAsync(string sourcePath, System.Text.Encoding targetEncoding, string? outputDir = null, CancellationToken cancellationToken = default);
}

public interface IFileTransferManager
{
    IReadOnlyList<TransferItem> ActiveTransfers { get; }
    event EventHandler<TransferItem>? TransferUpdated;
    event EventHandler<TransferItem>? TransferCompleted;
    TransferItem RegisterTransfer(TransferDirection direction, string fileName, long size);
    void UpdateProgress(string transferId, long bytesTransferred, double speed);
    Task CompleteTransferAsync(string transferId, string? hash);
    Task FailTransferAsync(string transferId, string error);
    void CancelTransfer(string transferId);
    Task<List<TransferItem>> GetHistoryAsync(CancellationToken cancellationToken = default);
    Task ClearHistoryAsync(CancellationToken cancellationToken = default);
}

public interface IQuickDropService
{
    QuickDropSession? CurrentSession { get; }
    bool IsRunning { get; }
    event EventHandler<QuickDropSession?>? SessionChanged;
    event EventHandler<QuickDropSharedFile>? FileShared;
    event EventHandler<TransferItem>? FileReceived;
    Task<QuickDropSession> StartSessionAsync(CancellationToken cancellationToken = default);
    Task StopSessionAsync();
    QuickDropSharedFile ShareFile(string filePath);
    bool RemoveSharedFile(string fileId);
    byte[] GenerateQrCodePng(string content, int pixelsPerModule = 6);
}
