using ImageGen.Core.Services;
using ImageGen.Core.Tests.TestDoubles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImageGen.Core.Tests.Services;

public sealed class SessionChangeNotifierTests
{
    private const string PrivateText = "секретный текст провайдера";

    [Fact]
    public void Notify_RaisesSessionChangedWithId()
    {
        var notifier = new SessionChangeNotifier(NullLogger<SessionChangeNotifier>.Instance);
        var id = Guid.NewGuid();
        Guid? received = null;
        notifier.SessionChanged += (_, sessionId) => received = sessionId;

        notifier.Notify(id);

        Assert.Equal(id, received);
    }

    [Fact]
    public void Notify_WithoutSubscribers_DoesNothing()
    {
        var notifier = new SessionChangeNotifier(NullLogger<SessionChangeNotifier>.Instance);

        notifier.Notify(Guid.NewGuid());
    }

    [Fact]
    public void Notify_HandlerThrows_OtherHandlersStillCalled()
    {
        var logger = new CapturingLogger<SessionChangeNotifier>();
        var notifier = new SessionChangeNotifier(logger);
        var id = Guid.NewGuid();
        var calls = new List<(object? Sender, Guid Id)>();
        notifier.SessionChanged += (sender, sessionId) => calls.Add((sender, sessionId));
        notifier.SessionChanged += (_, _) => throw new InvalidOperationException(PrivateText);
        notifier.SessionChanged += (sender, sessionId) => calls.Add((sender, sessionId));

        notifier.Notify(id);

        // The throwing handler does not stop the others, and the exception does not reach the caller.
        Assert.Equal(2, calls.Count);
        Assert.All(calls, call => Assert.Equal(id, call.Id));
        Assert.All(calls, call => Assert.Same(notifier, call.Sender));

        // The operational entry carries the session id only; the details stay at Debug.
        foreach (var entry in logger.Entries.Where(entry => entry.Level > LogLevel.Debug))
        {
            Assert.Null(entry.Exception);
            Assert.DoesNotContain(PrivateText, entry.Message, StringComparison.Ordinal);
        }

        var debug = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Debug);
        Assert.NotNull(debug.Exception);
        Assert.Contains(PrivateText, debug.Exception!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Notify_SubscribeDuringHandler_AffectsNextCallOnly()
    {
        var notifier = new SessionChangeNotifier(NullLogger<SessionChangeNotifier>.Instance);
        var id = Guid.NewGuid();
        var late = 0;
        notifier.SessionChanged += (_, _) => notifier.SessionChanged += (_, _) => late++;

        notifier.Notify(id);
        Assert.Equal(0, late);

        notifier.Notify(id);
        Assert.Equal(1, late);
    }
}
