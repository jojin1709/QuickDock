using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QuickDock.App.ViewModels;

namespace QuickDock.App.Views;

public sealed partial class SystemView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(SystemViewModel),
            typeof(SystemView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty MainViewModelProperty =
        DependencyProperty.Register(
            nameof(MainViewModel),
            typeof(MainViewModel),
            typeof(SystemView),
            new PropertyMetadata(null));

    public SystemViewModel ViewModel
    {
        get => (SystemViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public MainViewModel MainViewModel
    {
        get => (MainViewModel)GetValue(MainViewModelProperty);
        set => SetValue(MainViewModelProperty, value);
    }

    public SystemView()
    {
        InitializeComponent();
        Loaded += (s, e) => ViewModel?.StartLiveUpdates(DispatcherQueue);
        Unloaded += (s, e) => ViewModel?.StopLiveUpdates();
    }
}
