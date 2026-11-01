using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;
using QuickDock.Core.Settings;

namespace QuickDock.App.ViewModels;

public sealed partial class ClipboardViewModel : ObservableObject
{
    private readonly IClipboardService _clipboardService;
    private readonly ISettingsManager _settingsManager;
    private readonly DispatcherQueue? _dispatcherQueue;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isPaused;

    public string PauseButtonText => IsPaused ? "Resume" : "Pause";
    public string PauseButtonGlyph => IsPaused ? "\uE768" : "\uE769";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasPinnedItems;

    [ObservableProperty]
    private bool _hasRecentItems;

    [ObservableProperty]
    private bool _isEmpty = true;

    public ObservableCollection<ClipboardItem> PinnedItems { get; } = new();
    public ObservableCollection<ClipboardItem> RecentItems { get; } = new();

    public ClipboardViewModel(
        IClipboardService clipboardService,
        ISettingsManager settingsManager,
        DispatcherQueue? dispatcherQueue = null)
    {
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _dispatcherQueue = dispatcherQueue ?? DispatcherQueue.GetForCurrentThread();

        _isPaused = _clipboardService.IsPaused;
        _clipboardService.ClipboardItemAdded += OnClipboardItemAdded;
        _clipboardService.ClipboardCleared += OnClipboardCleared;
    }

    private void Dispatch(Action action)
    {
        if (_dispatcherQueue != null)
        {
            _dispatcherQueue.TryEnqueue(() => action());
        }
        else
        {
            action();
        }
    }

    public async Task LoadHistoryAsync()
    {
        try
        {
            var items = await _clipboardService.GetHistoryAsync(SearchQuery);
            Dispatch(() =>
            {
                PinnedItems.Clear();
                RecentItems.Clear();

                foreach (var item in items)
                {
                    if (item.IsPinned)
                    {
                        PinnedItems.Add(item);
                    }
                    else
                    {
                        RecentItems.Add(item);
                    }
                }
                HasPinnedItems = PinnedItems.Count > 0;
                HasRecentItems = RecentItems.Count > 0;
                IsEmpty = PinnedItems.Count == 0 && RecentItems.Count == 0;
            });
        }
        catch (Exception)
        {
            StatusMessage = "Failed to load clipboard history.";
        }
    }

    partial void OnSearchQueryChanged(string value)
    {
        _ = LoadHistoryAsync();
    }

    partial void OnIsPausedChanged(bool value)
    {
        OnPropertyChanged(nameof(PauseButtonText));
        OnPropertyChanged(nameof(PauseButtonGlyph));
    }

    [RelayCommand]
    private void TogglePause()
    {
        IsPaused = !IsPaused;
        _clipboardService.IsPaused = IsPaused;
    }

    [RelayCommand]
    private async Task CopyItemAsync(ClipboardItem item)
    {
        if (item == null) return;
        await _clipboardService.CopyItemToClipboardAsync(item);
        StatusMessage = "Copied to clipboard.";
    }

    [RelayCommand]
    private async Task TogglePinAsync(ClipboardItem item)
    {
        if (item == null) return;
        var newPinned = !item.IsPinned;
        await _clipboardService.SetPinnedAsync(item.Id, newPinned);
        item.IsPinned = newPinned;
        await LoadHistoryAsync();
    }

    [RelayCommand]
    private async Task DeleteItemAsync(ClipboardItem item)
    {
        if (item == null) return;
        await _clipboardService.DeleteItemAsync(item.Id);
        await LoadHistoryAsync();
    }

    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        await _clipboardService.ClearHistoryAsync(clearPinned: false);
        await LoadHistoryAsync();
    }

    private void OnClipboardItemAdded(object? sender, ClipboardItem e)
    {
        _ = LoadHistoryAsync();
    }

    private void OnClipboardCleared(object? sender, EventArgs e)
    {
        _ = LoadHistoryAsync();
    }
}
