using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.App.ViewModels;

public sealed partial class TransferManagerViewModel : ObservableObject, IDisposable
{
    private readonly IFileTransferManager _transferManager;
    private readonly ILogger<TransferManagerViewModel> _logger;
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcher;

    [ObservableProperty]
    private ObservableCollection<TransferItem> _activeTransfers = new();

    [ObservableProperty]
    private ObservableCollection<TransferItem> _transferHistory = new();

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public TransferManagerViewModel(
        IFileTransferManager transferManager,
        ILogger<TransferManagerViewModel> logger)
    {
        _transferManager = transferManager ?? throw new ArgumentNullException(nameof(transferManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        _transferManager.TransferUpdated += OnTransferUpdated;
        _transferManager.TransferCompleted += OnTransferCompleted;

        _ = LoadHistoryAsync();
    }

    [RelayCommand]
    public async Task LoadHistoryAsync()
    {
        try
        {
            var list = await _transferManager.GetHistoryAsync();
            TransferHistory.Clear();
            foreach (var item in list)
            {
                TransferHistory.Add(item);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load transfer history");
        }
    }

    [RelayCommand]
    public void CancelTransfer(TransferItem? item)
    {
        if (item == null) return;
        _transferManager.CancelTransfer(item.Id);
    }

    [RelayCommand]
    public async Task ClearHistoryAsync()
    {
        try
        {
            await _transferManager.ClearHistoryAsync();
            TransferHistory.Clear();
            StatusMessage = "Transfer history cleared.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear transfer history");
        }
    }

    private void OnTransferUpdated(object? sender, TransferItem item)
    {
        _dispatcher.TryEnqueue(() =>
        {
            var existing = ActiveTransfers.FirstOrDefault(t => t.Id == item.Id);
            if (item.Status == TransferStatus.Transferring)
            {
                if (existing == null)
                {
                    ActiveTransfers.Add(item);
                }
            }
            else
            {
                if (existing != null)
                {
                    ActiveTransfers.Remove(existing);
                }
            }
        });
    }

    private void OnTransferCompleted(object? sender, TransferItem item)
    {
        _dispatcher.TryEnqueue(() =>
        {
            _ = LoadHistoryAsync();
        });
    }

    public void Dispose()
    {
        _transferManager.TransferUpdated -= OnTransferUpdated;
        _transferManager.TransferCompleted -= OnTransferCompleted;
    }
}
