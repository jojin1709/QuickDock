using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;
using QuickDock.Core.Settings;

namespace QuickDock.App.ViewModels;

public sealed partial class NotesViewModel : ObservableObject
{
    private readonly INotesService _notesService;
    private readonly ISettingsManager _settingsManager;
    private readonly DispatcherQueue? _dispatcherQueue;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private NoteItem? _selectedNote;

    [ObservableProperty]
    private string _editorTitle = string.Empty;

    [ObservableProperty]
    private string _editorContent = string.Empty;

    [ObservableProperty]
    private string _autosaveStatus = "Ready";

    public ObservableCollection<NoteItem> Notes { get; } = new();

    private System.Threading.Timer? _autosaveTimer;
    private bool _isInternalLoading;

    public NotesViewModel(
        INotesService notesService,
        ISettingsManager settingsManager,
        DispatcherQueue? dispatcherQueue = null)
    {
        _notesService = notesService ?? throw new ArgumentNullException(nameof(notesService));
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
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

    public async Task LoadNotesAsync()
    {
        try
        {
            var notes = await _notesService.GetNotesAsync(SearchQuery);
            Dispatch(() =>
            {
                Notes.Clear();
                foreach (var note in notes)
                {
                    Notes.Add(note);
                }

                if (SelectedNote == null && Notes.Count > 0)
                {
                    SelectNote(Notes[0]);
                }
            });
        }
        catch
        {
            AutosaveStatus = "Error loading notes";
        }
    }

    partial void OnSearchQueryChanged(string value)
    {
        _ = LoadNotesAsync();
    }

    partial void OnSelectedNoteChanged(NoteItem? value)
    {
        if (value == null) return;
        _isInternalLoading = true;
        EditorTitle = value.Title;
        EditorContent = value.Content;
        AutosaveStatus = "Saved";
        _isInternalLoading = false;
    }

    partial void OnEditorTitleChanged(string value)
    {
        if (_isInternalLoading || SelectedNote == null) return;
        TriggerAutosave();
    }

    partial void OnEditorContentChanged(string value)
    {
        if (_isInternalLoading || SelectedNote == null) return;
        TriggerAutosave();
    }

    private void TriggerAutosave()
    {
        AutosaveStatus = "Saving...";
        _autosaveTimer?.Dispose();
        var delay = _settingsManager.CurrentSettings.Notes.AutosaveDebounceMs;
        _autosaveTimer = new System.Threading.Timer(async _ =>
        {
            if (SelectedNote == null) return;
            SelectedNote.Title = EditorTitle;
            SelectedNote.Content = EditorContent;
            await _notesService.SaveNoteAsync(SelectedNote);

            Dispatch(() =>
            {
                AutosaveStatus = "Saved";
                var idx = Notes.IndexOf(SelectedNote);
                if (idx >= 0)
                {
                    Notes[idx] = SelectedNote;
                }
            });
        }, null, delay, Timeout.Infinite);
    }

    [RelayCommand]
    private void SelectNote(NoteItem note)
    {
        SelectedNote = note;
    }

    [RelayCommand]
    private async Task CreateNewNoteAsync()
    {
        var newNote = new NoteItem
        {
            Title = "Untitled Note",
            Content = string.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _notesService.SaveNoteAsync(newNote);
        await LoadNotesAsync();
        SelectNote(newNote);
    }

    [RelayCommand]
    private async Task TogglePinNoteAsync(NoteItem note)
    {
        if (note == null) return;
        var newPinned = !note.IsPinned;
        await _notesService.SetPinnedAsync(note.Id, newPinned);
        note.IsPinned = newPinned;
        await LoadNotesAsync();
    }

    [RelayCommand]
    private async Task DeleteNoteAsync(NoteItem note)
    {
        if (note == null) return;
        await _notesService.DeleteNoteAsync(note.Id);
        if (SelectedNote?.Id == note.Id)
        {
            SelectedNote = null;
            EditorTitle = string.Empty;
            EditorContent = string.Empty;
        }
        await LoadNotesAsync();
    }
}
