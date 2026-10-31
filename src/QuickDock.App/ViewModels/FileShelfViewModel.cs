using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;
using Windows.ApplicationModel.DataTransfer;

namespace QuickDock.App.ViewModels;

public sealed partial class FileShelfViewModel : ObservableObject
{
    private readonly IFileShelfService _shelfService;
    private readonly IFilePreviewService _previewService;
    private readonly ILogger<FileShelfViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<FileShelfItem> _items = new();

    [ObservableProperty]
    private FileShelfItem? _selectedItem;

    [ObservableProperty]
    private FilePreviewInfo? _activePreview;

    [ObservableProperty]
    private bool _isPreviewOpen;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public event Action<string>? RequestNavigate;

    public FileShelfViewModel(
        IFileShelfService shelfService,
        IFilePreviewService previewService,
        ILogger<FileShelfViewModel> logger)
    {
        _shelfService = shelfService ?? throw new ArgumentNullException(nameof(shelfService));
        _previewService = previewService ?? throw new ArgumentNullException(nameof(previewService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _shelfService.ShelfChanged += (s, e) => _ = LoadItemsAsync();
    }

    [RelayCommand]
    public async Task LoadItemsAsync()
    {
        IsLoading = true;
        try
        {
            var list = await _shelfService.GetItemsAsync();
            Items.Clear();
            foreach (var item in list)
            {
                Items.Add(item);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load shelf items");
            StatusMessage = "Error loading shelf items.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task AddFilesAsync(IEnumerable<string> paths)
    {
        try
        {
            int added = await _shelfService.AddFilesAsync(paths);
            if (added > 0)
            {
                StatusMessage = $"Added {added} item(s) to shelf.";
            }
            await LoadItemsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add files to shelf");
        }
    }

    [RelayCommand]
    public async Task RemoveItemAsync(FileShelfItem? item)
    {
        if (item == null) return;
        try
        {
            await _shelfService.RemoveItemAsync(item.Id);
            Items.Remove(item);
            if (SelectedItem?.Id == item.Id)
            {
                SelectedItem = null;
                IsPreviewOpen = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove item {Id}", item.Id);
        }
    }

    [RelayCommand]
    public async Task ClearShelfAsync()
    {
        try
        {
            await _shelfService.ClearShelfAsync();
            Items.Clear();
            SelectedItem = null;
            IsPreviewOpen = false;
            StatusMessage = "Shelf cleared.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear shelf");
        }
    }

    [RelayCommand]
    public async Task RemoveMissingAsync()
    {
        try
        {
            int removed = await _shelfService.RemoveMissingItemsAsync();
            StatusMessage = removed > 0 ? $"Removed {removed} missing item(s)." : "No missing items found.";
            await LoadItemsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove missing items");
        }
    }

    [RelayCommand]
    public async Task OpenFileAsync(FileShelfItem? item)
    {
        if (item == null) return;
        await _shelfService.OpenFileAsync(item.Path);
    }

    [RelayCommand]
    public async Task RevealInExplorerAsync(FileShelfItem? item)
    {
        if (item == null) return;
        await _shelfService.RevealInExplorerAsync(item.Path);
    }

    [RelayCommand]
    public void CopyPath(FileShelfItem? item)
    {
        if (item == null) return;
        try
        {
            var package = new DataPackage();
            package.SetText(item.Path);
            Clipboard.SetContent(package);
            StatusMessage = "Path copied to clipboard.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy path to clipboard");
        }
    }

    [RelayCommand]
    public async Task PreviewFileAsync(FileShelfItem? item)
    {
        if (item == null) return;
        SelectedItem = item;

        try
        {
            ActivePreview = await _previewService.GeneratePreviewAsync(item.Path);
            IsPreviewOpen = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate preview for {Path}", item.Path);
        }
    }

    [RelayCommand]
    public void ClosePreview()
    {
        IsPreviewOpen = false;
        ActivePreview = null;
    }

    [RelayCommand]
    public async Task DeleteFileAsync(FileShelfItem? item)
    {
        if (item == null) return;
        try
        {
            bool deleted = await _shelfService.DeleteFileAsync(item.Path);
            if (deleted)
            {
                await _shelfService.RemoveItemAsync(item.Id);
                Items.Remove(item);
                if (SelectedItem?.Id == item.Id)
                {
                    SelectedItem = null;
                    IsPreviewOpen = false;
                }
                StatusMessage = "File deleted.";
            }
            else
            {
                StatusMessage = "Could not delete file.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file {Path}", item.Path);
        }
    }

    [RelayCommand]
    public void SendToQuickDrop(FileShelfItem? item)
    {
        if (item == null) return;
        RequestNavigate?.Invoke("quickdrop");
    }

    [RelayCommand]
    public void SendToConverter(FileShelfItem? item)
    {
        if (item == null) return;
        RequestNavigate?.Invoke("converter");
    }
}

