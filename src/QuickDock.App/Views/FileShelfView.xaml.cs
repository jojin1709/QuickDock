using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QuickDock.App.ViewModels;
using QuickDock.Core.Models;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace QuickDock.App.Views;

public sealed partial class FileShelfView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(FileShelfViewModel),
            typeof(FileShelfView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty MainViewModelProperty =
        DependencyProperty.Register(
            nameof(MainViewModel),
            typeof(MainViewModel),
            typeof(FileShelfView),
            new PropertyMetadata(null));

    public FileShelfViewModel ViewModel
    {
        get => (FileShelfViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public MainViewModel MainViewModel
    {
        get => (MainViewModel)GetValue(MainViewModelProperty);
        set => SetValue(MainViewModelProperty, value);
    }

    public FileShelfView()
    {
        InitializeComponent();
        Loaded += async (s, e) =>
        {
            if (ViewModel != null)
            {
                await ViewModel.LoadItemsAsync();
            }
        };
    }

    private async void OnAddFilesClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add("*");

            var files = await picker.PickMultipleFilesAsync();
            if (files != null && files.Count > 0)
            {
                var paths = files.Select(f => f.Path).ToList();
                await ViewModel.AddFilesAsync(paths);
            }
        }
        catch
        {
            // Fail silently or handle error safely
        }
    }

    private async void OnAddFolderClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FolderPicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add("*");

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                await ViewModel.AddFilesAsync(new[] { folder.Path });
            }
        }
        catch
        {
            // Fail silently or handle error safely
        }
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
                var paths = items.Select(i => i.Path).ToList();
                await ViewModel.AddFilesAsync(paths);
            }
            finally
            {
                def.Complete();
            }
        }
    }

    private async void OnItemDragStarting(UIElement sender, DragStartingEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is FileShelfItem item && File.Exists(item.Path))
        {
            var def = e.GetDeferral();
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(item.Path);
                e.Data.SetStorageItems(new[] { file });
                e.Data.RequestedOperation = DataPackageOperation.Copy;
            }
            catch
            {
                // Ignore if access denied or already deleted
            }
            finally
            {
                def.Complete();
            }
        }
    }

    private void OnPreviewClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.DataContext is FileShelfItem item)
        {
            _ = ViewModel.PreviewFileAsync(item);
        }
    }

    private void OnOpenClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.DataContext is FileShelfItem item)
        {
            _ = ViewModel.OpenFileAsync(item);
        }
    }

    private void OnRevealClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.DataContext is FileShelfItem item)
        {
            _ = ViewModel.RevealInExplorerAsync(item);
        }
    }

    private void OnCopyPathClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.DataContext is FileShelfItem item)
        {
            ViewModel.CopyPath(item);
        }
    }

    private void OnRemoveClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.DataContext is FileShelfItem item)
        {
            _ = ViewModel.RemoveItemAsync(item);
        }
    }
}

