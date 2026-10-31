using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Modules;

namespace QuickDock.Modules.Media;

public sealed class MediaModule : ModuleBase
{
    private readonly IMediaService _mediaService;

    public override string Id => "quickdock.media";
    public override string Name => "Media Controller";
    public override string Description => "System media session controls and metadata.";
    public override ModuleCategory Category => ModuleCategory.Utilities;

    public MediaSessionSnapshot? CurrentMedia => _mediaService.CurrentMedia;

    public event EventHandler<MediaSessionSnapshot?>? MediaChanged;

    public MediaModule(IMediaService mediaService, ILogger<MediaModule> logger) : base(logger)
    {
        _mediaService = mediaService;
        _mediaService.MediaChanged += (s, e) => MediaChanged?.Invoke(this, e);
    }

    public Task TogglePlayPauseAsync() => _mediaService.TogglePlayPauseAsync();
    public Task SkipNextAsync() => _mediaService.SkipNextAsync();
    public Task SkipPreviousAsync() => _mediaService.SkipPreviousAsync();
    public Task<MediaSessionSnapshot?> RefreshAsync() => _mediaService.RefreshAsync();

    protected override Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Media module initialized.");
        return Task.CompletedTask;
    }

    protected override Task OnStartAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Media module activated.");
        return Task.CompletedTask;
    }

    protected override Task OnStopAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Media module deactivated.");
        return Task.CompletedTask;
    }
}

