using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QuickDock.App.ViewModels;

namespace QuickDock.App.Views;

public sealed partial class TimerView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(TimerViewModel),
            typeof(TimerView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty MainViewModelProperty =
        DependencyProperty.Register(
            nameof(MainViewModel),
            typeof(MainViewModel),
            typeof(TimerView),
            new PropertyMetadata(null));

    public TimerViewModel ViewModel
    {
        get => (TimerViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public MainViewModel MainViewModel
    {
        get => (MainViewModel)GetValue(MainViewModelProperty);
        set => SetValue(MainViewModelProperty, value);
    }

    public TimerView()
    {
        InitializeComponent();
    }

    public Visibility GetTabVisibility(int selectedIndex, int targetIndex)
    {
        return selectedIndex == targetIndex ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnTabTimersClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null) ViewModel.SelectedTabIndex = 0;
    }

    private void OnTabStopwatchClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null) ViewModel.SelectedTabIndex = 1;
    }

    private void OnTabPomodoroClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null) ViewModel.SelectedTabIndex = 2;
    }

    private void OnPreset5Clicked(object sender, RoutedEventArgs e)
    {
        ViewModel?.CreateTimerPresetCommand.Execute(5);
    }

    private void OnPreset15Clicked(object sender, RoutedEventArgs e)
    {
        ViewModel?.CreateTimerPresetCommand.Execute(15);
    }

    private void OnPreset25Clicked(object sender, RoutedEventArgs e)
    {
        ViewModel?.CreateTimerPresetCommand.Execute(25);
    }

    private void OnPreset45Clicked(object sender, RoutedEventArgs e)
    {
        ViewModel?.CreateTimerPresetCommand.Execute(45);
    }

    private void OnStartTimerClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id })
        {
            ViewModel?.StartTimerCommand.Execute(id);
        }
    }

    private void OnPauseTimerClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id })
        {
            ViewModel?.PauseTimerCommand.Execute(id);
        }
    }

    private void OnResetTimerClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id })
        {
            ViewModel?.ResetTimerCommand.Execute(id);
        }
    }

    private void OnCancelTimerClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id })
        {
            ViewModel?.CancelTimerCommand.Execute(id);
        }
    }
}

