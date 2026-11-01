using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using QuickDock.App.ViewModels;
using QuickDock.Core.Models;
using Windows.Storage.Streams;

namespace QuickDock.App.Views;

public sealed partial class DeveloperView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(DeveloperViewModel),
            typeof(DeveloperView),
            new PropertyMetadata(null, OnViewModelChangedStatic));

    public static readonly DependencyProperty MainViewModelProperty =
        DependencyProperty.Register(
            nameof(MainViewModel),
            typeof(MainViewModel),
            typeof(DeveloperView),
            new PropertyMetadata(null));

    public DeveloperViewModel? ViewModel
    {
        get => (DeveloperViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public MainViewModel? MainViewModel
    {
        get => (MainViewModel?)GetValue(MainViewModelProperty);
        set => SetValue(MainViewModelProperty, value);
    }

    public DeveloperView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            var sec = string.IsNullOrEmpty(ViewModel.SelectedSection) ? "Git" : ViewModel.SelectedSection;
            _ = ViewModel.InitializeSectionAsync(sec);
        }
    }

    public Visibility GetBoolVisibility(bool isVisible) => isVisible ? Visibility.Visible : Visibility.Collapsed;
    public Visibility GetInvertedBoolVisibility(bool isVisible) => isVisible ? Visibility.Collapsed : Visibility.Visible;

    private void OnGitRepoPathKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter && ViewModel != null)
        {
            _ = ViewModel.RefreshGitCommand.ExecuteAsync(null);
            e.Handled = true;
        }
    }

    private async void OnBrowseGitRepoClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            picker.FileTypeFilter.Add("*");

            var window = App.MainWindowInstance;
            if (window != null)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            }

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null && ViewModel != null)
            {
                ViewModel.GitRepoPath = folder.Path;
                await ViewModel.RefreshGitCommand.ExecuteAsync(null);
            }
        }
        catch (Exception ex)
        {
            if (ViewModel != null)
            {
                ViewModel.GitStatusMessage = $"Browse folder error: {ex.Message}";
            }
        }
    }

    private static void OnViewModelChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DeveloperView view && e.NewValue is DeveloperViewModel vm)
        {
            vm.PropertyChanged += view.OnViewModelPropertyChanged;
        }
    }

    private async void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DeveloperViewModel.QrPngBytes) && ViewModel?.QrPngBytes != null)
        {
            try
            {
                var bitmap = new BitmapImage();
                using var stream = new InMemoryRandomAccessStream();
                await stream.WriteAsync(ViewModel.QrPngBytes.AsBuffer());
                stream.Seek(0);
                await bitmap.SetSourceAsync(stream);
                QrDisplayImage.Source = bitmap;
            }
            catch
            {
                // ignore image decode errors
            }
        }
    }

    private Visibility GetSectionVisibility(string current, string target) =>
        string.Equals(current, target, StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;

    private Visibility GetToolVisibility(string current, string target) =>
        string.Equals(current, target, StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;

    #region Navigation Clicks

    private void OnNavGitClicked(object sender, RoutedEventArgs e) => _ = ViewModel?.InitializeSectionAsync("Git");
    private void OnNavDockerClicked(object sender, RoutedEventArgs e) => _ = ViewModel?.InitializeSectionAsync("Docker");
    private void OnNavTerminalClicked(object sender, RoutedEventArgs e) => _ = ViewModel?.InitializeSectionAsync("Terminal");
    private void OnNavToolsClicked(object sender, RoutedEventArgs e) => _ = ViewModel?.InitializeSectionAsync("Tools");
    private void OnNavHttpClicked(object sender, RoutedEventArgs e) => _ = ViewModel?.InitializeSectionAsync("Http");
    private void OnNavLocalDevClicked(object sender, RoutedEventArgs e) => _ = ViewModel?.InitializeSectionAsync("LocalDev");
    private void OnNavProcessesClicked(object sender, RoutedEventArgs e) => _ = ViewModel?.InitializeSectionAsync("Processes");

    #endregion

    #region Tool Tabs Clicks

    private void OnToolJsonClicked(object sender, RoutedEventArgs e) { if (ViewModel != null) ViewModel.SelectedTool = "Json"; }
    private void OnToolJwtClicked(object sender, RoutedEventArgs e) { if (ViewModel != null) ViewModel.SelectedTool = "Jwt"; }
    private void OnToolEncodingClicked(object sender, RoutedEventArgs e) { if (ViewModel != null) ViewModel.SelectedTool = "Encoding"; }
    private void OnToolHashClicked(object sender, RoutedEventArgs e) { if (ViewModel != null) ViewModel.SelectedTool = "Hash"; }
    private void OnToolUuidClicked(object sender, RoutedEventArgs e) { if (ViewModel != null) ViewModel.SelectedTool = "Uuid"; }
    private void OnToolTimestampClicked(object sender, RoutedEventArgs e) { if (ViewModel != null) ViewModel.SelectedTool = "Timestamp"; }
    private void OnToolRegexClicked(object sender, RoutedEventArgs e) { if (ViewModel != null) ViewModel.SelectedTool = "Regex"; }
    private void OnToolQrClicked(object sender, RoutedEventArgs e) { if (ViewModel != null) ViewModel.SelectedTool = "Qr"; }

    #endregion

    #region Git Actions

    private void OnGitFileSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel != null && ViewModel.SelectedGitFile != null)
        {
            _ = ViewModel.ViewDiffAsync(ViewModel.SelectedGitFile);
        }
    }

    private void OnStageFileClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is GitFileStatus file && ViewModel != null)
        {
            _ = ViewModel.StageFileAsync(file);
        }
    }

    private void OnUnstageFileClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is GitFileStatus file && ViewModel != null)
        {
            _ = ViewModel.UnstageFileAsync(file);
        }
    }

    #endregion

    #region Docker Actions

    private void OnStartContainerClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DockerContainerInfo c && ViewModel != null)
            _ = ViewModel.StartContainerAsync(c);
    }

    private void OnStopContainerClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DockerContainerInfo c && ViewModel != null)
            _ = ViewModel.StopContainerAsync(c);
    }

    private void OnLogsContainerClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DockerContainerInfo c && ViewModel != null)
            _ = ViewModel.ViewContainerLogsAsync(c);
    }

    private void OnRemoveContainerClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DockerContainerInfo c && ViewModel != null)
            _ = ViewModel.RemoveContainerAsync(c);
    }

    private void OnRemoveImageClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DockerImageInfo img && ViewModel != null)
            _ = ViewModel.RemoveImageAsync(img);
    }

    #endregion

    #region Selectors

    private void OnTerminalTypeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && ViewModel != null)
        {
            ViewModel.SelectedTerminalType = cb.SelectedIndex switch
            {
                1 => TerminalType.PowerShell,
                2 => TerminalType.CommandPrompt,
                _ => TerminalType.WindowsTerminal
            };
        }
    }

    private void OnHashAlgorithmSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && ViewModel != null)
        {
            ViewModel.SelectedHashAlgorithm = cb.SelectedIndex switch
            {
                0 => DeveloperHashAlgorithm.MD5,
                1 => DeveloperHashAlgorithm.SHA1,
                2 => DeveloperHashAlgorithm.SHA256,
                3 => DeveloperHashAlgorithm.SHA384,
                4 => DeveloperHashAlgorithm.SHA512,
                _ => DeveloperHashAlgorithm.SHA256
            };
        }
    }

    private void OnHttpMethodSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedItem is ComboBoxItem item && ViewModel != null)
        {
            ViewModel.HttpMethod = item.Content?.ToString() ?? "GET";
        }
    }

    #endregion

    #region Local Dev & Processes

    private void OnOpenPortUrlClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ListeningPortInfo port && ViewModel != null)
        {
            ViewModel.OpenPortUrl(port);
        }
    }

    private void OnOpenProcessFolderClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DeveloperProcessInfo proc && ViewModel != null)
        {
            ViewModel.OpenProcessLocation(proc);
        }
    }

    private void OnTerminateProcessClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DeveloperProcessInfo proc && ViewModel != null)
        {
            _ = ViewModel.TerminateProcessAsync(proc);
        }
    }

    #endregion
}

