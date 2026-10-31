using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.Services.Files;

public sealed class FileTransferManager : IFileTransferManager
{
    private readonly IDatabaseService _databaseService;
    private readonly ILogger<FileTransferManager> _logger;
    private readonly ConcurrentDictionary<string, TransferItem> _activeTransfers = new();

    public IReadOnlyList<TransferItem> ActiveTransfers => _activeTransfers.Values.ToList();

    public event EventHandler<TransferItem>? TransferUpdated;
    public event EventHandler<TransferItem>? TransferCompleted;

    public FileTransferManager(IDatabaseService databaseService, ILogger<FileTransferManager> logger)
    {
        _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public TransferItem RegisterTransfer(TransferDirection direction, string fileName, long size)
    {
        var item = new TransferItem
        {
            Direction = direction,
            FileName = fileName,
            Size = size,
            BytesTransferred = 0,
            SpeedBytesPerSecond = 0,
            Status = TransferStatus.Transferring,
            Timestamp = DateTimeOffset.UtcNow
        };

        _activeTransfers[item.Id] = item;
        TransferUpdated?.Invoke(this, item);
        return item;
    }

    public void UpdateProgress(string transferId, long bytesTransferred, double speed)
    {
        if (_activeTransfers.TryGetValue(transferId, out var item))
        {
            item.BytesTransferred = bytesTransferred;
            item.SpeedBytesPerSecond = speed;
            TransferUpdated?.Invoke(this, item);
        }
    }

    public async Task CompleteTransferAsync(string transferId, string? hash)
    {
        if (_activeTransfers.TryRemove(transferId, out var item))
        {
            item.Status = TransferStatus.Completed;
            item.BytesTransferred = item.Size;
            item.Hash = hash;
            item.SpeedBytesPerSecond = 0;

            await _databaseService.SaveTransferHistoryAsync(item).ConfigureAwait(false);
            TransferUpdated?.Invoke(this, item);
            TransferCompleted?.Invoke(this, item);
        }
    }

    public async Task FailTransferAsync(string transferId, string error)
    {
        if (_activeTransfers.TryRemove(transferId, out var item))
        {
            item.Status = TransferStatus.Failed;
            item.ErrorMessage = error;
            item.SpeedBytesPerSecond = 0;

            await _databaseService.SaveTransferHistoryAsync(item).ConfigureAwait(false);
            TransferUpdated?.Invoke(this, item);
            TransferCompleted?.Invoke(this, item);
        }
    }

    public void CancelTransfer(string transferId)
    {
        if (_activeTransfers.TryRemove(transferId, out var item))
        {
            item.Status = TransferStatus.Cancelled;
            item.SpeedBytesPerSecond = 0;
            _ = _databaseService.SaveTransferHistoryAsync(item);
            TransferUpdated?.Invoke(this, item);
            TransferCompleted?.Invoke(this, item);
        }
    }

    public async Task<List<TransferItem>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        return await _databaseService.GetTransferHistoryAsync(100, cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearHistoryAsync(CancellationToken cancellationToken = default)
    {
        await _databaseService.ClearTransferHistoryAsync(cancellationToken).ConfigureAwait(false);
    }
}
