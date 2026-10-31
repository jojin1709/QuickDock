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
using QuickDock.Modules.Battery;
using QuickDock.Modules.Clock;
using QuickDock.Modules.Media;
using QuickDock.Modules.Network;
using QuickDock.Modules.System;
using QuickDock.Modules.Volume;
using QuickDock.Modules.Productivity;
using QuickDock.Modules.Files;
using QuickDock.Modules.Developer;
using QuickDock.Modules.NetworkSecurity;
using QuickDock.Services.Storage;
using QuickDock.Services.Audio;
using QuickDock.Services.Clipboard;
using QuickDock.Services.Files;
using QuickDock.Services.Developer;
using QuickDock.Services.NetworkSecurity;
using QuickDock.Services.Media;
using QuickDock.Services.Productivity;
using QuickDock.Services.QuickDrop;
using QuickDock.Services.ScreenTime;
using QuickDock.Services.System;
using QuickDock.Services.Time;
using QuickDock.Services.Weather;
using System.Net.Http;

namespace QuickDock.App;

public partial class App : Application
{
    private Window? _window;
    private ILogger<App>? _logger;

    public static Window? MainWindowInstance => (Current as App)?._window;
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
                services.AddSingleton<IStorageManagerService, StorageManagerService>();
                services.AddSingleton<IShortcutManagerService, ShortcutManagerService>();
                services.AddSingleton<ISystemMetricsService, SystemMetricsService>();
                services.AddSingleton<IAudioService, CoreAudioService>();
                services.AddSingleton<IMediaService, WindowsMediaService>();
                services.AddSingleton<IWindowPositioningService, WindowPositioningService>();
                services.AddSingleton<IGlobalHotkeyService, GlobalHotkeyService>();
                services.AddSingleton<ISystemTrayService, SystemTrayService>();
                services.AddSingleton<IPerformanceDiagnosticsService, PerformanceDiagnosticsService>();

                // ClockModule registered directly and as IQuickDockModule
                services.AddSingleton<ClockModule>();
                services.AddSingleton<IQuickDockModule>(sp => sp.GetRequiredService<ClockModule>());

                // Core Utility Modules
                services.AddSingleton<IQuickDockModule, SystemModule>();
                services.AddSingleton<IQuickDockModule, VolumeModule>();
                services.AddSingleton<IQuickDockModule, MediaModule>();
                services.AddSingleton<IQuickDockModule, BatteryModule>();
                services.AddSingleton<IQuickDockModule, NetworkModule>();

                // Phase 5 Productivity Services & Infrastructure
                services.AddSingleton<HttpClient>();
                services.AddSingleton<IDatabaseService, SqliteDatabaseService>();
                services.AddSingleton<ISensitiveDataDetector, SensitiveDataDetector>();
                services.AddSingleton<IClipboardService, WindowsClipboardService>();
                services.AddSingleton<ITimerService, TimerService>();
                services.AddSingleton<IStopwatchService, StopwatchService>();
                services.AddSingleton<IPomodoroService, PomodoroService>();
                services.AddSingleton<INotesService, NotesService>();
                services.AddSingleton<ICalendarService, CalendarService>();
                services.AddSingleton<IWeatherProvider, OpenMeteoWeatherProvider>();
                services.AddSingleton<IWeatherService, WeatherService>();
                services.AddSingleton<ISavedLinksService, SavedLinksService>();
                services.AddSingleton<ITemporaryMessageService, TemporaryMessageService>();
                services.AddSingleton<IScreenTimeService, ScreenTimeService>();

                // Phase 5 Productivity Modules
                services.AddSingleton<IQuickDockModule, ClipboardModule>();
                services.AddSingleton<IQuickDockModule, TimerModule>();
                services.AddSingleton<IQuickDockModule, NotesModule>();
                services.AddSingleton<IQuickDockModule, CalendarModule>();
                services.AddSingleton<IQuickDockModule, WeatherModule>();
                services.AddSingleton<IQuickDockModule, LinksModule>();
                services.AddSingleton<IQuickDockModule, TemporaryMessageModule>();
                services.AddSingleton<IQuickDockModule, ScreenTimeModule>();

                // Phase 6 File & Transfer Services
                services.AddSingleton<IFileHashService, FileHashService>();
                services.AddSingleton<IFilePreviewService, FilePreviewService>();
                services.AddSingleton<IFileShelfService, FileShelfService>();
                services.AddSingleton<IFileConversionService, FileConversionService>();
                services.AddSingleton<IFileTransferManager, FileTransferManager>();
                services.AddSingleton<IQuickDropService, QuickDropService>();

                // Phase 6 File & Transfer Modules
                services.AddSingleton<IQuickDockModule, FileShelfModule>();
                services.AddSingleton<IQuickDockModule, QuickDropModule>();
                services.AddSingleton<IQuickDockModule, FileConverterModule>();
                services.AddSingleton<IQuickDockModule, TransferManagerModule>();

