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

    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush DefaultCardBorderBrush =
        new(Windows.UI.Color.FromArgb(255, 0x27, 0x27, 0x2A));
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush HoverCardBorderBrush =
        new(Windows.UI.Color.FromArgb(255, 0x4F, 0x4F, 0x56));

    private void OnCardPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border)
        {
            border.BorderBrush = HoverCardBorderBrush;
        }
    }

    private void OnCardPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border)
        {
            border.BorderBrush = DefaultCardBorderBrush;
        }
    }

    private void OnScreenTimeCardTapped(object sender, TappedRoutedEventArgs e)
    {
        MainViewModel?.OpenScreenTime();
    }

    private void OnSystemCardTapped(object sender, TappedRoutedEventArgs e)
    {
        MainViewModel?.OpenSystem();
    }

    private void OnWeatherCardTapped(object sender, TappedRoutedEventArgs e)
    {
        MainViewModel?.OpenWeather();
    }

    private void OnFocusCardTapped(object sender, TappedRoutedEventArgs e)
    {
        MainViewModel?.OpenTimer();
    }

    private void OnClipboardCardTapped(object sender, TappedRoutedEventArgs e)
    {
        MainViewModel?.OpenClipboard();
    }

    private void OnNotesCardTapped(object sender, TappedRoutedEventArgs e)
    {
        // If clicking inside the Quick Note text box, let user type; otherwise navigate to Notes
        if (e.OriginalSource is not TextBox)
        {
            MainViewModel?.OpenNotes();
        }
    }

    public Visibility GetBoolVisibility(bool isVisible) => isVisible ? Visibility.Visible : Visibility.Collapsed;

    private Visibility HasSystemModules(HomeViewModel? vm) => vm?.FilteredSystemModules.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    private Visibility HasProductivityModules(HomeViewModel? vm) => vm?.FilteredProductivityModules.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    private Visibility HasUtilityModules(HomeViewModel? vm) => vm?.FilteredUtilityModules.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    private Visibility HasDeveloperModules(HomeViewModel? vm) => vm?.FilteredDeveloperModules.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
}
