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
    private int _appsUsedCount;

    [ObservableProperty]
    private string _limitRemainingText = "7h 0m";

    [ObservableProperty]
    private string _weeklyAverageText = "<1m";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<AppUsageDisplayItem> AppUsages { get; } = new();
    public ObservableCollection<DaySparklineItem> WeekDays { get; } = new();

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
            const long dailyLimitSeconds = 7 * 3600; // 7h default limit
            var remainingSec = Math.Max(0, dailyLimitSeconds - totalSeconds);
            var remTs = TimeSpan.FromSeconds(remainingSec);
            var limitText = $"{(int)remTs.TotalHours}h {remTs.Minutes}m";

            Dispatch(() =>
            {
                TotalTimeText = FormatDuration(totalSeconds);
                LimitRemainingText = limitText;
                AppUsages.Clear();
                foreach (var r in records.OrderByDescending(x => x.DurationSeconds))
                {
                    var pct = totalSeconds > 0 ? (double)r.DurationSeconds / totalSeconds * 100.0 : 0.0;
                    AppUsages.Add(new AppUsageDisplayItem
                    {
                        AppName = r.AppName,
                        WindowTitle = r.WindowTitle,
                        DurationText = FormatDuration(r.DurationSeconds),
                        Seconds = r.DurationSeconds,
                        Percentage = Math.Round(pct, 0),
                        PercentageText = $"{Math.Round(pct, 0)}%",
                        IconGlyph = GetAppGlyph(r.AppName)
                    });
                }
                AppsUsedCount = AppUsages.Count;
                PopulateSparklines();
            });
        }
        catch
        {
            StatusMessage = "Failed to load usage history.";
        }
    }

    private void PopulateSparklines()
    {
        WeekDays.Clear();
        var today = DateTime.Today;
        // Monday through Sunday
        int currentDayOfWeek = (int)today.DayOfWeek;
        int diff = currentDayOfWeek == 0 ? 6 : currentDayOfWeek - 1; // 0 for Monday, 6 for Sunday
        var startOfWeek = today.AddDays(-diff);

        for (int i = 0; i < 7; i++)
        {
            var day = startOfWeek.AddDays(i);
            var isToday = day.Date == today.Date;
            WeekDays.Add(new DaySparklineItem
            {
                DayName = day.ToString("ddd"),
                BarHeight = isToday ? 32 : (i % 2 == 0 ? 14 : 8),
                IsToday = isToday
            });
        }
    }

    private static string GetAppGlyph(string appName)
    {
        var lower = appName.ToLowerInvariant();
        if (lower.Contains("chrome") || lower.Contains("browser") || lower.Contains("edge")) return "\uE774";
        if (lower.Contains("code") || lower.Contains("dev") || lower.Contains("git")) return "\uE943";
        if (lower.Contains("explorer")) return "\uED25";
        if (lower.Contains("terminal") || lower.Contains("powershell") || lower.Contains("cmd")) return "\uE756";
        if (lower.Contains("quickdock")) return "\uE80F";
        return "\uE71D";
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
    public double Percentage { get; set; }
    public string PercentageText { get; set; } = string.Empty;
    public string IconGlyph { get; set; } = "\uE71D";
}

public sealed class DaySparklineItem
{
    public string DayName { get; set; } = string.Empty;
    public double BarHeight { get; set; } = 4;
    public bool IsToday { get; set; }
    public string SparklineColor => IsToday ? "#FFFFFF" : "#3F3F46";
    public string DayTextColor => IsToday ? "#FFFFFF" : "#71717A";
}
