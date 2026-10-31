using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QuickDock.App.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;

namespace QuickDock.App.Views;

public sealed partial class FileConverterView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(FileConverterViewModel),
            typeof(FileConverterView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty MainViewModelProperty =
        DependencyProperty.Register(
            nameof(MainViewModel),
            typeof(MainViewModel),
            typeof(FileConverterView),
            new PropertyMetadata(null));

    public FileConverterViewModel ViewModel
    {
        get => (FileConverterViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public MainViewModel MainViewModel
    {
        get => (MainViewModel)GetValue(MainViewModelProperty);
        set => SetValue(MainViewModelProperty, value);
    }

    public FileConverterView()
    {
        InitializeComponent();
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var def = e.GetDeferral();
            try
            {
                var items = await e.DataView.GetStorageItemsAsync();
                var first = items.FirstOrDefault();
                if (first != null)
                {
                    ViewModel.SetSourceFile(first.Path);
                }
            }
            finally
            {
                def.Complete();
            }
        }
    }

    private async void OnBrowseFileClicked(object sender, RoutedEventArgs e)
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
                ViewModel.SetSourceFile(file.Path);
            }
        }
        catch
        {
            // Ignore
        }
    }

    private void OnRevealResultClicked(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(ViewModel.LastOutputFilePath) && File.Exists(ViewModel.LastOutputFilePath))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{ViewModel.LastOutputFilePath}\"",
                    UseShellExecute = true
                });
            }
            catch
            {
                // Ignore
            }
        }
    }
}
