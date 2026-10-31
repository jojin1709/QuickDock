using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Modules;
using QuickDock.Core.Settings;
using Serilog;

namespace QuickDock.Infrastructure.Services;

/// <summary>
/// Controls the graceful lifecycle orchestration of QuickDock.
/// </summary>
public sealed class AppLifecycleService : IAppLifecycleService
{
    private readonly ISettingsManager _settingsManager;
    private readonly IModuleManager _moduleManager;
    private readonly ILogger<AppLifecycleService> _logger;
    private int _isShuttingDown;

    public bool IsShuttingDown => Interlocked.CompareExchange(ref _isShuttingDown, 0, 0) == 1;

    public event EventHandler? ShutdownRequested;

    public AppLifecycleService(
        ISettingsManager settingsManager,
        IModuleManager moduleManager,
        ILogger<AppLifecycleService> logger)
    {
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _moduleManager = moduleManager ?? throw new ArgumentNullException(nameof(moduleManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("QuickDock application starting up...");

        // 1. Load settings
        var settings = await _settingsManager.LoadAsync(cancellationToken).ConfigureAwait(false);

        // 2. Sync enabled flags to registered modules
        foreach (var module in _moduleManager.Modules)
        {
            if (settings.EnabledModules.TryGetValue(module.Id, out var isEnabled))
            {
                module.IsEnabled = isEnabled;
            }
        }

        // 3. Initialize all enabled modules (lazy start happens on demand)
        await _moduleManager.InitializeAllEnabledAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("QuickDock core startup sequence completed successfully.");
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _isShuttingDown, 1) == 1)
        {
            return;
        }

        _logger.LogInformation("Initiating graceful shutdown for QuickDock...");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            // Stop all active modules
            await _moduleManager.StopAllRunningAsync(cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error encountered while stopping modules during shutdown.");
        }

        try
        {
            // Flush Serilog buffer
            Log.CloseAndFlush();
        }
        catch
        {
            // Best effort log flush during shutdown
        }

        _logger.LogInformation("Graceful shutdown completed.");
    }

    public void RequestShutdown()
    {
        _logger.LogInformation("Shutdown requested by application host.");
        ShutdownRequested?.Invoke(this, EventArgs.Empty);
    }
}

