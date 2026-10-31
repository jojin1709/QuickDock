using QuickDock.Core.Events;
using Xunit;

namespace QuickDock.Core.Tests;

public class EventBusTests
{
    private sealed record TestSampleEvent(string Message, DateTimeOffset Timestamp) : IEvent
    {
        public TestSampleEvent(string message) : this(message, DateTimeOffset.UtcNow) { }
    }

    [Fact]
    public async Task PublishAsync_DeliversEventToSubscriber()
    {
        var bus = new InMemoryEventBus();
        TestSampleEvent? received = null;

        using var sub = bus.Subscribe<TestSampleEvent>((e, ct) =>
        {
            received = e;
            return Task.CompletedTask;
        });

        var sentEvent = new TestSampleEvent("Hello EventBus");
        await bus.PublishAsync(sentEvent);

        Assert.NotNull(received);
        Assert.Equal("Hello EventBus", received.Message);
    }

    [Fact]
    public async Task PublishAsync_DeliversToMultipleSubscribers()
    {
        var bus = new InMemoryEventBus();
        int sub1Count = 0;
        int sub2Count = 0;

        using var sub1 = bus.Subscribe<TestSampleEvent>((_, _) =>
        {
            Interlocked.Increment(ref sub1Count);
            return Task.CompletedTask;
        });

        using var sub2 = bus.Subscribe<TestSampleEvent>((_, _) =>
        {
            Interlocked.Increment(ref sub2Count);
            return Task.CompletedTask;
        });

        await bus.PublishAsync(new TestSampleEvent("Broadcast"));

        Assert.Equal(1, sub1Count);
        Assert.Equal(1, sub2Count);
    }

    [Fact]
    public async Task DisposeSubscription_StopsDeliveringEvents()
    {
        var bus = new InMemoryEventBus();
        int callCount = 0;

        var sub = bus.Subscribe<TestSampleEvent>((_, _) =>
        {
            callCount++;
            return Task.CompletedTask;
        });

        await bus.PublishAsync(new TestSampleEvent("First"));
        Assert.Equal(1, callCount);

        sub.Dispose();

        await bus.PublishAsync(new TestSampleEvent("Second"));
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task PublishAsync_IsolatesSubscriberExceptions()
    {
        var bus = new InMemoryEventBus();
        bool secondSubscriberRan = false;

        using var faultySub = bus.Subscribe<TestSampleEvent>((_, _) =>
        {
            throw new InvalidOperationException("Simulated subscriber crash");
        });

        using var goodSub = bus.Subscribe<TestSampleEvent>((_, _) =>
        {
            secondSubscriberRan = true;
            return Task.CompletedTask;
        });

        // Must not throw exception
        var exception = await Record.ExceptionAsync(() => bus.PublishAsync(new TestSampleEvent("Test")));
        Assert.Null(exception);
        Assert.True(secondSubscriberRan);
    }

    [Fact]
    public async Task PublishAsync_RespectsCancellationToken()
    {
        var bus = new InMemoryEventBus();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        bool subscriberRan = false;
        using var sub = bus.Subscribe<TestSampleEvent>((_, ct) =>
        {
            subscriberRan = true;
            return Task.CompletedTask;
        });

        await bus.PublishAsync(new TestSampleEvent("Canceled"), cts.Token);
        Assert.False(subscriberRan);
    }
}

