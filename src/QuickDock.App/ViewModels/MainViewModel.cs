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
    private string _currentView = "Home"; // "Home", "System", or "Settings"

    public HomeViewModel Home { get; }
    public SystemViewModel System { get; }
    public SettingsViewModel Settings { get; }

    public event Action<bool>? DockExpansionStateChanged;

    public MainViewModel(
        HomeViewModel home,
        SystemViewModel system,
        SettingsViewModel settings,
        IEventBus eventBus,
        ILogger<MainViewModel> logger)
    {
        Home = home;
        System = system;
        Settings = settings;
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
        if (targetView == "System")
        {
            OpenSystem();
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
        if (!IsExpanded)
        {
            SetExpansionState(true, "OpenSettings");
        }
    }

    [RelayCommand]
    public void OpenSystem()
    {
        SwitchView("System");
        if (!IsExpanded)
        {
            SetExpansionState(true, "OpenSystem");
        }
    }

    [RelayCommand]
    public void OpenHome()
    {
        SwitchView("Home");
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

        _eventBus.PublishAsync(new AppVisibilityChangedEvent(expanded, trigger));
        DockExpansionStateChanged?.Invoke(expanded);
    }
}
