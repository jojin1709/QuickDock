using Microsoft.Extensions.Logging.Abstractions;
using QuickDock.Core.Models;
using QuickDock.Infrastructure.Storage;
using QuickDock.Services.Files;
using Xunit;

namespace QuickDock.Core.Tests;

public class FileTransferTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteDatabaseService _db;
    private readonly FileTransferManager _manager;

    public FileTransferTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"quickdock_transfer_test_{Guid.NewGuid():N}.db");
        _db = new SqliteDatabaseService(NullLogger<SqliteDatabaseService>.Instance, _testDbPath);
        _manager = new FileTransferManager(_db, NullLogger<FileTransferManager>.Instance);
    }

    [Fact]
    public async Task Transfer_Lifecycle_TracksAndPersistsHistory()
    {
        // Arrange
        TransferItem? updatedItem = null;
        TransferItem? completedItem = null;
        _manager.TransferUpdated += (s, e) => updatedItem = e;
        _manager.TransferCompleted += (s, e) => completedItem = e;

        // 1. Register Transfer
        var transfer = _manager.RegisterTransfer(TransferDirection.Upload, "presentation.pptx", 10_000_000);
        Assert.Single(_manager.ActiveTransfers);
        Assert.Equal(TransferStatus.Transferring, transfer.Status);

        // 2. Update Progress
        _manager.UpdateProgress(transfer.Id, 5_000_000, 1_000_000);
        Assert.NotNull(updatedItem);
        Assert.Equal(50.0, updatedItem.ProgressPercentage);

        // 3. Complete Transfer
        var fakeHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        await _manager.CompleteTransferAsync(transfer.Id, fakeHash);

        Assert.Empty(_manager.ActiveTransfers);
        Assert.NotNull(completedItem);
        Assert.Equal(TransferStatus.Completed, completedItem.Status);
        Assert.Equal(fakeHash, completedItem.Hash);

        // 4. Verify History in DB
        var history = await _manager.GetHistoryAsync();
        Assert.Single(history);
        Assert.Equal("presentation.pptx", history[0].FileName);
        Assert.Equal(TransferStatus.Completed, history[0].Status);
        Assert.Equal(fakeHash, history[0].Hash);
    }

    [Fact]
    public async Task CancelTransfer_UpdatesStatusAndHistory()
    {
        var transfer = _manager.RegisterTransfer(TransferDirection.Download, "large_dataset.csv", 50_000_000);
        _manager.CancelTransfer(transfer.Id);

        Assert.Empty(_manager.ActiveTransfers);
        var history = await _manager.GetHistoryAsync();
        Assert.Single(history);
        Assert.Equal(TransferStatus.Cancelled, history[0].Status);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }
}
