using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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

    private static readonly SolidColorBrush ActiveBg = new(Windows.UI.Color.FromArgb(255, 39, 39, 42));
    private static readonly SolidColorBrush InactiveBg = new(Microsoft.UI.Colors.Transparent);
    private static readonly SolidColorBrush ActiveFg = new(Microsoft.UI.Colors.White);
    private static readonly SolidColorBrush InactiveFg = new(Windows.UI.Color.FromArgb(255, 142, 142, 147));

    private static readonly SolidColorBrush ActiveBorder = new(Microsoft.UI.Colors.White);
    private static readonly SolidColorBrush InactiveBorder = new(Windows.UI.Color.FromArgb(255, 34, 34, 38));

    public SettingsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateAllVisualStates();
        if (ViewModel != null)
        {
            ViewModel.PropertyChanged += (s, args) => UpdateAllVisualStates();
        }
    }

    private void UpdateAllVisualStates()
    {
        if (ViewModel == null) return;

        // Time format
        SetSegment(BtnTimeSystem, ViewModel.TimeFormatIndex == 0);
        SetSegment(BtnTime12h, ViewModel.TimeFormatIndex == 1);
        SetSegment(BtnTime24h, ViewModel.TimeFormatIndex == 2);

        // Temp
        SetSegment(BtnTempC, ViewModel.TemperatureUnitIndex == 0);
        SetSegment(BtnTempF, ViewModel.TemperatureUnitIndex == 1);

        // Wind
        SetSegment(BtnWindKmh, ViewModel.WindSpeedUnitIndex == 0);
        SetSegment(BtnWindMph, ViewModel.WindSpeedUnitIndex == 1);

        // Preview
        SetSegment(BtnPreviewResting, !ViewModel.PreviewIsHover);
        SetSegment(BtnPreviewHover, ViewModel.PreviewIsHover);

        // Visibility
        SetBorder(CardVisibilityAlways, ViewModel.VisibilityMode == 0);
        SetBorder(CardVisibilitySmart, ViewModel.VisibilityMode == 1);
        SetBorder(CardVisibilityMinimal, ViewModel.VisibilityMode == 2);
        SetBorder(CardVisibilityHidden, ViewModel.VisibilityMode == 3);

        // Style
        SetBorder(CardStyleLine, ViewModel.StyleMode == 0);
        SetBorder(CardStyleNotch, ViewModel.StyleMode == 1);
        SetBorder(CardStyleIsland, ViewModel.StyleMode == 2);

        // Theme
        SetBorder(CardThemePureBlack, ViewModel.ThemeMode == 0);
        SetBorder(CardThemeEmberDark, ViewModel.ThemeMode == 1);
        SetBorder(CardThemeVivid, ViewModel.ThemeMode == 2);
        SetBorder(CardThemeSilver, ViewModel.ThemeMode == 3);

        // Resting
        SetSegment(BtnRestingCorners, ViewModel.RestingLayoutIndex == 0);
        SetSegment(BtnRestingCenter, ViewModel.RestingLayoutIndex == 1);

        // Outline
        SetSegment(BtnOutline1px, ViewModel.OutlineWidthIndex == 0);
        SetSegment(BtnOutline2px, ViewModel.OutlineWidthIndex == 1);
        SetSegment(BtnOutline3px, ViewModel.OutlineWidthIndex == 2);

        // Shadow
        SetSegment(BtnShadowNone, ViewModel.ShadowLevelIndex == 0);
        SetSegment(BtnShadowSubtle, ViewModel.ShadowLevelIndex == 1);
        SetSegment(BtnShadowMedium, ViewModel.ShadowLevelIndex == 2);
        SetSegment(BtnShadowDeep, ViewModel.ShadowLevelIndex == 3);

        // Soft edge
        SetSegment(BtnSoftEdgeNone, ViewModel.SoftEdgeLevelIndex == 0);
        SetSegment(BtnSoftEdgeLow, ViewModel.SoftEdgeLevelIndex == 1);
        SetSegment(BtnSoftEdgeMedium, ViewModel.SoftEdgeLevelIndex == 2);
        SetSegment(BtnSoftEdgeHigh, ViewModel.SoftEdgeLevelIndex == 3);
    }

    private static void SetSegment(Button? btn, bool active)
    {
        if (btn == null) return;
        btn.Background = active ? ActiveBg : InactiveBg;
        btn.Foreground = active ? ActiveFg : InactiveFg;
    }

    private static void SetBorder(Border? border, bool active)
    {
        if (border == null) return;
        border.BorderBrush = active ? ActiveBorder : InactiveBorder;
    }

    private void OnTimeFormatClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag && int.TryParse(tag, out int idx))
        {
            ViewModel?.SetTimeFormat(idx);
            UpdateAllVisualStates();
        }
    }

    private void OnTempUnitClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag && int.TryParse(tag, out int idx))
        {
            ViewModel?.SetTemperatureUnit(idx);
            UpdateAllVisualStates();
        }
    }

    private void OnWindSpeedClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag && int.TryParse(tag, out int idx))
        {
            ViewModel?.SetWindSpeedUnit(idx);
            UpdateAllVisualStates();
        }
    }

    private void OnPreviewModeClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag && int.TryParse(tag, out int idx))
        {
            ViewModel?.SetPreviewHover(idx == 1);
            UpdateAllVisualStates();
        }
    }

    private void OnVisibilityCardClick(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Border border && border.Tag is string tag && int.TryParse(tag, out int idx))
        {
            ViewModel?.SetVisibilityMode(idx);
            UpdateAllVisualStates();
        }
    }

    private void OnStyleCardClick(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Border border && border.Tag is string tag && int.TryParse(tag, out int idx))
        {
            ViewModel?.SetStyleMode(idx);
            UpdateAllVisualStates();
        }
    }

    private void OnThemeCardClick(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Border border && border.Tag is string tag && int.TryParse(tag, out int idx))
        {
            ViewModel?.SetThemeMode(idx);
            UpdateAllVisualStates();
        }
    }

    private void OnRestingLayoutClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag && int.TryParse(tag, out int idx))
        {
            ViewModel?.SetRestingLayout(idx);
            UpdateAllVisualStates();
        }
    }

    private void OnOutlineWidthClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag && int.TryParse(tag, out int idx))
        {
            ViewModel?.SetOutlineWidth(idx);
            UpdateAllVisualStates();
        }
    }

    private void OnShadowClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag && int.TryParse(tag, out int idx))
        {
            ViewModel?.SetShadowLevel(idx);
            UpdateAllVisualStates();
        }
    }

    private void OnSoftEdgeClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag && int.TryParse(tag, out int idx))
        {
            ViewModel?.SetSoftEdgeLevel(idx);
            UpdateAllVisualStates();
        }
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
