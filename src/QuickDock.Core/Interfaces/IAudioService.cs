namespace QuickDock.Core.Interfaces;

public record AudioVolumeChangedEventArgs(float NewVolume, bool IsMuted);

public interface IAudioService : IDisposable
{
    float GetMasterVolume();
    void SetMasterVolume(float level);
    void IncreaseVolume(float step = 0.05f);
    void DecreaseVolume(float step = 0.05f);
    bool GetMuteState();
    void SetMuteState(bool mute);
    void ToggleMute();
    string GetDefaultDeviceName();
    event EventHandler<AudioVolumeChangedEventArgs>? VolumeChanged;
}

