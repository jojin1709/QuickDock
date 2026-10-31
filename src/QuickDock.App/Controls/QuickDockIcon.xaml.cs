using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace QuickDock.App.Controls;

public sealed partial class QuickDockIcon : UserControl
{
    public static readonly DependencyProperty GlyphBrushProperty =
        DependencyProperty.Register(
            nameof(GlyphBrush),
            typeof(Brush),
            typeof(QuickDockIcon),
            new PropertyMetadata(null));

    public Brush? GlyphBrush
    {
        get => (Brush?)GetValue(GlyphBrushProperty);
        set => SetValue(GlyphBrushProperty, value);
    }

    public QuickDockIcon()
    {
        InitializeComponent();
    }
}
