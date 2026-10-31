namespace QuickDock.Core.Events;

/// <summary>
/// A thread-safe, strongly-typed asynchronous event bus for decoupled component communication.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publishes an event to all subscribed handlers asynchronously.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="eventData">The event payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync<TEvent>(TEvent @eventData, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent;

    /// <summary>
    /// Subscribes a handler for a given event type.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="handler">The asynchronous handler function.</param>
    /// <returns>An IDisposable subscription handle.</returns>
    IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : class, IEvent;
}
