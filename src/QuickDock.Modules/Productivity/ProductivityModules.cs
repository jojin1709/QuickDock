using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;
using QuickDock.Core.Modules;

namespace QuickDock.Modules.Productivity;

public sealed class ClipboardModule : ModuleBase
{
    private readonly IClipboardService _clipboardService;

    public override string Id => "quickdock.clipboard";
    public override string Name => "Clipboard Manager";
    public override string Description => "Local clipboard history with search, pinning, and sensitive data protection.";
    public override ModuleCategory Category => ModuleCategory.Productivity;

    public ClipboardModule(IClipboardService clipboardService, ILogger<ClipboardModule> logger)
        : base(logger)
    {
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
    }

    protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        await _clipboardService.StartListeningAsync().ConfigureAwait(false);
    }

    protected override Task OnStartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    protected override Task OnStopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

public sealed class TimerModule : ModuleBase
{
    private readonly ITimerService _timerService;
    private readonly IStopwatchService _stopwatchService;
    private readonly IPomodoroService _pomodoroService;

    public override string Id => "quickdock.timer";
    public override string Name => "Timer & Stopwatch";
    public override string Description => "Countdown timers, precision stopwatch with laps, and Pomodoro focus sessions.";
    public override ModuleCategory Category => ModuleCategory.Productivity;

    public TimerModule(
        ITimerService timerService,
        IStopwatchService stopwatchService,
        IPomodoroService pomodoroService,
        ILogger<TimerModule> logger)
        : base(logger)
    {
        _timerService = timerService ?? throw new ArgumentNullException(nameof(timerService));
        _stopwatchService = stopwatchService ?? throw new ArgumentNullException(nameof(stopwatchService));
        _pomodoroService = pomodoroService ?? throw new ArgumentNullException(nameof(pomodoroService));
    }
}

public sealed class NotesModule : ModuleBase
{
    private readonly INotesService _notesService;

    public override string Id => "quickdock.notes";
    public override string Name => "Notes";
    public override string Description => "Local-first scratchpad and pinned notes with instant search.";
    public override ModuleCategory Category => ModuleCategory.Productivity;

    public NotesModule(INotesService notesService, ILogger<NotesModule> logger)
        : base(logger)
    {
        _notesService = notesService ?? throw new ArgumentNullException(nameof(notesService));
    }
}

public sealed class CalendarModule : ModuleBase
{
    private readonly ICalendarService _calendarService;

    public override string Id => "quickdock.calendar";
    public override string Name => "Calendar";
    public override string Description => "Local agenda and schedule viewer.";
    public override ModuleCategory Category => ModuleCategory.Productivity;

    public CalendarModule(ICalendarService calendarService, ILogger<CalendarModule> logger)
        : base(logger)
    {
        _calendarService = calendarService ?? throw new ArgumentNullException(nameof(calendarService));
    }
}

public sealed class WeatherModule : ModuleBase
{
    private readonly IWeatherService _weatherService;

    public override string Id => "quickdock.weather";
    public override string Name => "Weather";
    public override string Description => "Current atmospheric conditions and cached forecasts.";
    public override ModuleCategory Category => ModuleCategory.Productivity;

    public WeatherModule(IWeatherService weatherService, ILogger<WeatherModule> logger)
        : base(logger)
    {
        _weatherService = weatherService ?? throw new ArgumentNullException(nameof(weatherService));
    }

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        await _weatherService.GetWeatherAsync(forceRefresh: false).ConfigureAwait(false);
    }
}

public sealed class LinksModule : ModuleBase
{
    private readonly ISavedLinksService _linksService;

    public override string Id => "quickdock.links";
    public override string Name => "Saved Links";
    public override string Description => "Quick bookmarks and web launch shortcuts.";
    public override ModuleCategory Category => ModuleCategory.Productivity;

    public LinksModule(ISavedLinksService linksService, ILogger<LinksModule> logger)
        : base(logger)
    {
        _linksService = linksService ?? throw new ArgumentNullException(nameof(linksService));
    }
}

public sealed class TemporaryMessageModule : ModuleBase
{
    private readonly ITemporaryMessageService _tempMessageService;

    public override string Id => "quickdock.temporarymessage";
    public override string Name => "Temporary Message";
    public override string Description => "Quick time-expiring reminder banners.";
    public override ModuleCategory Category => ModuleCategory.Productivity;

    public TemporaryMessageModule(ITemporaryMessageService tempMessageService, ILogger<TemporaryMessageModule> logger)
        : base(logger)
    {
        _tempMessageService = tempMessageService ?? throw new ArgumentNullException(nameof(tempMessageService));
    }
}

public sealed class ScreenTimeModule : ModuleBase
{
    private readonly IScreenTimeService _screenTimeService;

    public override string Id => "quickdock.screentime";
    public override string Name => "Screen Time";
    public override string Description => "Local foreground application usage analytics.";
    public override ModuleCategory Category => ModuleCategory.Productivity;

    public ScreenTimeModule(IScreenTimeService screenTimeService, ILogger<ScreenTimeModule> logger)
        : base(logger)
    {
        _screenTimeService = screenTimeService ?? throw new ArgumentNullException(nameof(screenTimeService));
    }

    protected override Task OnStartAsync(CancellationToken cancellationToken)
    {
        _screenTimeService.StartTracking();
        return Task.CompletedTask;
    }

    protected override Task OnStopAsync(CancellationToken cancellationToken)
    {
        _screenTimeService.StopTracking();
        return Task.CompletedTask;
    }
}

