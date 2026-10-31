using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace QuickDock.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        try
        {
            SystemBackdrop = new MicaBackdrop();
        }
        catch
        {
            // Graceful fallback if Mica is not supported
        }

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        RootFrame.Navigate(typeof(MainPage));
    }
}
