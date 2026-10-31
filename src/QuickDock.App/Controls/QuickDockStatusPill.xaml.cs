using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace QuickDock.App.Controls;

public sealed partial class QuickDockStatusPill : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(QuickDockStatusPill),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DotBrushProperty =
        DependencyProperty.Register(
            nameof(DotBrush),
            typeof(Brush),
            typeof(QuickDockStatusPill),
            new PropertyMetadata(null));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public Brush DotBrush
    {
        get => (Brush)GetValue(DotBrushProperty);
        set => SetValue(DotBrushProperty, value);
    }

    public QuickDockStatusPill()
    {
        InitializeComponent();
    }
}

