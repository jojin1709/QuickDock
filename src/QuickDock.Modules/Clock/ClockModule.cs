using Microsoft.Extensions.Logging;
using QuickDock.Core.Events;
using QuickDock.Core.Modules;

namespace QuickDock.Modules.Clock;

/// <summary>
/// Lightweight clock module providing time, date, and timezone services.
/// Adheres to "Core stays alive. Modules do not" by only ticking when started/active.
/// </summary>
public sealed class ClockModule : ModuleBase
{
    private readonly IEventBus _eventBus;
    private PeriodicTimer? _periodicTimer;
    private CancellationTokenSource? _timerCts;
    private Task? _timerTask;

    public override string Id => "quickdock.clock";
    public override string Name => "Clock";
    public override string Description => "System time, date, and timezone services.";
    public override ModuleCategory Category => ModuleCategory.Utilities;

    public DateTime CurrentTime { get; private set; } = DateTime.Now;
    public TimeZoneInfo TimeZone => TimeZoneInfo.Local;

    public ClockModule(IEventBus eventBus, ILogger<ClockModule> logger) : base(logger)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    protected override Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        CurrentTime = DateTime.Now;
        Logger.LogInformation("Clock module initialized. TimeZone: {TimeZone}", TimeZone.DisplayName);
        return Task.CompletedTask;
    }

    protected override Task OnStartAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Clock module starting active time updates.");
        _timerCts = new CancellationTokenSource();
        _periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        _timerTask = RunTimerLoopAsync(_periodicTimer, _timerCts.Token);
        return Task.CompletedTask;
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Clock module stopping timer to conserve idle resources.");
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
                    // Expected when canceling timer loop
                }
            }
            _timerCts.Dispose();
            _timerCts = null;
        }

        _periodicTimer?.Dispose();
        _periodicTimer = null;
        _timerTask = null;
    }

    private async Task RunTimerLoopAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                CurrentTime = DateTime.Now;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on module stop
        }
    }
}
