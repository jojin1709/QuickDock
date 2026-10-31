using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Events;

namespace QuickDock.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<MainViewModel> _logger;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private string _currentView = "Home"; // "Home" or "Settings"

    public HomeViewModel Home { get; }
    public SettingsViewModel Settings { get; }

    public event Action<bool>? DockExpansionStateChanged;

    public MainViewModel(
        HomeViewModel home,
        SettingsViewModel settings,
        IEventBus eventBus,
        ILogger<MainViewModel> logger)
    {
        Home = home;
        Settings = settings;
        _eventBus = eventBus;
        _logger = logger;
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
        CurrentView = "Settings";
        if (!IsExpanded)
        {
            SetExpansionState(true, "OpenSettings");
        }
    }

    [RelayCommand]
    public void OpenHome()
    {
        CurrentView = "Home";
    }

    public void SetExpansionState(bool expanded, string trigger)
    {
        if (IsExpanded == expanded) return;

        IsExpanded = expanded;
        _logger.LogInformation("Dock expansion state changed to {Expanded} (Trigger: {Trigger})", expanded, trigger);
        _eventBus.PublishAsync(new AppVisibilityChangedEvent(expanded, trigger));
        DockExpansionStateChanged?.Invoke(expanded);
    }
}

