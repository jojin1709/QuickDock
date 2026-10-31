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

    private Visibility IsSectionVisible(string selectedSection, string targetSection) =>
        string.Equals(selectedSection, targetSection, StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;

    private Visibility HasSearchResults(int count) => count > 0 ? Visibility.Visible : Visibility.Collapsed;
    private Visibility NoSearchResults(int count) => count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private Visibility IsCustomAccentVisible(int accentIndex) => accentIndex == 5 ? Visibility.Visible : Visibility.Collapsed;
    private Visibility HasEditingShortcut(ShortcutDisplayItem? item) => item != null ? Visibility.Visible : Visibility.Collapsed;
    private Visibility HasStatusMessage(string? msg) => !string.IsNullOrWhiteSpace(msg) ? Visibility.Visible : Visibility.Collapsed;

    private string FormatHoverDelay(int delayMs) => $"{delayMs} ms";
    private string FormatSeconds(int seconds) => $"{seconds} s";
}
