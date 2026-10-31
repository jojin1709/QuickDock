using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using Windows.Media.Control;

namespace QuickDock.Services.Media;

public sealed class WindowsMediaService : IMediaService
{
    private readonly ILogger<WindowsMediaService> _logger;
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;
    private MediaSessionSnapshot? _cachedSnapshot;
    private bool _isDisposed;

    public event EventHandler<MediaSessionSnapshot?>? MediaChanged;

    public MediaSessionSnapshot? CurrentMedia => _cachedSnapshot;

    public WindowsMediaService(ILogger<WindowsMediaService> logger)
    {
        _logger = logger;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (_manager != null)
            {
                _manager.CurrentSessionChanged += OnCurrentSessionChanged;
                await UpdateCurrentSessionAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize GlobalSystemMediaTransportControlsSessionManager.");
        }
    }

    private async void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        await UpdateCurrentSessionAsync();
    }

    private async Task UpdateCurrentSessionAsync()
    {
        try
        {
            if (_currentSession != null)
            {
                _currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                _currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            }

            _currentSession = _manager?.GetCurrentSession();

            if (_currentSession != null)
            {
                _currentSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
                _currentSession.MediaPropertiesChanged += OnMediaPropertiesChanged;
            }

            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error updating media session.");
        }
    }

    private async void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
    {
        await RefreshAsync();
    }

    private async void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
    {
        await RefreshAsync();
    }

    public async Task<MediaSessionSnapshot?> RefreshAsync()
    {
        if (_currentSession == null)
        {
            _cachedSnapshot = new MediaSessionSnapshot(
                Title: "No media playing",
                Artist: string.Empty,
                Album: string.Empty,
                ArtworkBytes: null,
                PlaybackStatus: MediaPlaybackStatus.Closed,
                CanPlayPause: false,
                CanSkipNext: false,
                CanSkipPrevious: false,
                HasSession: false
            );
            MediaChanged?.Invoke(this, _cachedSnapshot);
            return _cachedSnapshot;
        }

        try
        {
            var mediaProps = await _currentSession.TryGetMediaPropertiesAsync();
            var playbackInfo = _currentSession.GetPlaybackInfo();

            string title = mediaProps?.Title ?? "Unknown Title";
            string artist = mediaProps?.Artist ?? string.Empty;
            string album = mediaProps?.AlbumTitle ?? string.Empty;

            byte[]? artwork = null;
            if (mediaProps?.Thumbnail != null)
            {
                try
                {
                    using var stream = await mediaProps.Thumbnail.OpenReadAsync();
                    using var netStream = stream.AsStreamForRead();
                    using var ms = new MemoryStream();
                    await netStream.CopyToAsync(ms);
                    artwork = ms.ToArray();
                }
                catch
                {
                    artwork = null;
                }
            }

            var status = MapPlaybackStatus(playbackInfo?.PlaybackStatus);
            bool canPlayPause = playbackInfo?.Controls?.IsPlayPauseToggleEnabled ?? false;
            bool canNext = playbackInfo?.Controls?.IsNextEnabled ?? false;
            bool canPrev = playbackInfo?.Controls?.IsPreviousEnabled ?? false;

            _cachedSnapshot = new MediaSessionSnapshot(
                Title: string.IsNullOrWhiteSpace(title) ? "No media playing" : title,
                Artist: artist,
                Album: album,
                ArtworkBytes: artwork,
                PlaybackStatus: status,
                CanPlayPause: canPlayPause,
                CanSkipNext: canNext,
                CanSkipPrevious: canPrev,
                HasSession: true
            );

            MediaChanged?.Invoke(this, _cachedSnapshot);
            return _cachedSnapshot;
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Failed to refresh media properties.");
            return _cachedSnapshot;
        }
    }

    public async Task TogglePlayPauseAsync()
    {
        if (_currentSession == null) return;
        try
        {
            await _currentSession.TryTogglePlayPauseAsync();
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TogglePlayPause failed.");
        }
    }

    public async Task SkipNextAsync()
    {
        if (_currentSession == null) return;
        try
        {
            await _currentSession.TrySkipNextAsync();
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SkipNext failed.");
        }
    }

    public async Task SkipPreviousAsync()
    {
        if (_currentSession == null) return;
        try
        {
            await _currentSession.TrySkipPreviousAsync();
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SkipPrevious failed.");
        }
    }

    public static MediaPlaybackStatus MapPlaybackStatus(GlobalSystemMediaTransportControlsSessionPlaybackStatus? status)
    {
        return status switch
        {
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => MediaPlaybackStatus.Playing,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => MediaPlaybackStatus.Paused,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => MediaPlaybackStatus.Stopped,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Changing => MediaPlaybackStatus.Changing,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Opened => MediaPlaybackStatus.Opened,
            _ => MediaPlaybackStatus.Closed
        };
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (_currentSession != null)
        {
            _currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            _currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            _currentSession = null;
        }

        if (_manager != null)
        {
            _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
            _manager = null;
        }
    }
}

