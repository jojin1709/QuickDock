using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.App.ViewModels;

public sealed partial class SavedLinksViewModel : ObservableObject
{
    private readonly ISavedLinksService _linksService;
    private readonly DispatcherQueue? _dispatcherQueue;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _newTitle = string.Empty;

    [ObservableProperty]
    private string _newUrl = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<SavedLinkItem> Links { get; } = new();

    public SavedLinksViewModel(
        ISavedLinksService linksService,
        DispatcherQueue? dispatcherQueue = null)
    {
        _linksService = linksService ?? throw new ArgumentNullException(nameof(linksService));
        _dispatcherQueue = dispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
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

    public async Task LoadLinksAsync()
    {
        try
        {
            var links = await _linksService.GetLinksAsync(SearchQuery);
            Dispatch(() =>
            {
                Links.Clear();
                foreach (var link in links)
                {
                    Links.Add(link);
                }
            });
        }
        catch
        {
            StatusMessage = "Failed to load links.";
        }
    }

    partial void OnSearchQueryChanged(string value)
    {
        _ = LoadLinksAsync();
    }

    [RelayCommand]
    private async Task AddLinkAsync()
    {
        if (string.IsNullOrWhiteSpace(NewUrl)) return;

        var url = NewUrl.Trim();
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            StatusMessage = "Invalid URL. Please enter a valid web address.";
            return;
        }

        var title = string.IsNullOrWhiteSpace(NewTitle) ? uri.Host : NewTitle.Trim();

        var link = new SavedLinkItem
        {
            Title = title,
            Url = url,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _linksService.SaveLinkAsync(link);
        NewTitle = string.Empty;
        NewUrl = string.Empty;
        StatusMessage = "Link saved.";
        await LoadLinksAsync();
    }

    [RelayCommand]
    private void OpenLink(SavedLinkItem link)
    {
        if (link == null) return;
        var success = _linksService.OpenLink(link.Url);
        if (!success)
        {
            StatusMessage = "Could not open link.";
        }
    }

    [RelayCommand]
    private async Task TogglePinAsync(SavedLinkItem link)
    {
        if (link == null) return;
        var newPinned = !link.IsPinned;
        await _linksService.SetPinnedAsync(link.Id, newPinned);
        link.IsPinned = newPinned;
        await LoadLinksAsync();
    }

    [RelayCommand]
    private async Task DeleteLinkAsync(SavedLinkItem link)
    {
        if (link == null) return;
        await _linksService.DeleteLinkAsync(link.Id);
        await LoadLinksAsync();
    }
}
