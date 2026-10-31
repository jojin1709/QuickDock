using Microsoft.Extensions.Logging;

namespace QuickDock.Core.Modules;

/// <summary>
/// Safe base class for QuickDock modules providing thread-safe lifecycle state transitions and crash isolation.
/// </summary>
public abstract class ModuleBase : IQuickDockModule
{
    private readonly object _stateLock = new();
    private ModuleState _state = ModuleState.Uninitialized;
    private Exception? _lastError;
    private bool _isEnabled = true;

    protected ILogger Logger { get; }

    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract ModuleCategory Category { get; }

    public ModuleState State
    {
        get
        {
            lock (_stateLock)
            {
                return _state;
            }
        }
        private set
        {
            lock (_stateLock)
            {
                _state = value;
            }
        }
    }

    public bool IsEnabled
    {
        get
        {
            lock (_stateLock)
            {
                return _isEnabled;
            }
        }
        set
        {
            lock (_stateLock)
            {
                _isEnabled = value;
            }
        }
    }

    public Exception? LastError
    {
        get
        {
            lock (_stateLock)
            {
                return _lastError;
            }
        }
        private set
        {
            lock (_stateLock)
            {
                _lastError = value;
            }
        }
    }

    protected ModuleBase(ILogger logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (_state != ModuleState.Uninitialized && _state != ModuleState.Stopped)
            {
                return;
            }
            _state = ModuleState.Initializing;
        }

        try
        {
            await OnInitializeAsync(cancellationToken).ConfigureAwait(false);
            State = ModuleState.Initialized;
            LastError = null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            State = ModuleState.Uninitialized;
            Logger.LogInformation("Module {ModuleId} initialization canceled", Id);
            throw;
        }
        catch (Exception ex)
        {
            LastError = ex;
            State = ModuleState.Faulted;
            Logger.LogError(ex, "Module {ModuleId} failed during initialization", Id);
            throw;
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (!_isEnabled || _state == ModuleState.Running || _state == ModuleState.Starting)
            {
                return;
            }
            if (_state == ModuleState.Uninitialized)
            {
                throw new InvalidOperationException($"Module {Id} must be initialized before starting.");
            }
            _state = ModuleState.Starting;
        }

        try
        {
            await OnStartAsync(cancellationToken).ConfigureAwait(false);
            State = ModuleState.Running;
            LastError = null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            State = ModuleState.Initialized;
            Logger.LogInformation("Module {ModuleId} start canceled", Id);
            throw;
        }
        catch (Exception ex)
        {
            LastError = ex;
            State = ModuleState.Faulted;
            Logger.LogError(ex, "Module {ModuleId} failed during start", Id);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (_state != ModuleState.Running && _state != ModuleState.Starting)
            {
                return;
            }
            _state = ModuleState.Stopping;
        }

        try
        {
            await OnStopAsync(cancellationToken).ConfigureAwait(false);
            State = ModuleState.Stopped;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            State = ModuleState.Stopped;
            Logger.LogInformation("Module {ModuleId} stop canceled", Id);
        }
        catch (Exception ex)
        {
            LastError = ex;
            State = ModuleState.Faulted;
            Logger.LogError(ex, "Module {ModuleId} failed during stop", Id);
            throw;
        }
    }

    protected virtual Task OnInitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected virtual Task OnStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected virtual Task OnStopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
