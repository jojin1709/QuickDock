using Microsoft.Extensions.Logging;
using QuickDock.Core.Modules;
using QuickDock.Services.System;

namespace QuickDock.Modules.Battery;

public sealed class BatteryModule : ModuleBase
{
    private readonly ISystemMetricsService _metricsService;

    public override string Id => "quickdock.battery";
    public override string Name => "Battery Monitor";
    public override string Description => "System battery level and power state.";
    public override ModuleCategory Category => ModuleCategory.System;

    public BatteryMetrics? CurrentBattery => _metricsService.GetBatteryMetrics();

    public BatteryModule(ISystemMetricsService metricsService, ILogger<BatteryModule> logger) : base(logger)
    {
        _metricsService = metricsService;
    }

    protected override Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Battery module initialized.");
        return Task.CompletedTask;
    }

    protected override Task OnStartAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Battery module activated.");
        return Task.CompletedTask;
    }

    protected override Task OnStopAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Battery module deactivated.");
        return Task.CompletedTask;
    }
}

