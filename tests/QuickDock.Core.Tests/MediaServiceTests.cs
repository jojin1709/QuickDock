using QuickDock.Core.Interfaces;
using QuickDock.Services.Media;
using Xunit;

namespace QuickDock.Core.Tests;

public class MediaServiceTests
{
    [Fact]
    public void MapPlaybackStatus_MapsExpectedEnumValues()
    {
        Assert.Equal(MediaPlaybackStatus.Playing,
            WindowsMediaService.MapPlaybackStatus(Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing));

        Assert.Equal(MediaPlaybackStatus.Paused,
            WindowsMediaService.MapPlaybackStatus(Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused));

        Assert.Equal(MediaPlaybackStatus.Stopped,
            WindowsMediaService.MapPlaybackStatus(Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped));

        Assert.Equal(MediaPlaybackStatus.Closed,
            WindowsMediaService.MapPlaybackStatus(Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed));

        Assert.Equal(MediaPlaybackStatus.Closed,
            WindowsMediaService.MapPlaybackStatus(null));
    }

    [Fact]
    public void MediaSessionSnapshot_FallbackState_IsCorrect()
    {
        var snapshot = new MediaSessionSnapshot(
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

        Assert.False(snapshot.HasSession);
        Assert.Equal("No media playing", snapshot.Title);
        Assert.Equal(MediaPlaybackStatus.Closed, snapshot.PlaybackStatus);
        Assert.False(snapshot.CanPlayPause);
    }
}

