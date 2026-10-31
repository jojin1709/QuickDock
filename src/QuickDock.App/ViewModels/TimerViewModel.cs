using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.App.ViewModels;

public sealed partial class TimerViewModel : ObservableObject
{
    private readonly ITimerService _timerService;
    private readonly IStopwatchService _stopwatchService;
    private readonly IPomodoroService _pomodoroService;
    private readonly DispatcherQueue? _dispatcherQueue;

    [ObservableProperty]
    private int _selectedTabIndex; // 0 = Timers, 1 = Stopwatch, 2 = Pomodoro

    public bool IsTimersTabVisible => SelectedTabIndex == 0;
    public bool IsStopwatchTabVisible => SelectedTabIndex == 1;
    public bool IsPomodoroTabVisible => SelectedTabIndex == 2;

    partial void OnSelectedTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsTimersTabVisible));
        OnPropertyChanged(nameof(IsStopwatchTabVisible));
        OnPropertyChanged(nameof(IsPomodoroTabVisible));
    }

    [RelayCommand]
    public void SelectTab(int index)
    {
        SelectedTabIndex = index;
    }

    // Stopwatch properties
    [ObservableProperty]
    private string _stopwatchDisplay = "00:00:00.00";

    [ObservableProperty]
    private bool _isStopwatchRunning;

    public ObservableCollection<LapItem> StopwatchLaps { get; } = new();

    // Pomodoro properties
    [ObservableProperty]
    private string _pomodoroDisplay = "25:00";

    [ObservableProperty]
    private string _pomodoroPhaseText = "Work";

    [ObservableProperty]
    private string _pomodoroSessionText = "Session 1 / 4";

    [ObservableProperty]
    private bool _isPomodoroRunning;

    // Timers list
    public ObservableCollection<TimerItem> Timers { get; } = new();

    [ObservableProperty]
    private string _newTimerName = "Quick Timer";

    [ObservableProperty]
    private int _newTimerMinutes = 15;

    public TimerViewModel(
        ITimerService timerService,
        IStopwatchService stopwatchService,
        IPomodoroService pomodoroService,
        DispatcherQueue? dispatcherQueue = null)
    {
        _timerService = timerService ?? throw new ArgumentNullException(nameof(timerService));
        _stopwatchService = stopwatchService ?? throw new ArgumentNullException(nameof(stopwatchService));
        _pomodoroService = pomodoroService ?? throw new ArgumentNullException(nameof(pomodoroService));
        _dispatcherQueue = dispatcherQueue ?? DispatcherQueue.GetForCurrentThread();

        _timerService.TimerUpdated += OnTimerUpdated;
        _timerService.TimerCompleted += OnTimerCompleted;

        _stopwatchService.Tick += OnStopwatchTick;
        _stopwatchService.LapRecorded += OnStopwatchLap;

        _pomodoroService.Tick += OnPomodoroTick;
        _pomodoroService.PhaseChanged += OnPomodoroPhaseChanged;

        UpdatePomodoroUI();
        RefreshTimersList();
    }

    private void Dispatch(Action action)
    {
        if (_dispatcherQueue != null)
        {
            _dispatcherQueue.TryEnqueue(() => action());
        }
        else
        {
            action();
        }
    }

    public void RefreshTimersList()
    {
        Dispatch(() =>
        {
            Timers.Clear();
            foreach (var t in _timerService.ActiveTimers)
            {
                Timers.Add(t);
            }
        });
    }

    #region Timers Commands

    [RelayCommand]
    private void CreateTimerPreset(int minutes)
    {
        var timer = _timerService.CreateTimer($"{minutes} Min Timer", TimeSpan.FromMinutes(minutes));
        _timerService.StartTimer(timer.Id);
        RefreshTimersList();
    }

    [RelayCommand]
    private void CreateCustomTimer()
    {
        if (NewTimerMinutes <= 0) return;
        var timer = _timerService.CreateTimer(NewTimerName, TimeSpan.FromMinutes(NewTimerMinutes));
        _timerService.StartTimer(timer.Id);
        RefreshTimersList();
    }

    [RelayCommand]
    private void StartTimer(string id)
    {
        _timerService.StartTimer(id);
        RefreshTimersList();
    }

    [RelayCommand]
    private void PauseTimer(string id)
    {
        _timerService.PauseTimer(id);
        RefreshTimersList();
    }

    [RelayCommand]
    private void ResetTimer(string id)
    {
        _timerService.ResetTimer(id);
        RefreshTimersList();
    }

    [RelayCommand]
    private void CancelTimer(string id)
    {
        _timerService.CancelTimer(id);
        RefreshTimersList();
    }

    private void OnTimerUpdated(object? sender, TimerItem e)
    {
        Dispatch(() =>
        {
            var existing = Timers.FirstOrDefault(t => t.Id == e.Id);
            if (existing != null)
            {
                var index = Timers.IndexOf(existing);
                Timers[index] = e;
            }
            else
            {
                RefreshTimersList();
            }
        });
    }

    private void OnTimerCompleted(object? sender, TimerItem e)
    {
        RefreshTimersList();
    }

    #endregion

    #region Stopwatch Commands

    [RelayCommand]
    private void StartStopwatch()
    {
        _stopwatchService.Start();
        IsStopwatchRunning = true;
    }

    [RelayCommand]
    private void PauseStopwatch()
    {
        _stopwatchService.Pause();
        IsStopwatchRunning = false;
    }

    [RelayCommand]
    private void ResetStopwatch()
    {
        _stopwatchService.Reset();
        IsStopwatchRunning = false;
        StopwatchDisplay = "00:00:00.00";
        StopwatchLaps.Clear();
    }

    [RelayCommand]
    private void LapStopwatch()
    {
        _stopwatchService.RecordLap();
    }

    private void OnStopwatchTick(object? sender, TimeSpan elapsed)
    {
        Dispatch(() =>
        {
            StopwatchDisplay = elapsed.ToString(@"hh\:mm\:ss\.ff");
        });
    }

    private void OnStopwatchLap(object? sender, LapItem lap)
    {
        Dispatch(() =>
        {
            StopwatchLaps.Insert(0, lap);
        });
    }

    #endregion

    #region Pomodoro Commands

    [RelayCommand]
    private void StartPomodoro()
    {
        _pomodoroService.Start();
        IsPomodoroRunning = true;
    }

    [RelayCommand]
    private void PausePomodoro()
    {
        _pomodoroService.Pause();
        IsPomodoroRunning = false;
    }

    [RelayCommand]
    private void ResetPomodoro()
    {
        _pomodoroService.Reset();
        IsPomodoroRunning = false;
        UpdatePomodoroUI();
    }

    [RelayCommand]
    private void SkipPomodoro()
    {
        _pomodoroService.SkipToNextPhase();
        UpdatePomodoroUI();
    }

    private void OnPomodoroTick(object? sender, TimeSpan remaining)
    {
        Dispatch(() =>
        {
            PomodoroDisplay = remaining.ToString(@"mm\:ss");
        });
    }

    private void OnPomodoroPhaseChanged(object? sender, EventArgs e)
    {
        Dispatch(() =>
        {
            UpdatePomodoroUI();
        });
    }

    private void UpdatePomodoroUI()
    {
        PomodoroPhaseText = _pomodoroService.CurrentPhase switch
        {
            PomodoroPhase.Work => "Work Focus",
            PomodoroPhase.ShortBreak => "Short Break",
            PomodoroPhase.LongBreak => "Long Break",
            _ => "Focus"
        };
        PomodoroSessionText = $"Session {_pomodoroService.CurrentSession} / {_pomodoroService.TotalSessionsBeforeLongBreak}";
        PomodoroDisplay = _pomodoroService.RemainingTime.ToString(@"mm\:ss");
        IsPomodoroRunning = _pomodoroService.IsRunning;
    }

    #endregion
}
