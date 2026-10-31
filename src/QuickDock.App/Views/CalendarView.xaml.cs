using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QuickDock.App.ViewModels;
using QuickDock.Core.Models;

namespace QuickDock.App.Views;

public sealed partial class CalendarView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(CalendarViewModel),
            typeof(CalendarView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty MainViewModelProperty =
        DependencyProperty.Register(
            nameof(MainViewModel),
            typeof(MainViewModel),
            typeof(CalendarView),
            new PropertyMetadata(null));

    public CalendarViewModel ViewModel
    {
        get => (CalendarViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public MainViewModel MainViewModel
    {
        get => (MainViewModel)GetValue(MainViewModelProperty);
        set => SetValue(MainViewModelProperty, value);
    }

    public CalendarView()
    {
        InitializeComponent();
    }

    private void OnDeleteEventClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CalendarEventItem ev } && ViewModel != null)
        {
            _ = ViewModel.DeleteEventCommand.ExecuteAsync(ev);
        }
    }
}

