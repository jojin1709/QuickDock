using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using QuickDock.App.ViewModels;
using QuickDock.Core.Models;

namespace QuickDock.App.Views;

public sealed partial class NotesView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(NotesViewModel),
            typeof(NotesView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty MainViewModelProperty =
        DependencyProperty.Register(
            nameof(MainViewModel),
            typeof(MainViewModel),
            typeof(NotesView),
            new PropertyMetadata(null));

    public NotesViewModel ViewModel
    {
        get => (NotesViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public MainViewModel MainViewModel
    {
        get => (MainViewModel)GetValue(MainViewModelProperty);
        set => SetValue(MainViewModelProperty, value);
    }

    public NotesView()
    {
        InitializeComponent();
    }

    private void OnSelectNoteTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: NoteItem note } && ViewModel != null)
        {
            ViewModel.SelectNoteCommand.Execute(note);
        }
    }

    private void OnTogglePinNoteClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: NoteItem note } && ViewModel != null)
        {
            _ = ViewModel.TogglePinNoteCommand.ExecuteAsync(note);
        }
    }

    private void OnDeleteNoteClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: NoteItem note } && ViewModel != null)
        {
            _ = ViewModel.DeleteNoteCommand.ExecuteAsync(note);
        }
    }
}

