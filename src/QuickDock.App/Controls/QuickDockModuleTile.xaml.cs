using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace QuickDock.App.Controls;

public sealed partial class QuickDockModuleTile : UserControl
{
    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.Register(
            nameof(Glyph),
            typeof(string),
            typeof(QuickDockModuleTile),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(QuickDockModuleTile),
            new PropertyMetadata(string.Empty, OnTitleChanged));

    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(
            nameof(Subtitle),
            typeof(string),
            typeof(QuickDockModuleTile),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty StatusTextProperty =
        DependencyProperty.Register(
            nameof(StatusText),
            typeof(string),
            typeof(QuickDockModuleTile),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(
            nameof(Command),
            typeof(ICommand),
            typeof(QuickDockModuleTile),
            new PropertyMetadata(null));

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.Register(
            nameof(CommandParameter),
            typeof(object),
            typeof(QuickDockModuleTile),
            new PropertyMetadata(null));

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public Visibility HasStatus => GetStatusVisibility(StatusText);

    private Visibility GetStatusVisibility(string text) =>
        string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;

    public QuickDockModuleTile()
    {
        InitializeComponent();
    }

    private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is QuickDockModuleTile tile && tile.RootButton != null)
        {
            AutomationProperties.SetName(tile.RootButton, (string)e.NewValue);
        }
    }

    private void OnTileClicked(object sender, RoutedEventArgs e)
    {
        if (Command != null && Command.CanExecute(CommandParameter))
        {
            Command.Execute(CommandParameter);
        }
    }
}

