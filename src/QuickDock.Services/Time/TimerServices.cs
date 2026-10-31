using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;
using QuickDock.Core.Settings;

namespace QuickDock.Services.Time;

public sealed class TimerService : ITimerService
{
    private readonly INotificationService _notificationService;
    private readonly ISettingsManager _settingsManager;
    private readonly ILogger<TimerService> _logger;
    private readonly List<TimerItem> _timers = new();
    private readonly object _lock = new();
    private global::System.Threading.Timer? _ticker;

    public event EventHandler<TimerItem>? TimerUpdated;
    public event EventHandler<TimerItem>? TimerCompleted;

    public IReadOnlyList<TimerItem> ActiveTimers
    {
        get
        {
            lock (_lock)
            {
                return _timers.ToList();
            }
        }
    }

    public TimerService(
        INotificationService notificationService,
        ISettingsManager settingsManager,
        ILogger<TimerService> logger)
    {
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public TimerItem CreateTimer(string name, TimeSpan duration)
    {
        lock (_lock)
        {
            var timer = new TimerItem
            {
                Name = string.IsNullOrWhiteSpace(name) ? "Timer" : name,
                TotalDuration = duration,
                RemainingTime = duration,
                State = TimerState.Stopped
            };
            _timers.Add(timer);
            return timer;
        }
    }

    public void StartTimer(string id)
    {
        lock (_lock)
        {
            var timer = _timers.FirstOrDefault(t => t.Id == id);
            if (timer == null) return;

            timer.State = TimerState.Running;
            EnsureTicker();
        }
    }

    public void PauseTimer(string id)
    {
        lock (_lock)
        {
            var timer = _timers.FirstOrDefault(t => t.Id == id);
            if (timer == null) return;

            timer.State = TimerState.Paused;
            CheckTickerStatus();
        }
    }

    public void ResumeTimer(string id)
    {
        StartTimer(id);
    }

    public void ResetTimer(string id)
    {
        lock (_lock)
        {
            var timer = _timers.FirstOrDefault(t => t.Id == id);
            if (timer == null) return;

            timer.RemainingTime = timer.TotalDuration;
            timer.State = TimerState.Stopped;
            CheckTickerStatus();
            TimerUpdated?.Invoke(this, timer);
        }
    }

    public void CancelTimer(string id)
    {
        lock (_lock)
        {
            var timer = _timers.FirstOrDefault(t => t.Id == id);
            if (timer != null)
            {
                _timers.Remove(timer);
                CheckTickerStatus();
            }
        }
    }

    private void EnsureTicker()
    {
        _ticker ??= new global::System.Threading.Timer(OnTick, null, 1000, 1000);
    }

    private void CheckTickerStatus()
    {
        if (_timers.All(t => t.State != TimerState.Running))
        {
            _ticker?.Dispose();
            _ticker = null;
        }
    }

    private void OnTick(object? state)
    {
        List<TimerItem> completed = new();
        List<TimerItem> updated = new();

        lock (_lock)
        {
            foreach (var timer in _timers.Where(t => t.State == TimerState.Running))
            {
                timer.RemainingTime = timer.RemainingTime.Subtract(TimeSpan.FromSeconds(1));
                if (timer.RemainingTime <= TimeSpan.Zero)
                {
                    timer.RemainingTime = TimeSpan.Zero;
                    timer.State = TimerState.Completed;
                    completed.Add(timer);
                }
                else
                {
                    updated.Add(timer);
                }
            }
            CheckTickerStatus();
        }

        foreach (var timer in updated)
        {
            TimerUpdated?.Invoke(this, timer);
        }

        foreach (var timer in completed)
        {
            TimerCompleted?.Invoke(this, timer);
            TimerUpdated?.Invoke(this, timer);
            NotifyTimerCompleted(timer);
        }
    }

    private void NotifyTimerCompleted(TimerItem timer)
    {
        try
        {
            _notificationService.ShowNotification("Timer Finished", $"\"{timer.Name}\" timer has completed.");
            if (_settingsManager.CurrentSettings.Timer.PlaySoundOnComplete)
            {
                NativeAudioHelper.PlayExclamation();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deliver timer completion notification.");
        }
    }

    public void Dispose()
    {
        _ticker?.Dispose();
        _ticker = null;
        lock (_lock)
        {
            _timers.Clear();
        }
    }
}

public sealed class StopwatchService : IStopwatchService
{
    private readonly Stopwatch _stopwatch = new();
    private readonly List<LapItem> _laps = new();
    private readonly object _lock = new();
    private global::System.Threading.Timer? _ticker;
    private TimeSpan _lastLapTime = TimeSpan.Zero;

    public event EventHandler<TimeSpan>? Tick;
    public event EventHandler<LapItem>? LapRecorded;

    public bool IsRunning => _stopwatch.IsRunning;
    public TimeSpan ElapsedTime => _stopwatch.Elapsed;

    public IReadOnlyList<LapItem> Laps
    {
        get
        {
            lock (_lock)
            {
                return _laps.ToList();
            }
        }
    }

    public void Start()
    {
        lock (_lock)
        {
            if (!_stopwatch.IsRunning)
            {
                _stopwatch.Start();
                _ticker ??= new global::System.Threading.Timer(OnTick, null, 50, 50);
            }
        }
    }

    public void Pause()
    {
        lock (_lock)
        {
            if (_stopwatch.IsRunning)
            {
                _stopwatch.Stop();
                _ticker?.Dispose();
                _ticker = null;
            }
        }
    }

    public void Resume() => Start();

    public void Reset()
    {
        lock (_lock)
        {
            _stopwatch.Reset();
            _ticker?.Dispose();
            _ticker = null;
            _laps.Clear();
            _lastLapTime = TimeSpan.Zero;
            Tick?.Invoke(this, TimeSpan.Zero);
        }
    }

    public LapItem? RecordLap()
    {
        lock (_lock)
        {
            if (!_stopwatch.IsRunning && _stopwatch.Elapsed == TimeSpan.Zero) return null;

            var currentElapsed = _stopwatch.Elapsed;
            var lapDuration = currentElapsed - _lastLapTime;
            _lastLapTime = currentElapsed;

            var lap = new LapItem
            {
                LapNumber = _laps.Count + 1,
                LapTime = lapDuration,
                SplitTime = currentElapsed
            };
            _laps.Add(lap);
            LapRecorded?.Invoke(this, lap);
            return lap;
        }
    }

    private void OnTick(object? state)
    {
        if (_stopwatch.IsRunning)
        {
            Tick?.Invoke(this, _stopwatch.Elapsed);
        }
    }

    public void Dispose()
    {
        _stopwatch.Stop();
        _ticker?.Dispose();
        _ticker = null;
    }
}

public sealed class PomodoroService : IPomodoroService
{
    private readonly INotificationService _notificationService;
    private readonly ISettingsManager _settingsManager;
    private readonly ILogger<PomodoroService> _logger;
    private readonly object _lock = new();

    private global::System.Threading.Timer? _ticker;
    private TimeSpan _remainingTime;
    private PomodoroPhase _currentPhase = PomodoroPhase.Work;
    private int _currentSession = 1;
    private bool _isRunning;

    public event EventHandler? PhaseChanged;
    public event EventHandler<TimeSpan>? Tick;

    public PomodoroPhase CurrentPhase => _currentPhase;
    public int CurrentSession => _currentSession;
    public int TotalSessionsBeforeLongBreak => _settingsManager.CurrentSettings.Timer.PomodoroSessionsBeforeLongBreak;
    public TimeSpan RemainingTime => _remainingTime;
    public bool IsRunning => _isRunning;

    public PomodoroService(
        INotificationService notificationService,
        ISettingsManager settingsManager,
        ILogger<PomodoroService> logger)
    {
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _remainingTime = TimeSpan.FromMinutes(_settingsManager.CurrentSettings.Timer.PomodoroWorkMinutes);
    }

    public void Start()
    {
        lock (_lock)
        {
            if (_isRunning) return;
            _isRunning = true;
            _ticker ??= new global::System.Threading.Timer(OnTick, null, 1000, 1000);
        }
    }

    public void Pause()
    {
        lock (_lock)
        {
            _isRunning = false;
            _ticker?.Dispose();
            _ticker = null;
        }
    }

    public void Resume() => Start();

    public void Reset()
    {
        lock (_lock)
        {
            Pause();
            _currentPhase = PomodoroPhase.Work;
            _currentSession = 1;
            _remainingTime = TimeSpan.FromMinutes(_settingsManager.CurrentSettings.Timer.PomodoroWorkMinutes);
            PhaseChanged?.Invoke(this, EventArgs.Empty);
            Tick?.Invoke(this, _remainingTime);
        }
    }

    public void SkipToNextPhase()
    {
        lock (_lock)
        {
            TransitionToNextPhase();
        }
    }

    private void OnTick(object? state)
    {
        lock (_lock)
        {
            if (!_isRunning) return;

            _remainingTime = _remainingTime.Subtract(TimeSpan.FromSeconds(1));
            if (_remainingTime <= TimeSpan.Zero)
            {
                _remainingTime = TimeSpan.Zero;
                Tick?.Invoke(this, _remainingTime);
                TransitionToNextPhase();
            }
            else
            {
                Tick?.Invoke(this, _remainingTime);
            }
        }
    }

    private void TransitionToNextPhase()
    {
        var settings = _settingsManager.CurrentSettings.Timer;
        string notificationTitle;
        string notificationBody;

        if (_currentPhase == PomodoroPhase.Work)
        {
            if (_currentSession >= settings.PomodoroSessionsBeforeLongBreak)
            {
                _currentPhase = PomodoroPhase.LongBreak;
                _remainingTime = TimeSpan.FromMinutes(settings.PomodoroLongBreakMinutes);
                notificationTitle = "Pomodoro: Work Session Complete";
                notificationBody = $"Great work! Take a long break ({settings.PomodoroLongBreakMinutes} mins).";
            }
            else
            {
                _currentPhase = PomodoroPhase.ShortBreak;
                _remainingTime = TimeSpan.FromMinutes(settings.PomodoroShortBreakMinutes);
                notificationTitle = "Pomodoro: Work Session Complete";
                notificationBody = $"Time for a quick break ({settings.PomodoroShortBreakMinutes} mins).";
            }

            if (!settings.PomodoroAutoStartBreaks)
            {
                Pause();
            }
        }
        else
        {
            if (_currentPhase == PomodoroPhase.LongBreak)
            {
                _currentSession = 1;
            }
            else
            {
                _currentSession++;
            }

            _currentPhase = PomodoroPhase.Work;
            _remainingTime = TimeSpan.FromMinutes(settings.PomodoroWorkMinutes);
            notificationTitle = "Pomodoro: Break Finished";
            notificationBody = $"Break is over. Ready for Session {_currentSession}?";

            if (!settings.PomodoroAutoStartWork)
            {
                Pause();
            }
        }

        try
        {
            _notificationService.ShowNotification(notificationTitle, notificationBody);
            if (settings.PlaySoundOnComplete)
            {
                NativeAudioHelper.PlayAsterisk();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send Pomodoro notification.");
        }

        PhaseChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _ticker?.Dispose();
        _ticker = null;
    }
}

internal static class NativeAudioHelper
{
    [DllImport("user32.dll")]
    private static extern bool MessageBeep(uint uType);

    public static void PlayExclamation() => MessageBeep(0x00000030); // MB_ICONEXCLAMATION
    public static void PlayAsterisk() => MessageBeep(0x00000040); // MB_ICONASTERISK
}
