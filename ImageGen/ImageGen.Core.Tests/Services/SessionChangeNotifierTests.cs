using ImageGen.Core.Services;

namespace ImageGen.Core.Tests.Services;

public sealed class SessionChangeNotifierTests
{
    [Fact]
    public void Notify_RaisesSessionChangedWithId()
    {
        var notifier = new SessionChangeNotifier();
        var id = Guid.NewGuid();
        Guid? received = null;
        notifier.SessionChanged += (_, sessionId) => received = sessionId;

        notifier.Notify(id);

        Assert.Equal(id, received);
    }
}
