using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QuickDock.App.ViewModels;

namespace QuickDock.App.Views;

public sealed partial class WeatherView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(WeatherViewModel),
            typeof(WeatherView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty MainViewModelProperty =
        DependencyProperty.Register(
            nameof(MainViewModel),
            typeof(MainViewModel),
            typeof(WeatherView),
            new PropertyMetadata(null));

    public WeatherViewModel ViewModel
    {
        get => (WeatherViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public MainViewModel MainViewModel
    {
        get => (MainViewModel)GetValue(MainViewModelProperty);
        set => SetValue(MainViewModelProperty, value);
    }

    public WeatherView()
    {
        InitializeComponent();
    }
}

