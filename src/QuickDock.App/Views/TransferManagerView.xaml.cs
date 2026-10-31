using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QuickDock.App.ViewModels;
using QuickDock.Core.Models;

namespace QuickDock.App.Views;

public sealed partial class TransferManagerView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(TransferManagerViewModel),
            typeof(TransferManagerView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty MainViewModelProperty =
        DependencyProperty.Register(
            nameof(MainViewModel),
            typeof(MainViewModel),
            typeof(TransferManagerView),
            new PropertyMetadata(null));

    public TransferManagerViewModel ViewModel
    {
        get => (TransferManagerViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public MainViewModel MainViewModel
    {
        get => (MainViewModel)GetValue(MainViewModelProperty);
        set => SetValue(MainViewModelProperty, value);
    }

    public TransferManagerView()
    {
        InitializeComponent();
    }

    private void OnCancelTransferClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.DataContext is TransferItem item)
        {
            ViewModel.CancelTransfer(item);
        }
    }
}

