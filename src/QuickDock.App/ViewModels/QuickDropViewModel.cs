using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Media.Imaging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;
using Windows.Storage.Streams;

namespace QuickDock.App.ViewModels;

public sealed partial class QuickDropViewModel : ObservableObject, IDisposable
{
    private readonly IQuickDropService _quickDropService;
    private readonly IFileTransferManager _transferManager;
    private readonly ILogger<QuickDropViewModel> _logger;
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcher;
    private global::System.Threading.Timer? _refreshTimer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotRunning))]
    private bool _isRunning;

    public bool IsNotRunning => !IsRunning;

    [ObservableProperty]
    private string _localAddress = string.Empty;

    [ObservableProperty]
    private int _port;

    [ObservableProperty]
    private string _pairingUrl = string.Empty;

    [ObservableProperty]
    private string _sessionToken = string.Empty;

    [ObservableProperty]
    private string _remainingTimeText = "10:00";

    [ObservableProperty]
    private BitmapImage? _qrCodeImage;

    [ObservableProperty]
    private ObservableCollection<QuickDropSharedFile> _sharedFiles = new();

    [ObservableProperty]
    private ObservableCollection<TransferItem> _activeTransfers = new();

    [ObservableProperty]
    private string _statusMessage = "Ready to start session.";

    public QuickDropViewModel(
        IQuickDropService quickDropService,
        IFileTransferManager transferManager,
        ILogger<QuickDropViewModel> logger)
    {
        _quickDropService = quickDropService ?? throw new ArgumentNullException(nameof(quickDropService));
        _transferManager = transferManager ?? throw new ArgumentNullException(nameof(transferManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        _quickDropService.SessionChanged += OnSessionChanged;
        _quickDropService.FileShared += OnFileShared;
        _quickDropService.FileReceived += OnFileReceived;
        _transferManager.TransferUpdated += OnTransferUpdated;
    }

    [RelayCommand]
    public async Task StartSessionAsync()
    {
        try
        {
            StatusMessage = "Starting local QuickDrop server...";
            var session = await _quickDropService.StartSessionAsync();
            UpdateFromSession(session);
            StatusMessage = "QuickDrop active. Waiting for device on local Wi-Fi...";

            _refreshTimer?.Dispose();
            _refreshTimer = new global::System.Threading.Timer(_ =>
            {
                _dispatcher.TryEnqueue(() =>
                {
                    if (_quickDropService.CurrentSession != null)
                    {
                        RemainingTimeText = _quickDropService.CurrentSession.FormattedRemainingTime;
                        if (_quickDropService.CurrentSession.RemainingTime <= TimeSpan.Zero)
                        {
                            StopSession();
                        }
                    }
                });
            }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start QuickDrop session");
            StatusMessage = $"Failed to start: {ex.Message}";
        }
    }

    [RelayCommand]
    public void StopSession()
    {
        try
        {
            _refreshTimer?.Dispose();
            _refreshTimer = null;
            _ = _quickDropService.StopSessionAsync();
            IsRunning = false;
            QrCodeImage = null;
            StatusMessage = "QuickDrop session stopped.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop QuickDrop session");
        }
    }

    [RelayCommand]
    public void AddSharedFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

        try
        {
            var shared = _quickDropService.ShareFile(filePath);
            SharedFiles.Add(shared);
            StatusMessage = $"Shared '{shared.FileName}'.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to share file {FilePath}", filePath);
        }
    }

    [RelayCommand]
    public void RemoveSharedFile(QuickDropSharedFile? file)
    {
        if (file == null) return;
        try
        {
            _quickDropService.RemoveSharedFile(file.FileId);
            SharedFiles.Remove(file);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove shared file {FileId}", file.FileId);
        }
    }

    private void UpdateFromSession(QuickDropSession session)
    {
        IsRunning = session.IsActive;
        LocalAddress = session.LocalAddress;
        Port = session.Port;
        PairingUrl = session.PairingUrl;
        SessionToken = session.Token;
        RemainingTimeText = session.FormattedRemainingTime;

        SharedFiles.Clear();
        foreach (var file in session.SharedFiles.Values)
        {
            SharedFiles.Add(file);
        }

        // Generate QR code
        try
        {
            var qrBytes = _quickDropService.GenerateQrCodePng(session.PairingUrl, 6);
            _ = SetQrImageFromBytesAsync(qrBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate QR code bitmap");
        }
    }

    private async Task SetQrImageFromBytesAsync(byte[] bytes)
    {
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(bytes);
            await writer.StoreAsync();
        }

        var bmp = new BitmapImage();
        await bmp.SetSourceAsync(stream);
        QrCodeImage = bmp;
    }

    private void OnSessionChanged(object? sender, QuickDropSession? session)
    {
        _dispatcher.TryEnqueue(() =>
        {
            if (session == null)
            {
                IsRunning = false;
                QrCodeImage = null;
                StatusMessage = "Session terminated.";
            }
            else
            {
                UpdateFromSession(session);
            }
        });
    }

    private void OnFileShared(object? sender, QuickDropSharedFile file)
    {
        _dispatcher.TryEnqueue(() =>
        {
            if (!SharedFiles.Any(f => f.FileId == file.FileId))
            {
                SharedFiles.Add(file);
            }
        });
    }

    private void OnFileReceived(object? sender, TransferItem transfer)
    {
        _dispatcher.TryEnqueue(() =>
        {
            StatusMessage = $"Received '{transfer.FileName}' ({transfer.FormattedSize}). Verified SHA-256.";
        });
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

    public void Dispose()
    {
        _refreshTimer?.Dispose();
        _quickDropService.SessionChanged -= OnSessionChanged;
        _quickDropService.FileShared -= OnFileShared;
        _quickDropService.FileReceived -= OnFileReceived;
        _transferManager.TransferUpdated -= OnTransferUpdated;
    }
}
