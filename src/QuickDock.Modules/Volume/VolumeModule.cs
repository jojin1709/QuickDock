using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Modules;

namespace QuickDock.Modules.Volume;

public sealed class VolumeModule : ModuleBase
{
    private readonly IAudioService _audioService;

    public override string Id => "quickdock.volume";
    public override string Name => "Volume Control";
    public override string Description => "System master volume and audio output device management.";
    public override ModuleCategory Category => ModuleCategory.System;

    public float CurrentVolume => _audioService.GetMasterVolume();
    public bool IsMuted => _audioService.GetMuteState();
    public string DeviceName => _audioService.GetDefaultDeviceName();

    public event EventHandler<AudioVolumeChangedEventArgs>? VolumeChanged;

    public VolumeModule(IAudioService audioService, ILogger<VolumeModule> logger) : base(logger)
    {
        _audioService = audioService;
        _audioService.VolumeChanged += (s, e) => VolumeChanged?.Invoke(this, e);
    }

    public void SetVolume(float level) => _audioService.SetMasterVolume(level);
    public void ToggleMute() => _audioService.ToggleMute();
    public void IncreaseVolume(float step = 0.05f) => _audioService.IncreaseVolume(step);
    public void DecreaseVolume(float step = 0.05f) => _audioService.DecreaseVolume(step);

    protected override Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Volume module initialized for audio device: {Device}", DeviceName);
        return Task.CompletedTask;
    }

    protected override Task OnStartAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Volume module activated.");
        return Task.CompletedTask;
    }

    protected override Task OnStopAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Volume module deactivated.");
        return Task.CompletedTask;
    }
}