                // Phase 7 Developer Services
                services.AddSingleton<IGitService, GitService>();
                services.AddSingleton<IDockerService, DockerService>();
                services.AddSingleton<ITerminalLauncherService, TerminalLauncherService>();
                services.AddSingleton<IJsonToolkitService, JsonToolkitService>();
                services.AddSingleton<IJwtToolkitService, JwtToolkitService>();
                services.AddSingleton<IEncodingToolkitService, EncodingToolkitService>();
                services.AddSingleton<IDeveloperHashService, DeveloperHashService>();
                services.AddSingleton<IUuidToolkitService, UuidToolkitService>();
                services.AddSingleton<ITimestampToolkitService, TimestampToolkitService>();
                services.AddSingleton<IRegexToolkitService, RegexToolkitService>();
                services.AddSingleton<IQrGeneratorService, QrGeneratorService>();
                services.AddSingleton<IHttpToolkitService, HttpToolkitService>();
                services.AddSingleton<ILocalDevelopmentService, LocalDevelopmentService>();
                services.AddSingleton<IDeveloperProcessService, DeveloperProcessService>();

                // Phase 7 Developer Modules
                services.AddSingleton<IQuickDockModule, GitModule>();
                services.AddSingleton<IQuickDockModule, DockerModule>();
                services.AddSingleton<IQuickDockModule, TerminalModule>();
                services.AddSingleton<IQuickDockModule, DeveloperToolsModule>();
                services.AddSingleton<IQuickDockModule, HttpToolkitModule>();
                services.AddSingleton<IQuickDockModule, LocalDevModule>();
                services.AddSingleton<IQuickDockModule, ProcessMonitorModule>();

                // Phase 8 Network & Security Services
                services.AddSingleton<INetworkOverviewService, NetworkOverviewService>();
                services.AddSingleton<IDnsDiagnosticService, DnsDiagnosticService>();
                services.AddSingleton<IConnectivityService, ConnectivityService>();
                services.AddSingleton<INetworkConnectionService, NetworkConnectionService>();
                services.AddSingleton<IRoutingNeighborService, RoutingNeighborService>();
                services.AddSingleton<ITlsInspectionService, TlsInspectionService>();
                services.AddSingleton<IHttpSecurityService, HttpSecurityService>();
                services.AddSingleton<IIpDomainService, IpDomainService>();
                services.AddSingleton<IFirewallStatusService, FirewallStatusService>();
                services.AddSingleton<IWindowsSecurityService, WindowsSecurityService>();
                services.AddSingleton<ILocalSecurityCheckService, LocalSecurityCheckService>();
                services.AddSingleton<IFileIntegrityService, FileIntegrityService>();
                services.AddSingleton<ISecurityReportService, SecurityReportService>();

                // Phase 8 Network & Security Modules
                services.AddSingleton<IQuickDockModule, NetworkDiagnosticsModule>();
                services.AddSingleton<IQuickDockModule, SecurityDiagnosticsModule>();
                services.AddSingleton<IQuickDockModule, TlsInspectorModule>();
                services.AddSingleton<IQuickDockModule, HttpSecurityModule>();
                services.AddSingleton<IQuickDockModule, FirewallStatusModule>();

                // ViewModels
                services.AddSingleton<HomeViewModel>();
                services.AddSingleton<SystemViewModel>();
                services.AddSingleton<SettingsViewModel>();
                services.AddSingleton<ClipboardViewModel>();
                services.AddSingleton<TimerViewModel>();
                services.AddSingleton<NotesViewModel>();
                services.AddSingleton<CalendarViewModel>();
                services.AddSingleton<WeatherViewModel>();
                services.AddSingleton<SavedLinksViewModel>();
                services.AddSingleton<ScreenTimeViewModel>();
                services.AddSingleton<FileShelfViewModel>();
                services.AddSingleton<QuickDropViewModel>();
                services.AddSingleton<FileConverterViewModel>();
                services.AddSingleton<TransferManagerViewModel>();
                services.AddSingleton<DeveloperViewModel>();
                services.AddSingleton<NetworkSecurityViewModel>();
                services.AddSingleton<MainViewModel>();

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
            e.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            _logger?.LogError(e.Exception, "Unobserved task exception caught in background thread.");
            e.SetObserved();
        };
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            var lifecycle = Services.GetRequiredService<IAppLifecycleService>();
            await lifecycle.StartAsync();

            var database = Services.GetRequiredService<IDatabaseService>();
            await database.InitializeAsync();

            var clipboard = Services.GetRequiredService<IClipboardService>();
            await clipboard.StartListeningAsync();

            var screenTime = Services.GetRequiredService<IScreenTimeService>();
            if (Services.GetRequiredService<ISettingsManager>().CurrentSettings.ScreenTime.TrackingEnabled)
            {
                screenTime.StartTracking();
            }

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
