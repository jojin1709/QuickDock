using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace QuickDock.App.Controls;

public sealed partial class QuickDockSection : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(QuickDockSection),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty BadgeProperty =
        DependencyProperty.Register(
            nameof(Badge),
            typeof(string),
            typeof(QuickDockSection),
            new PropertyMetadata(string.Empty));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Badge
    {
        get => (string)GetValue(BadgeProperty);
        set => SetValue(BadgeProperty, value);
    }

    public Visibility HasBadge => GetBadgeVisibility(Badge);

    private Visibility GetBadgeVisibility(string badge) =>
        string.IsNullOrEmpty(badge) ? Visibility.Collapsed : Visibility.Visible;

    public QuickDockSection()
    {
        InitializeComponent();
    }
}

