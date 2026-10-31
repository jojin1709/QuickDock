using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace QuickDock.Core.Events;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IEventBus"/>.
/// Handles isolated subscriber execution and clean disposal.
/// </summary>
public sealed class InMemoryEventBus : IEventBus
{
    private readonly ILogger<InMemoryEventBus>? _logger;
    private readonly ConcurrentDictionary<Type, List<DelegateHandler>> _subscriptions = new();
    private readonly object _lock = new();

    public InMemoryEventBus(ILogger<InMemoryEventBus>? logger = null)
    {
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent @eventData, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent
    {
        ArgumentNullException.ThrowIfNull(eventData);

        var eventType = typeof(TEvent);
        if (!_subscriptions.TryGetValue(eventType, out var handlers) || handlers.Count == 0)
        {
            return;
        }

        DelegateHandler[] handlersCopy;
        lock (_lock)
        {
            handlersCopy = handlers.ToArray();
        }

        foreach (var handler in handlersCopy)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                if (handler.Action is Func<TEvent, CancellationToken, Task> typedHandler)
                {
                    await typedHandler(eventData, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger?.LogDebug("Event handler execution canceled for {EventType}", eventType.Name);
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unhandled exception in event handler for {EventType}", eventType.Name);
            }
        }
    }

    public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : class, IEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        var eventType = typeof(TEvent);
        var delegateHandler = new DelegateHandler(handler);

        lock (_lock)
        {
            var list = _subscriptions.GetOrAdd(eventType, _ => new List<DelegateHandler>());
            list.Add(delegateHandler);
        }

        return new SubscriptionToken(() =>
        {
            lock (_lock)
            {
                if (_subscriptions.TryGetValue(eventType, out var list))
                {
                    list.Remove(delegateHandler);
                }
            }
        });
    }

    private sealed class DelegateHandler
    {
        public Delegate Action { get; }

        public DelegateHandler(Delegate action)
        {
            Action = action;
        }
    }

    private sealed class SubscriptionToken : IDisposable
    {
        private Action? _unsubscribe;

        public SubscriptionToken(Action unsubscribe)
        {
            _unsubscribe = unsubscribe;
        }

        public void Dispose()
        {
            var action = Interlocked.Exchange(ref _unsubscribe, null);
            action?.Invoke();
        }
    }
}

