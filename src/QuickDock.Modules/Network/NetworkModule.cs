using Microsoft.Extensions.Logging;
using QuickDock.Core.Modules;
using QuickDock.Services.System;

namespace QuickDock.Modules.Network;

public sealed class NetworkModule : ModuleBase
{
    private readonly ISystemMetricsService _metricsService;

    public override string Id => "quickdock.network";
    public override string Name => "Network Status";
    public override string Description => "Network connectivity and active interface status.";
    public override ModuleCategory Category => ModuleCategory.Utilities;

    public NetworkMetrics? CurrentNetwork => _metricsService.GetNetworkMetrics();

    public NetworkModule(ISystemMetricsService metricsService, ILogger<NetworkModule> logger) : base(logger)
    {
        _metricsService = metricsService;
    }

    protected override Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Network module initialized.");
        return Task.CompletedTask;
    }

    protected override Task OnStartAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Network module activated.");
        return Task.CompletedTask;
    }

    protected override Task OnStopAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Network module deactivated.");
        return Task.CompletedTask;
    }
}

