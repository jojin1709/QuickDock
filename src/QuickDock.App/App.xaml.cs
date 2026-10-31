using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using QuickDock.Core.Events;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Modules;
using QuickDock.Core.Settings;
using QuickDock.Infrastructure.Logging;
using QuickDock.Infrastructure.Services;
using QuickDock.Infrastructure.Storage;
using QuickDock.Modules.Clock;

namespace QuickDock.App;

public partial class App : Application
{
    private Window? _window;
    private ILogger<App>? _logger;

    public static IHost Host { get; private set; } = null!;
    public static IServiceProvider Services => Host.Services;

    public App()
    {
        InitializeComponent();

        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureLogging((context, logging) =>
            {
                QuickDockLogging.ConfigureLogging(logging);
            })
            .ConfigureServices((context, services) =>
            {
                AppStoragePaths.EnsureDirectoriesExist();

                services.AddSingleton<IEventBus, InMemoryEventBus>();
                services.AddSingleton<ISettingsManager, JsonSettingsManager>();
                services.AddSingleton<IModuleManager, ModuleManager>();
                services.AddSingleton<IAppLifecycleService, AppLifecycleService>();

                services.AddSingleton<IQuickDockModule, ClockModule>();

                services.AddTransient<MainWindow>();
                services.AddTransient<MainPage>();
            })
            .Build();

        _logger = Services.GetService<ILogger<App>>();

        UnhandledException += (s, e) =>
        {
            _logger?.LogError(e.Exception, "Unhandled application exception caught at UI boundary.");
        };
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var lifecycle = Services.GetRequiredService<IAppLifecycleService>();
        await lifecycle.StartAsync().ConfigureAwait(true);

        _window = Services.GetRequiredService<MainWindow>();
        _window.Closed += async (s, e) =>
        {
            await lifecycle.StopAsync().ConfigureAwait(false);
        };

        _window.Activate();
    }
}
