using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Events;

namespace QuickDock.Core.Modules;

/// <summary>
/// Implements the QuickDock module management system with strict crash isolation.
/// </summary>
public sealed class ModuleManager : IModuleManager
{
    private readonly ConcurrentDictionary<string, IQuickDockModule> _modules = new(StringComparer.OrdinalIgnoreCase);
    private readonly IEventBus _eventBus;
    private readonly ILogger<ModuleManager> _logger;

    public IReadOnlyList<IQuickDockModule> Modules => _modules.Values.ToList();

    public ModuleManager(IEventBus eventBus, ILogger<ModuleManager> logger, IEnumerable<IQuickDockModule>? initialModules = null)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (initialModules != null)
        {
            foreach (var module in initialModules)
            {
                RegisterModule(module);
            }
        }
    }

    public IQuickDockModule? GetModule(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        _modules.TryGetValue(moduleId, out var module);
        return module;
    }

    public void RegisterModule(IQuickDockModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        if (!_modules.TryAdd(module.Id, module))
        {
            _logger.LogWarning("Module with ID '{ModuleId}' is already registered. Skipping duplicate.", module.Id);
        }
        else
        {
            _logger.LogInformation("Registered module: {ModuleName} ({ModuleId})", module.Name, module.Id);
        }
    }

    public async Task InitializeAllEnabledAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing all enabled modules...");

        foreach (var module in _modules.Values)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (!module.IsEnabled)
            {
                _logger.LogDebug("Module {ModuleId} is disabled, skipping initialization.", module.Id);
                continue;
            }

            var oldState = module.State;
            try
            {
                await module.InitializeAsync(cancellationToken).ConfigureAwait(false);
                await _eventBus.PublishAsync(new ModuleStateChangedEvent(module.Id, oldState, module.State), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Crash isolation: Module '{ModuleId}' threw during initialization. QuickDock remains stable.", module.Id);
                await _eventBus.PublishAsync(new ModuleStateChangedEvent(module.Id, oldState, ModuleState.Faulted, ex.Message), cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    public async Task<bool> StartModuleAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        var module = GetModule(moduleId);
        if (module == null)
        {
            _logger.LogWarning("Cannot start module '{ModuleId}': Not found.", moduleId);
            return false;
        }

        if (!module.IsEnabled)
        {
            _logger.LogWarning("Cannot start module '{ModuleId}': Module is disabled.", moduleId);
            return false;
        }

        var oldState = module.State;
        try
        {
            if (module.State == ModuleState.Uninitialized)
            {
                await module.InitializeAsync(cancellationToken).ConfigureAwait(false);
            }

            await module.StartAsync(cancellationToken).ConfigureAwait(false);
            await _eventBus.PublishAsync(new ModuleStateChangedEvent(module.Id, oldState, module.State), cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Crash isolation: Module '{ModuleId}' threw during start. QuickDock remains stable.", module.Id);
            await _eventBus.PublishAsync(new ModuleStateChangedEvent(module.Id, oldState, ModuleState.Faulted, ex.Message), cancellationToken)
                .ConfigureAwait(false);
            return false;
        }
    }

    public async Task<bool> StopModuleAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        var module = GetModule(moduleId);
        if (module == null)
        {
            return false;
        }

        var oldState = module.State;
        try
        {
            await module.StopAsync(cancellationToken).ConfigureAwait(false);
            await _eventBus.PublishAsync(new ModuleStateChangedEvent(module.Id, oldState, module.State), cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Crash isolation: Module '{ModuleId}' threw during stop. QuickDock remains stable.", module.Id);
            await _eventBus.PublishAsync(new ModuleStateChangedEvent(module.Id, oldState, ModuleState.Faulted, ex.Message), cancellationToken)
                .ConfigureAwait(false);
            return false;
        }
    }

    public async Task StopAllRunningAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping all active modules...");

        foreach (var module in _modules.Values)
        {
            if (module.State == ModuleState.Running || module.State == ModuleState.Starting)
            {
                await StopModuleAsync(module.Id, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task EnableModuleAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        var module = GetModule(moduleId);
        if (module == null)
        {
            return;
        }

        module.IsEnabled = true;
        if (module.State == ModuleState.Uninitialized)
        {
            try
            {
                await module.InitializeAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize newly enabled module '{ModuleId}'", moduleId);
            }
        }
    }

    public async Task DisableModuleAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        var module = GetModule(moduleId);
        if (module == null)
        {
            return;
        }

        if (module.State == ModuleState.Running || module.State == ModuleState.Starting)
        {
            await StopModuleAsync(moduleId, cancellationToken).ConfigureAwait(false);
        }

        module.IsEnabled = false;
    }
}

