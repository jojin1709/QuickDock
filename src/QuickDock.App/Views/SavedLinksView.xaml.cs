using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QuickDock.App.ViewModels;
using QuickDock.Core.Models;

namespace QuickDock.App.Views;

public sealed partial class SavedLinksView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(SavedLinksViewModel),
            typeof(SavedLinksView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty MainViewModelProperty =
        DependencyProperty.Register(
            nameof(MainViewModel),
            typeof(MainViewModel),
            typeof(SavedLinksView),
            new PropertyMetadata(null));

    public SavedLinksViewModel ViewModel
    {
        get => (SavedLinksViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public MainViewModel MainViewModel
    {
        get => (MainViewModel)GetValue(MainViewModelProperty);
        set => SetValue(MainViewModelProperty, value);
    }

    public SavedLinksView()
    {
        InitializeComponent();
    }

    private void OnOpenLinkClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SavedLinkItem link } && ViewModel != null)
        {
            ViewModel.OpenLinkCommand.Execute(link);
        }
    }

    private void OnTogglePinClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SavedLinkItem link } && ViewModel != null)
        {
            _ = ViewModel.TogglePinCommand.ExecuteAsync(link);
        }
    }

    private void OnDeleteLinkClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SavedLinkItem link } && ViewModel != null)
        {
            _ = ViewModel.DeleteLinkCommand.ExecuteAsync(link);
        }
    }
}

