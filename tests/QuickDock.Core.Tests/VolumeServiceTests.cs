using Microsoft.Extensions.Logging.Abstractions;
using QuickDock.Core.Interfaces;
using QuickDock.Services.Audio;
using Xunit;

namespace QuickDock.Core.Tests;

public class VolumeServiceTests
{
    [Fact]
    public void CoreAudioService_Initialization_DoesNotThrow()
    {
        using var service = new CoreAudioService(NullLogger<CoreAudioService>.Instance);

        float vol = service.GetMasterVolume();
        Assert.InRange(vol, 0.0f, 1.0f);

        bool muted = service.GetMuteState();
        // Simply assert bool doesn't throw
        Assert.True(muted || !muted);

        string device = service.GetDefaultDeviceName();
        Assert.False(string.IsNullOrWhiteSpace(device));
    }

    [Fact]
    public void CoreAudioService_ClampsVolumeLevelBetweenZeroAndOne()
    {
        using var service = new CoreAudioService(NullLogger<CoreAudioService>.Instance);

        float initial = service.GetMasterVolume();

        // Safe restoration in try-finally
        try
        {
            service.SetMasterVolume(1.5f);
            float afterHigh = service.GetMasterVolume();
            Assert.True(afterHigh <= 1.0f);

            service.SetMasterVolume(-0.5f);
            float afterLow = service.GetMasterVolume();
            Assert.True(afterLow >= 0.0f);
        }
        finally
        {
            service.SetMasterVolume(initial);
        }
    }
}

