using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using QuickDock.App.ViewModels;
using QuickDock.Core.Events;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Modules;
using QuickDock.Core.Settings;
using QuickDock.Infrastructure.Logging;
using QuickDock.Infrastructure.Services;
using QuickDock.Infrastructure.Storage;
using QuickDock.Infrastructure.Windows;
using QuickDock.Modules.Clock;
using QuickDock.Services.System;

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

                // Windows Shell & Native Services
                services.AddSingleton<IMonitorService, MonitorService>();
                services.AddSingleton<IStartupService, StartupService>();
                services.AddSingleton<IFullscreenService, FullscreenDetectionService>();
                services.AddSingleton<INotificationService, NotificationService>();
                services.AddSingleton<ISystemMetricsService, SystemMetricsService>();
                services.AddSingleton<IWindowPositioningService, WindowPositioningService>();
                services.AddSingleton<IGlobalHotkeyService, GlobalHotkeyService>();
                services.AddSingleton<ISystemTrayService, SystemTrayService>();

                // ViewModels
                services.AddSingleton<HomeViewModel>();
                services.AddSingleton<SettingsViewModel>();
                services.AddSingleton<MainViewModel>();

                // Modules
                services.AddSingleton<IQuickDockModule, ClockModule>();

                services.AddTransient<MainWindow>(sp =>
                {
                    _logger?.LogInformation("MainWindow factory lambda starting...");
                    var vm = sp.GetRequiredService<MainViewModel>();
                    var pos = sp.GetRequiredService<IWindowPositioningService>();
                    var mon = sp.GetRequiredService<IMonitorService>();
                    var full = sp.GetRequiredService<IFullscreenService>();
                    var start = sp.GetRequiredService<IStartupService>();
                    var notif = sp.GetRequiredService<INotificationService>();
                    var hotkey = sp.GetRequiredService<IGlobalHotkeyService>();
                    var tray = sp.GetRequiredService<ISystemTrayService>();
                    var settings = sp.GetRequiredService<ISettingsManager>();
                    var bus = sp.GetRequiredService<IEventBus>();
                    var logger = sp.GetRequiredService<ILogger<MainWindow>>();

                    return new MainWindow(vm, pos, mon, full, start, notif, hotkey, tray, settings, bus, logger);
                });
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
        try
        {
            var lifecycle = Services.GetRequiredService<IAppLifecycleService>();
            await lifecycle.StartAsync();

            _logger?.LogInformation("Resolving MainViewModel...");
            var vm = Services.GetRequiredService<MainViewModel>();
            _logger?.LogInformation("MainViewModel resolved.");

            _logger?.LogInformation("Resolving MainWindow...");
            _window = Services.GetRequiredService<MainWindow>();
            _logger?.LogInformation("MainWindow resolved.");

            _window.Closed += async (s, e) =>
            {
                await lifecycle.StopAsync();
            };

            _window.Activate();
            _logger?.LogInformation("QuickDock MainWindow activated successfully.");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to launch QuickDock MainWindow.");
        }
    }
}
