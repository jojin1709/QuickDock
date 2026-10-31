using Microsoft.Extensions.Logging;
using QuickDock.Core.Events;
using QuickDock.Core.Modules;
using QuickDock.Core.Settings;

namespace QuickDock.Modules.Clock;

public sealed class ClockModule : ModuleBase
{
    private readonly ISettingsManager? _settingsManager;
    private PeriodicTimer? _periodicTimer;
    private CancellationTokenSource? _timerCts;
    private Task? _timerTask;

    public override string Id => "quickdock.clock";
    public override string Name => "Clock";
    public override string Description => "System time, date, and timezone services.";
    public override ModuleCategory Category => ModuleCategory.Productivity;

    public DateTime CurrentTime { get; private set; } = DateTime.Now;
    public TimeZoneInfo TimeZone => GetConfiguredTimeZone();

    public ClockModule(ILogger<ClockModule> logger, ISettingsManager? settingsManager = null) : base(logger)
    {
        _settingsManager = settingsManager;
    }

    public TimeZoneInfo GetConfiguredTimeZone()
    {
        string? tzId = _settingsManager?.CurrentSettings.Clock.SelectedTimeZoneId;
        if (!string.IsNullOrWhiteSpace(tzId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(tzId);
            }
            catch
            {
                // Fall back to local timezone
            }
        }
        return TimeZoneInfo.Local;
    }

    public static string FormatTime(DateTime dateTime, bool use24Hour, bool showSeconds)
    {
        if (use24Hour)
        {
            return showSeconds ? dateTime.ToString("HH:mm:ss") : dateTime.ToString("HH:mm");
        }
        else
        {
            return showSeconds ? dateTime.ToString("h:mm:ss tt") : dateTime.ToString("h:mm tt");
        }
    }

    public static string FormatDate(DateTime dateTime)
    {
        return dateTime.ToString("dddd, MMMM d");
    }

    public string GetFormattedTime()
    {
        bool use24 = _settingsManager?.CurrentSettings.Clock.Use24HourFormat ?? true;
        bool seconds = _settingsManager?.CurrentSettings.Clock.ShowSeconds ?? false;
        var timeInZone = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZone);
        return FormatTime(timeInZone, use24, seconds);
    }

    public string GetFormattedDate()
    {
        var timeInZone = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZone);
        return FormatDate(timeInZone);
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

        bool showSeconds = _settingsManager?.CurrentSettings.Clock.ShowSeconds ?? false;
        var interval = showSeconds ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(1);

        _periodicTimer = new PeriodicTimer(interval);
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
