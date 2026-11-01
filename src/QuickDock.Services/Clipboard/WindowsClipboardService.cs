using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;
using QuickDock.Core.Settings;
using QuickDock.Infrastructure.Storage;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;

namespace QuickDock.Services.Clipboard;

public sealed class WindowsClipboardService : IClipboardService
{
    private readonly IDatabaseService _databaseService;
    private readonly ISensitiveDataDetector _sensitiveDetector;
    private readonly ISettingsManager _settingsManager;
    private readonly ILogger<WindowsClipboardService> _logger;
    private readonly SemaphoreSlim _clipboardLock = new(1, 1);

    private bool _isListening;
    private bool _isInternalCopy;
    private string _lastCapturedHash = string.Empty;

    public event EventHandler<ClipboardItem>? ClipboardItemAdded;
    public event EventHandler? ClipboardCleared;

    public bool IsPaused
    {
        get => _settingsManager.CurrentSettings.Clipboard.IsPaused;
        set
        {
            var s = _settingsManager.CurrentSettings;
            s.Clipboard.IsPaused = value;
            _settingsManager.SaveAsync(s).ConfigureAwait(false);
        }
    }

    public WindowsClipboardService(
        IDatabaseService databaseService,
        ISensitiveDataDetector sensitiveDetector,
        ISettingsManager settingsManager,
        ILogger<WindowsClipboardService> logger)
    {
        _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
        _sensitiveDetector = sensitiveDetector ?? throw new ArgumentNullException(nameof(sensitiveDetector));
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartListeningAsync()
    {
        if (_isListening) return Task.CompletedTask;

        try
        {
            global::Windows.ApplicationModel.DataTransfer.Clipboard.ContentChanged += OnClipboardContentChanged;
            _isListening = true;
            _logger.LogInformation("Clipboard listener registered successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to register Clipboard.ContentChanged event.");
        }

        return Task.CompletedTask;
    }

    public void StopListening()
    {
        if (!_isListening) return;

        try
        {
            global::Windows.ApplicationModel.DataTransfer.Clipboard.ContentChanged -= OnClipboardContentChanged;
            _isListening = false;
            _logger.LogInformation("Clipboard listener stopped.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to unregister Clipboard.ContentChanged.");
        }
    }

    public Task ProcessCurrentClipboardAsync()
    {
        OnClipboardContentChanged(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    private async void OnClipboardContentChanged(object? sender, object e)
    {
        var settings = _settingsManager.CurrentSettings.Clipboard;
        if (!settings.Enabled || settings.IsPaused)
        {
            return;
        }

        if (_isInternalCopy)
        {
            _isInternalCopy = false;
            return;
        }

        await _clipboardLock.WaitAsync();
        try
        {
            ClipboardItem? item = null;

            try
            {
                var dataView = global::Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
                if (dataView != null)
                {
                    if (dataView.Contains(StandardDataFormats.Text))
                    {
                        var text = await dataView.GetTextAsync();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            var isSensitive = _sensitiveDetector.IsSensitive(text, out _);
                            if (isSensitive && settings.SensitiveAction == SensitiveDataHandling.Ignore)
                            {
                                return; // Privacy: ignore sensitive data
                            }

                            var isUrl = Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri) &&
                                        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

                            var preview = text.Length > 120 ? text.Substring(0, 117) + "..." : text;
                            item = new ClipboardItem
                            {
                                Type = isUrl ? ClipboardContentType.Url : ClipboardContentType.Text,
                                Content = text,
                                Preview = preview.Replace("\r", " ").Replace("\n", " "),
                                IsSensitive = isSensitive
                            };
                        }
                    }
                    else if (dataView.Contains(StandardDataFormats.StorageItems))
                    {
                        var items = await dataView.GetStorageItemsAsync();
                        if (items != null && items.Count > 0)
                        {
                            var paths = items.Select(i => i.Path).Where(p => !string.IsNullOrEmpty(p)).ToList();
                            if (paths.Count > 0)
                            {
                                var content = string.Join(Environment.NewLine, paths);
                                var preview = paths.Count == 1 ? paths[0] : $"{paths.Count} files ({Path.GetFileName(paths[0])}, ...)";
                                item = new ClipboardItem
                                {
                                    Type = ClipboardContentType.Files,
                                    Content = content,
                                    Preview = preview
                                };
                            }
                        }
                    }
                    else if (dataView.Contains(StandardDataFormats.Bitmap))
                    {
                        var bmpRef = await dataView.GetBitmapAsync();
                        if (bmpRef != null)
                        {
                            var fileName = $"clip_{Guid.NewGuid():N}.png";
                            var filePath = Path.Combine(AppStoragePaths.CacheDirectory, fileName);
                            using var stream = await bmpRef.OpenReadAsync();
                            using var outStream = File.Create(filePath);
                            using var netStream = stream.AsStreamForRead();
                            await netStream.CopyToAsync(outStream).ConfigureAwait(false);

                            item = new ClipboardItem
                            {
                                Type = ClipboardContentType.Image,
                                Content = filePath,
                                Preview = "[Image Snapshot]",
                                ImageThumbnailPath = filePath
                            };
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is COMException or InvalidOperationException)
            {
                // Fall back to robust Win32 clipboard read if WinRT encounters thread or container restrictions
                item = TryReadWin32Clipboard(settings);
            }

            if (item == null)
            {
                item = TryReadWin32Clipboard(settings);
            }

            if (item != null)
            {
                // Prevent immediate duplicate capture
                var hash = $"{item.Type}_{item.Content.GetHashCode()}";
                if (hash == _lastCapturedHash)
                {
                    return;
                }
                _lastCapturedHash = hash;

                await _databaseService.SaveClipboardItemAsync(item).ConfigureAwait(false);

                // Enforce retention policy
                var maxAge = settings.Retention switch
                {
                    ClipboardRetention.Hours1 => TimeSpan.FromHours(1),
                    ClipboardRetention.Hours6 => TimeSpan.FromHours(6),
                    ClipboardRetention.Days1 => TimeSpan.FromDays(1),
                    ClipboardRetention.Days7 => TimeSpan.FromDays(7),
                    ClipboardRetention.Days30 => TimeSpan.FromDays(30),
                    _ => TimeSpan.FromDays(7)
                };
                await _databaseService.PruneClipboardRetentionAsync(maxAge, settings.MaxItems).ConfigureAwait(false);

                ClipboardItemAdded?.Invoke(this, item);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to capture clipboard content.");
        }
        finally
        {
            _clipboardLock.Release();
        }
    }

    public async Task<List<ClipboardItem>> GetHistoryAsync(string? search = null, ClipboardContentType? filterType = null)
    {
        var maxItems = _settingsManager.CurrentSettings.Clipboard.MaxItems;
        return await _databaseService.GetClipboardItemsAsync(maxItems, search, filterType).ConfigureAwait(false);
    }

    public async Task CopyItemToClipboardAsync(ClipboardItem item)
    {
        if (item == null) return;

        _isInternalCopy = true;
        var package = new DataPackage();

        switch (item.Type)
        {
            case ClipboardContentType.Text:
            case ClipboardContentType.Url:
                package.SetText(item.Content);
                break;
            case ClipboardContentType.Files:
                package.SetText(item.Content);
                break;
            case ClipboardContentType.Image:
                if (!string.IsNullOrEmpty(item.Content) && File.Exists(item.Content))
                {
                    var file = await StorageFile.GetFileFromPathAsync(item.Content);
                    package.SetBitmap(RandomAccessStreamReference.CreateFromFile(file));
                }
                break;
        }

        global::Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
    }

    public async Task SetPinnedAsync(string id, bool isPinned)
    {
        await _databaseService.SetClipboardItemPinnedAsync(id, isPinned).ConfigureAwait(false);
    }

    public async Task DeleteItemAsync(string id)
    {
        await _databaseService.DeleteClipboardItemAsync(id).ConfigureAwait(false);
    }

    public async Task ClearHistoryAsync(bool clearPinned = false)
    {
        await _databaseService.ClearClipboardHistoryAsync(clearPinned).ConfigureAwait(false);
        ClipboardCleared?.Invoke(this, EventArgs.Empty);
    }

    private ClipboardItem? TryReadWin32Clipboard(ClipboardModuleSettings settings)
    {
        try
        {
            if (!QuickDock.Infrastructure.Windows.NativeMethods.OpenClipboard(IntPtr.Zero))
            {
                return null;
            }

            try
            {
                if (QuickDock.Infrastructure.Windows.NativeMethods.IsClipboardFormatAvailable(QuickDock.Infrastructure.Windows.NativeMethods.CF_UNICODETEXT))
                {
                    IntPtr hData = QuickDock.Infrastructure.Windows.NativeMethods.GetClipboardData(QuickDock.Infrastructure.Windows.NativeMethods.CF_UNICODETEXT);
                    if (hData != IntPtr.Zero)
                    {
                        IntPtr ptr = QuickDock.Infrastructure.Windows.NativeMethods.GlobalLock(hData);
                        if (ptr != IntPtr.Zero)
                        {
                            try
                            {
                                string? text = Marshal.PtrToStringUni(ptr);
                                if (!string.IsNullOrWhiteSpace(text))
                                {
                                    var isSensitive = _sensitiveDetector.IsSensitive(text, out _);
                                    if (isSensitive && settings.SensitiveAction == SensitiveDataHandling.Ignore)
                                    {
                                        return null;
                                    }

                                    var isUrl = Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri) &&
                                                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

                                    var preview = text.Length > 120 ? text.Substring(0, 117) + "..." : text;
                                    return new ClipboardItem
                                    {
                                        Type = isUrl ? ClipboardContentType.Url : ClipboardContentType.Text,
                                        Content = text,
                                        Preview = preview.Replace("\r", " ").Replace("\n", " "),
                                        IsSensitive = isSensitive
                                    };
                                }
                            }
                            finally
                            {
                                QuickDock.Infrastructure.Windows.NativeMethods.GlobalUnlock(hData);
                            }
                        }
                    }
                }
            }
            finally
            {
                QuickDock.Infrastructure.Windows.NativeMethods.CloseClipboard();
            }
        }
        catch
        {
            // Best effort fallback
        }

        return null;
    }

    public void Dispose()
    {
        StopListening();
        _clipboardLock.Dispose();
    }
}
