using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using QuickDock.Core.Events;

namespace QuickDock.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<MainViewModel> _logger;
    private DispatcherQueue? _dispatcherQueue;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private string _currentView = "Home"; // "Home", "System", "Settings", "Clipboard", "Timer", "Notes", "Calendar", "Weather", "Links", "ScreenTime"

    public HomeViewModel Home { get; }
    public SystemViewModel System { get; }
    public SettingsViewModel Settings { get; }
    public ClipboardViewModel Clipboard { get; }
    public TimerViewModel Timer { get; }
    public NotesViewModel Notes { get; }
    public CalendarViewModel Calendar { get; }
    public WeatherViewModel Weather { get; }
    public SavedLinksViewModel Links { get; }
    public ScreenTimeViewModel ScreenTime { get; }

    public event Action<bool>? DockExpansionStateChanged;

    public MainViewModel(
        HomeViewModel home,
        SystemViewModel system,
        SettingsViewModel settings,
        ClipboardViewModel clipboard,
        TimerViewModel timer,
        NotesViewModel notes,
        CalendarViewModel calendar,
        WeatherViewModel weather,
        SavedLinksViewModel links,
        ScreenTimeViewModel screenTime,
        IEventBus eventBus,
        ILogger<MainViewModel> logger)
    {
        Home = home;
        System = system;
        Settings = settings;
        Clipboard = clipboard;
        Timer = timer;
        Notes = notes;
        Calendar = calendar;
        Weather = weather;
        Links = links;
        ScreenTime = screenTime;
        _eventBus = eventBus;
        _logger = logger;

        Home.NavigationRequested += OnHomeNavigationRequested;
    }

    public void SetDispatcherQueue(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
    }

    private void OnHomeNavigationRequested(string targetView)
    {
        switch (targetView)
        {
            case "System":
                OpenSystem();
                break;
            case "Clipboard":
                OpenClipboard();
                break;
            case "Timer":
                OpenTimer();
                break;
            case "Notes":
                OpenNotes();
                break;
            case "Calendar":
                OpenCalendar();
                break;
            case "Weather":
                OpenWeather();
                break;
            case "Links":
                OpenLinks();
                break;
            case "ScreenTime":
                OpenScreenTime();
                break;
            default:
                OpenHome();
                break;
        }
    }

    [RelayCommand]
    public void ToggleExpand()
    {
        SetExpansionState(!IsExpanded, "UserToggle");
    }

    [RelayCommand]
    public void Expand()
    {
        if (!IsExpanded)
        {
            SetExpansionState(true, "HoverOrAction");
        }
    }

    [RelayCommand]
    public void Collapse()
    {
        if (IsExpanded)
        {
            SetExpansionState(false, "DismissAction");
        }
    }

    [RelayCommand]
    public void OpenSettings()
    {
        SwitchView("Settings");
        EnsureExpanded("OpenSettings");
    }

    [RelayCommand]
    public void OpenSystem()
    {
        SwitchView("System");
        EnsureExpanded("OpenSystem");
    }

    [RelayCommand]
    public void OpenClipboard()
    {
        SwitchView("Clipboard");
        _ = Clipboard.LoadHistoryAsync();
        EnsureExpanded("OpenClipboard");
    }

    [RelayCommand]
    public void OpenTimer()
    {
        SwitchView("Timer");
        Timer.RefreshTimersList();
        EnsureExpanded("OpenTimer");
    }

    [RelayCommand]
    public void OpenNotes()
    {
        SwitchView("Notes");
        _ = Notes.LoadNotesAsync();
        EnsureExpanded("OpenNotes");
    }

    [RelayCommand]
    public void OpenCalendar()
    {
        SwitchView("Calendar");
        _ = Calendar.LoadEventsForSelectedDateAsync();
        EnsureExpanded("OpenCalendar");
    }

    [RelayCommand]
    public void OpenWeather()
    {
        SwitchView("Weather");
        _ = Weather.RefreshWeatherAsync(force: false);
        EnsureExpanded("OpenWeather");
    }

    [RelayCommand]
    public void OpenLinks()
    {
        SwitchView("Links");
        _ = Links.LoadLinksAsync();
        EnsureExpanded("OpenLinks");
    }

    [RelayCommand]
    public void OpenScreenTime()
    {
        SwitchView("ScreenTime");
        _ = ScreenTime.LoadUsageAsync();
        EnsureExpanded("OpenScreenTime");
    }

    [RelayCommand]
    public void OpenHome()
    {
        SwitchView("Home");
        _ = Home.RefreshSummaryDataAsync();
    }

    private void EnsureExpanded(string trigger)
    {
        if (!IsExpanded)
        {
            SetExpansionState(true, trigger);
        }
    }

    private void SwitchView(string newView)
    {
        string oldView = CurrentView;
        CurrentView = newView;

        if (oldView == "System" && newView != "System")
        {
            System.StopLiveUpdates();
        }
        else if (newView == "System" && _dispatcherQueue != null)
        {
            System.StartLiveUpdates(_dispatcherQueue);
        }
    }

    public void SetExpansionState(bool expanded, string trigger)
    {
        if (IsExpanded == expanded) return;

        IsExpanded = expanded;
        _logger.LogInformation("Dock expansion state changed to {Expanded} (Trigger: {Trigger})", expanded, trigger);

        if (!expanded)
        {
            System.StopLiveUpdates();
            CurrentView = "Home"; // Return to home on collapse
        }
        else
        {
            _ = Home.RefreshSummaryDataAsync();
        }

        _eventBus.PublishAsync(new AppVisibilityChangedEvent(expanded, trigger));
        DockExpansionStateChanged?.Invoke(expanded);
    }
}
