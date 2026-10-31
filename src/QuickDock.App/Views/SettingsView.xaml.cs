using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QuickDock.App.ViewModels;

namespace QuickDock.App.Views;

public sealed partial class SettingsView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(SettingsViewModel),
            typeof(SettingsView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty MainViewModelProperty =
        DependencyProperty.Register(
            nameof(MainViewModel),
            typeof(MainViewModel),
            typeof(SettingsView),
            new PropertyMetadata(null));

    public SettingsViewModel ViewModel
    {
        get => (SettingsViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public MainViewModel MainViewModel
    {
        get => (MainViewModel)GetValue(MainViewModelProperty);
        set => SetValue(MainViewModelProperty, value);
    }

    public SettingsView()
    {
        InitializeComponent();
    }

    private string FormatHoverDelay(SettingsViewModel? vm) => vm != null ? $"{vm.HoverDelayMs} ms" : "150 ms";
}

