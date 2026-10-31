using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QuickDock.App.ViewModels;
using QuickDock.Core.Models;
using Windows.Storage.Pickers;

namespace QuickDock.App.Views;

public sealed partial class QuickDropView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(QuickDropViewModel),
            typeof(QuickDropView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty MainViewModelProperty =
        DependencyProperty.Register(
            nameof(MainViewModel),
            typeof(MainViewModel),
            typeof(QuickDropView),
            new PropertyMetadata(null));

    public QuickDropViewModel ViewModel
    {
        get => (QuickDropViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public MainViewModel MainViewModel
    {
        get => (MainViewModel)GetValue(MainViewModelProperty);
        set => SetValue(MainViewModelProperty, value);
    }

    public QuickDropView()
    {
        InitializeComponent();
    }

    private async void OnShareFilePickerClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add("*");

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                ViewModel.AddSharedFile(file.Path);
            }
        }
        catch
        {
            // Fallback or ignore
        }
    }

    private void OnRemoveSharedClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.DataContext is QuickDropSharedFile file)
        {
            ViewModel.RemoveSharedFile(file);
        }
    }
}
