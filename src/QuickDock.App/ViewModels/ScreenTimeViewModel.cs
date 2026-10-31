using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;
using QuickDock.Core.Settings;

namespace QuickDock.App.ViewModels;

public sealed partial class ScreenTimeViewModel : ObservableObject
{
    private readonly IScreenTimeService _screenTimeService;
    private readonly ISettingsManager _settingsManager;
    private readonly DispatcherQueue? _dispatcherQueue;

    [ObservableProperty]
    private bool _isTrackingEnabled;

    [ObservableProperty]
    private bool _isWeeklyView;

    public string ViewModeButtonText => IsWeeklyView ? "Showing: 7 Days" : "Showing: Today";

    partial void OnIsWeeklyViewChanged(bool value)
    {
        OnPropertyChanged(nameof(ViewModeButtonText));
    }

    [ObservableProperty]
    private string _totalTimeText = "0m";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<AppUsageDisplayItem> AppUsages { get; } = new();

    public ScreenTimeViewModel(
        IScreenTimeService screenTimeService,
        ISettingsManager settingsManager,
        DispatcherQueue? dispatcherQueue = null)
    {
        _screenTimeService = screenTimeService ?? throw new ArgumentNullException(nameof(screenTimeService));
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _dispatcherQueue = dispatcherQueue ?? DispatcherQueue.GetForCurrentThread();

        _isTrackingEnabled = _settingsManager.CurrentSettings.ScreenTime.TrackingEnabled;
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

    public async Task LoadUsageAsync()
    {
        try
        {
            List<AppUsageRecord> records;
            if (IsWeeklyView)
            {
                var end = DateTime.Today;
                var start = end.AddDays(-7);
                records = await _screenTimeService.GetWeeklyUsageAsync(start, end);
            }
            else
            {
                records = await _screenTimeService.GetDailyUsageAsync(DateTime.Today);
            }

            var totalSeconds = records.Sum(r => r.DurationSeconds);
            Dispatch(() =>
            {
                TotalTimeText = FormatDuration(totalSeconds);
                AppUsages.Clear();
                foreach (var r in records)
                {
                    AppUsages.Add(new AppUsageDisplayItem
                    {
                        AppName = r.AppName,
                        WindowTitle = r.WindowTitle,
                        DurationText = FormatDuration(r.DurationSeconds),
                        Seconds = r.DurationSeconds
                    });
                }
            });
        }
        catch
        {
            StatusMessage = "Failed to load usage history.";
        }
    }

    [RelayCommand]
    private async Task ToggleTrackingAsync()
    {
        IsTrackingEnabled = !IsTrackingEnabled;
        var s = _settingsManager.CurrentSettings;
        s.ScreenTime.TrackingEnabled = IsTrackingEnabled;
        await _settingsManager.SaveAsync(s);

        if (IsTrackingEnabled)
        {
            _screenTimeService.StartTracking();
            StatusMessage = "Screen time tracking enabled.";
        }
        else
        {
            _screenTimeService.StopTracking();
            StatusMessage = "Screen time tracking paused.";
        }
    }

    [RelayCommand]
    private async Task ToggleViewModeAsync()
    {
        IsWeeklyView = !IsWeeklyView;
        await LoadUsageAsync();
    }

    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        await _screenTimeService.ClearHistoryAsync();
        StatusMessage = "Screen time history cleared.";
        await LoadUsageAsync();
    }

    private static string FormatDuration(long totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(totalSeconds);
        if (ts.TotalHours >= 1)
        {
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        }
        return $"{ts.Minutes}m {ts.Seconds}s";
    }
}

public sealed class AppUsageDisplayItem
{
    public string AppName { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
    public string DurationText { get; set; } = string.Empty;
    public long Seconds { get; set; }
}
