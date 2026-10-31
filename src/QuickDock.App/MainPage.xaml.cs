using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using QuickDock.Core.Events;
using QuickDock.Core.Modules;
using QuickDock.Core.Settings;
using QuickDock.Infrastructure.Storage;

namespace QuickDock.App;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var services = App.Services;
        var eventBus = services.GetService<IEventBus>();
        var settingsManager = services.GetService<ISettingsManager>();
        var moduleManager = services.GetService<IModuleManager>();

        DiStatusText.Text = "Host & Services Active";
        EventBusStatusText.Text = eventBus != null ? "Active (InMemoryEventBus)" : "Failed";
        SettingsStatusText.Text = settingsManager != null ? $"Config Loaded ({settingsManager.CurrentSettings.Appearance.Theme})" : "Failed";

        if (moduleManager != null)
        {
            var modules = moduleManager.Modules;
            ModulesStatusText.Text = $"{modules.Count} registered, {modules.Count(m => m.State == ModuleState.Initialized || m.State == ModuleState.Running)} active";
            ModulesListView.ItemsSource = modules;
        }

        StoragePathText.Text = $"App Data: {AppStoragePaths.RootDirectory}";
    }
}
