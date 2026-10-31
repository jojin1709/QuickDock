namespace QuickDock.Core.Interfaces;

public enum MediaPlaybackStatus
{
    Closed,
    Opened,
    Changing,
    Stopped,
    Playing,
    Paused
}

public record MediaSessionSnapshot(
    string Title,
    string Artist,
    string Album,
    byte[]? ArtworkBytes,
    MediaPlaybackStatus PlaybackStatus,
    bool CanPlayPause,
    bool CanSkipNext,
    bool CanSkipPrevious,
    bool HasSession
);

public interface IMediaService : IDisposable
{
    MediaSessionSnapshot? CurrentMedia { get; }
    Task<MediaSessionSnapshot?> RefreshAsync();
    Task TogglePlayPauseAsync();
    Task SkipNextAsync();
    Task SkipPreviousAsync();
    event EventHandler<MediaSessionSnapshot?>? MediaChanged;
}

