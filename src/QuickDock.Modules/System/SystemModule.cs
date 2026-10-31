using Microsoft.Extensions.Logging;
using QuickDock.Core.Modules;
using QuickDock.Services.System;

namespace QuickDock.Modules.System;

public sealed class SystemModule : ModuleBase
{
    private readonly ISystemMetricsService _metricsService;
    private PeriodicTimer? _periodicTimer;
    private CancellationTokenSource? _timerCts;
    private Task? _timerTask;

    public override string Id => "quickdock.system";
    public override string Name => "System Monitor";
    public override string Description => "Hardware metrics: CPU, Memory, GPU, Disk, Battery, and Network.";
    public override ModuleCategory Category => ModuleCategory.System;

    public SystemMetricsSnapshot? CurrentSnapshot { get; private set; }

    public SystemModule(ISystemMetricsService metricsService, ILogger<SystemModule> logger) : base(logger)
    {
        _metricsService = metricsService;
    }

    protected override Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        CurrentSnapshot = _metricsService.GetSystemSnapshot(detailed: false);
        Logger.LogInformation("System module initialized.");
        return Task.CompletedTask;
    }

    protected override Task OnStartAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("System module starting active metric polling.");
        CurrentSnapshot = _metricsService.GetSystemSnapshot(detailed: true);

        _timerCts = new CancellationTokenSource();
        _periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        _timerTask = PollMetricsAsync(_periodicTimer, _timerCts.Token);
        return Task.CompletedTask;
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("System module stopping metric polling to conserve resources.");
        if (_timerCts != null)
        {
            await _timerCts.CancelAsync().ConfigureAwait(false);
            if (_timerTask != null)
            {
                try
                {
                    await _timerTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected on stop
                }
            }
            _timerCts.Dispose();
            _timerCts = null;
        }

        _periodicTimer?.Dispose();
        _periodicTimer = null;
        _timerTask = null;
    }

    private async Task PollMetricsAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                CurrentSnapshot = _metricsService.GetSystemSnapshot(detailed: true);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on module stop
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                _timerCts?.Cancel();
                _periodicTimer?.Dispose();
                _timerCts?.Dispose();
            }
            catch { }
            _periodicTimer = null;
            _timerCts = null;
            _timerTask = null;
        }
        base.Dispose(disposing);
    }
}

