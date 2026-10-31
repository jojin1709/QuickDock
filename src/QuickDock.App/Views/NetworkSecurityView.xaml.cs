using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QuickDock.App.ViewModels;

namespace QuickDock.App.Views;

public sealed partial class NetworkSecurityView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(NetworkSecurityViewModel),
            typeof(NetworkSecurityView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty MainViewModelProperty =
        DependencyProperty.Register(
            nameof(MainViewModel),
            typeof(MainViewModel),
            typeof(NetworkSecurityView),
            new PropertyMetadata(null));

    public NetworkSecurityViewModel? ViewModel
    {
        get => (NetworkSecurityViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public MainViewModel? MainViewModel
    {
        get => (MainViewModel?)GetValue(MainViewModelProperty);
        set => SetValue(MainViewModelProperty, value);
    }

    public NetworkSecurityView()
    {
        InitializeComponent();
    }

    private Visibility GetSectionVisibility(string currentSection, string targetSection)
    {
        return string.Equals(currentSection, targetSection, StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void OnNavOverviewClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null) _ = ViewModel.InitializeSectionAsync("Overview");
    }

    private void OnNavAdaptersClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null) _ = ViewModel.InitializeSectionAsync("Adapters");
    }

    private void OnNavDnsPingClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null) _ = ViewModel.InitializeSectionAsync("DnsPing");
    }

    private void OnNavConnectionsClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null) _ = ViewModel.InitializeSectionAsync("Connections");
    }

    private void OnNavTlsClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null) _ = ViewModel.InitializeSectionAsync("Tls");
    }

    private void OnNavHeadersClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null) _ = ViewModel.InitializeSectionAsync("Headers");
    }

    private void OnNavFirewallClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null) _ = ViewModel.InitializeSectionAsync("Firewall");
    }

    private void OnNavPostureClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null) _ = ViewModel.InitializeSectionAsync("Posture");
    }

    private void OnNavIntegrityClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null) _ = ViewModel.InitializeSectionAsync("Integrity");
    }

    private void OnNavReportClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null) _ = ViewModel.InitializeSectionAsync("Report");
    }
}

