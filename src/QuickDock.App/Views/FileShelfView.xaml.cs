using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QuickDock.App.ViewModels;
using QuickDock.Core.Models;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

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

