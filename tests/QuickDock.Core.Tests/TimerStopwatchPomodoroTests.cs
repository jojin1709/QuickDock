using Microsoft.Extensions.Logging.Abstractions;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;
using QuickDock.Core.Settings;
using QuickDock.Services.Time;
using Xunit;

namespace QuickDock.Core.Tests;

public class MockNotificationService : INotificationService
{
    public List<(string Title, string Message)> Notifications { get; } = new();

    public void ShowNotification(string title, string message, string? action = null)
    {
        Notifications.Add((title, message));
    }
}

public class MockSettingsManager : ISettingsManager
{
    public QuickDockSettings CurrentSettings { get; set; } = new();

    public Task<QuickDockSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(CurrentSettings);
    public Task SaveAsync(QuickDockSettings settings, string? changedSection = null, CancellationToken cancellationToken = default)
    {
        CurrentSettings = settings;
        return Task.CompletedTask;
    }
    public Task ResetToDefaultsAsync(CancellationToken cancellationToken = default)
    {
        CurrentSettings = new QuickDockSettings();
        return Task.CompletedTask;
    }
}

public class TimerStopwatchPomodoroTests
{
    [Fact]
    public void TimerService_SupportsMultipleTimers_AndLifecycle()
    {
        var notif = new MockNotificationService();
        var settings = new MockSettingsManager();
        using var timerService = new TimerService(notif, settings, NullLogger<TimerService>.Instance);

        var t1 = timerService.CreateTimer("Study", TimeSpan.FromMinutes(25));
        var t2 = timerService.CreateTimer("Break", TimeSpan.FromMinutes(5));

        Assert.Equal(2, timerService.ActiveTimers.Count);
        Assert.Equal(TimerState.Stopped, t1.State);
        Assert.Equal(TimerState.Stopped, t2.State);

        // Start
        timerService.StartTimer(t1.Id);
        var active1 = timerService.ActiveTimers.First(t => t.Id == t1.Id);
        Assert.Equal(TimerState.Running, active1.State);

        // Pause
        timerService.PauseTimer(t1.Id);
        var paused1 = timerService.ActiveTimers.First(t => t.Id == t1.Id);
        Assert.Equal(TimerState.Paused, paused1.State);

        // Reset
        timerService.ResetTimer(t1.Id);
        var reset1 = timerService.ActiveTimers.First(t => t.Id == t1.Id);
        Assert.Equal(TimerState.Stopped, reset1.State);
        Assert.Equal(TimeSpan.FromMinutes(25), reset1.RemainingTime);

        // Cancel
        timerService.CancelTimer(t2.Id);
        Assert.Single(timerService.ActiveTimers);
    }

    [Fact]
    public async Task StopwatchService_Start_Pause_Lap_Reset_Works()
    {
        using var stopwatch = new StopwatchService();

        Assert.False(stopwatch.IsRunning);
        Assert.Equal(TimeSpan.Zero, stopwatch.ElapsedTime);

        stopwatch.Start();
        Assert.True(stopwatch.IsRunning);

        await Task.Delay(60);

        Assert.True(stopwatch.ElapsedTime > TimeSpan.Zero);

        var lap1 = stopwatch.RecordLap();
        Assert.NotNull(lap1);
        Assert.Equal(1, lap1.LapNumber);
        Assert.True(lap1.LapTime > TimeSpan.Zero);

        stopwatch.Pause();
        Assert.False(stopwatch.IsRunning);

        var pausedElapsed = stopwatch.ElapsedTime;
        await Task.Delay(20);
        Assert.Equal(pausedElapsed, stopwatch.ElapsedTime);

        stopwatch.Reset();
        Assert.Equal(TimeSpan.Zero, stopwatch.ElapsedTime);
        Assert.Empty(stopwatch.Laps);
    }

    [Fact]
    public void PomodoroService_PhaseTransitions_AndSessions()
    {
        var notif = new MockNotificationService();
        var settings = new MockSettingsManager();
        settings.CurrentSettings.Timer.PomodoroWorkMinutes = 25;
        settings.CurrentSettings.Timer.PomodoroShortBreakMinutes = 5;
        settings.CurrentSettings.Timer.PomodoroLongBreakMinutes = 15;
        settings.CurrentSettings.Timer.PomodoroSessionsBeforeLongBreak = 2;

        using var pomodoro = new PomodoroService(notif, settings, NullLogger<PomodoroService>.Instance);

        Assert.Equal(PomodoroPhase.Work, pomodoro.CurrentPhase);
        Assert.Equal(1, pomodoro.CurrentSession);
        Assert.Equal(TimeSpan.FromMinutes(25), pomodoro.RemainingTime);

        // Complete Session 1 Work -> Short Break
        pomodoro.SkipToNextPhase();
        Assert.Equal(PomodoroPhase.ShortBreak, pomodoro.CurrentPhase);
        Assert.Equal(TimeSpan.FromMinutes(5), pomodoro.RemainingTime);
        Assert.Contains(notif.Notifications, n => n.Title.Contains("Work Session Complete"));

        // Complete Short Break -> Session 2 Work
        pomodoro.SkipToNextPhase();
        Assert.Equal(PomodoroPhase.Work, pomodoro.CurrentPhase);
        Assert.Equal(2, pomodoro.CurrentSession);

        // Complete Session 2 Work (reaches 2 sessions threshold) -> Long Break
        pomodoro.SkipToNextPhase();
        Assert.Equal(PomodoroPhase.LongBreak, pomodoro.CurrentPhase);
        Assert.Equal(TimeSpan.FromMinutes(15), pomodoro.RemainingTime);

        // Complete Long Break -> Cycle resets to Session 1 Work
        pomodoro.SkipToNextPhase();
        Assert.Equal(PomodoroPhase.Work, pomodoro.CurrentPhase);
        Assert.Equal(1, pomodoro.CurrentSession);
    }
}
