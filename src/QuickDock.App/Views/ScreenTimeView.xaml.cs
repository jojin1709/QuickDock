using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QuickDock.App.ViewModels;

namespace QuickDock.App.Views;

public sealed partial class ScreenTimeView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(ScreenTimeViewModel),
            typeof(ScreenTimeView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty MainViewModelProperty =
        DependencyProperty.Register(
            nameof(MainViewModel),
            typeof(MainViewModel),
            typeof(ScreenTimeView),
            new PropertyMetadata(null));

    public ScreenTimeViewModel ViewModel
    {
        get => (ScreenTimeViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public MainViewModel MainViewModel
    {
        get => (MainViewModel)GetValue(MainViewModelProperty);
        set => SetValue(MainViewModelProperty, value);
    }

    public ScreenTimeView()
    {
        InitializeComponent();
    }

    private void OnToggleViewModeClicked(object sender, RoutedEventArgs e)
    {
        _ = ViewModel?.ToggleViewModeCommand.ExecuteAsync(null);
    }
}

