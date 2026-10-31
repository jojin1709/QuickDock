namespace QuickDock.Core.Events;

/// <summary>
/// Marker interface for all strongly-typed QuickDock events.
/// </summary>
public interface IEvent
{
    /// <summary>
    /// The UTC timestamp when the event occurred.
    /// </summary>
    DateTimeOffset Timestamp { get; }
}
