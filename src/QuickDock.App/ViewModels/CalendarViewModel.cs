using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.App.ViewModels;

public sealed partial class CalendarViewModel : ObservableObject
{
    private readonly ICalendarService _calendarService;
    private readonly DispatcherQueue? _dispatcherQueue;

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    public string FormattedSelectedDate => SelectedDate.ToString("dddd, MMMM d, yyyy");

    partial void OnSelectedDateChanged(DateTime value)
    {
        OnPropertyChanged(nameof(FormattedSelectedDate));
    }

    [ObservableProperty]
    private string _currentMonthYearText = string.Empty;

    [ObservableProperty]
    private string _newEventTitle = string.Empty;

    [ObservableProperty]
    private string _newEventDescription = string.Empty;

    [ObservableProperty]
    private TimeSpan _newEventStartTime = TimeSpan.FromHours(10);

    [ObservableProperty]
    private TimeSpan _newEventEndTime = TimeSpan.FromHours(11);

    public ObservableCollection<CalendarEventItem> SelectedDateEvents { get; } = new();

    public CalendarViewModel(
        ICalendarService calendarService,
        DispatcherQueue? dispatcherQueue = null)
    {
        _calendarService = calendarService ?? throw new ArgumentNullException(nameof(calendarService));
        _dispatcherQueue = dispatcherQueue ?? DispatcherQueue.GetForCurrentThread();

        UpdateMonthYearText();
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

    private void UpdateMonthYearText()
    {
        CurrentMonthYearText = SelectedDate.ToString("MMMM yyyy");
    }

    public async Task LoadEventsForSelectedDateAsync()
    {
        try
        {
            var events = await _calendarService.GetEventsForDateAsync(SelectedDate);
            Dispatch(() =>
            {
                SelectedDateEvents.Clear();
                foreach (var ev in events)
                {
                    SelectedDateEvents.Add(ev);
                }
            });
        }
        catch
        {
            // Handled safely
        }
    }

    [RelayCommand]
    private async Task PreviousMonthAsync()
    {
        SelectedDate = SelectedDate.AddMonths(-1);
        UpdateMonthYearText();
        await LoadEventsForSelectedDateAsync();
    }

    [RelayCommand]
    private async Task NextMonthAsync()
    {
        SelectedDate = SelectedDate.AddMonths(1);
        UpdateMonthYearText();
        await LoadEventsForSelectedDateAsync();
    }

    [RelayCommand]
    private async Task TodayAsync()
    {
        SelectedDate = DateTime.Today;
        UpdateMonthYearText();
        await LoadEventsForSelectedDateAsync();
    }

    [RelayCommand]
    private async Task AddEventAsync()
    {
        if (string.IsNullOrWhiteSpace(NewEventTitle)) return;

        var ev = new CalendarEventItem
        {
            Title = NewEventTitle.Trim(),
            Description = NewEventDescription?.Trim() ?? string.Empty,
            EventDate = SelectedDate.Date,
            StartTime = NewEventStartTime,
            EndTime = NewEventEndTime
        };

        await _calendarService.SaveEventAsync(ev);
        NewEventTitle = string.Empty;
        NewEventDescription = string.Empty;
        await LoadEventsForSelectedDateAsync();
    }

    [RelayCommand]
    private async Task DeleteEventAsync(CalendarEventItem ev)
    {
        if (ev == null) return;
        await _calendarService.DeleteEventAsync(ev.Id);
        await LoadEventsForSelectedDateAsync();
    }
}
