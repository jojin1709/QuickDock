using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QuickDock.App.ViewModels;
using QuickDock.Core.Models;

namespace QuickDock.App.Views;

public sealed partial class ClipboardView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(ClipboardViewModel),
            typeof(ClipboardView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty MainViewModelProperty =
        DependencyProperty.Register(
            nameof(MainViewModel),
            typeof(MainViewModel),
            typeof(ClipboardView),
            new PropertyMetadata(null));

    public ClipboardViewModel ViewModel
    {
        get => (ClipboardViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public MainViewModel MainViewModel
    {
        get => (MainViewModel)GetValue(MainViewModelProperty);
        set => SetValue(MainViewModelProperty, value);
    }

    public Visibility GetBoolVisibility(bool isVisible) => isVisible ? Visibility.Visible : Visibility.Collapsed;

    public ClipboardView()
    {
        InitializeComponent();
    }

    private void OnCopyItemClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ClipboardItem item } && ViewModel != null)
        {
            _ = ViewModel.CopyItemCommand.ExecuteAsync(item);
        }
    }

    private void OnTogglePinClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ClipboardItem item } && ViewModel != null)
        {
            _ = ViewModel.TogglePinCommand.ExecuteAsync(item);
        }
    }

    private void OnDeleteItemClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ClipboardItem item } && ViewModel != null)
        {
            _ = ViewModel.DeleteItemCommand.ExecuteAsync(item);
        }
    }
}

