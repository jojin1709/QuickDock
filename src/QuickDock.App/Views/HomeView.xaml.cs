using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using QuickDock.App.ViewModels;

namespace QuickDock.App.Views;

public sealed partial class HomeView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(HomeViewModel),
            typeof(HomeView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty MainViewModelProperty =
        DependencyProperty.Register(
            nameof(MainViewModel),
            typeof(MainViewModel),
            typeof(HomeView),
            new PropertyMetadata(null));

    public HomeViewModel ViewModel
    {
        get => (HomeViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public MainViewModel MainViewModel
    {
        get => (MainViewModel)GetValue(MainViewModelProperty);
        set => SetValue(MainViewModelProperty, value);
    }

    public HomeView()
    {
        InitializeComponent();
    }

    private void OnTileTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is ModuleTileItem item)
        {
            ViewModel?.SelectModuleCommand.Execute(item);
        }
    }

    private void OnCopyClipClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string content)
        {
            ViewModel?.CopyRecentClipCommand.Execute(content);
        }
    }

    private void OnNewNoteKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            if (ViewModel?.AddQuickNoteCommand.CanExecute(null) == true)
            {
                ViewModel.AddQuickNoteCommand.Execute(null);
            }
            e.Handled = true;
        }
    }

    public Visibility GetBoolVisibility(bool isVisible) => isVisible ? Visibility.Visible : Visibility.Collapsed;

    private Visibility HasSystemModules(HomeViewModel? vm) => vm?.FilteredSystemModules.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    private Visibility HasProductivityModules(HomeViewModel? vm) => vm?.FilteredProductivityModules.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    private Visibility HasUtilityModules(HomeViewModel? vm) => vm?.FilteredUtilityModules.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    private Visibility HasDeveloperModules(HomeViewModel? vm) => vm?.FilteredDeveloperModules.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
}
