using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ImageGen.Avalonia.Services;
using Microsoft.Extensions.Logging;

namespace ImageGen.Avalonia.Tests.Services;

public sealed class AvaloniaMediaPlayerServiceTests
{
    [AvaloniaFact]
    public async Task ShowAsync_SecondWindowClosesFirstAndDisposesPlayback()
    {
        var owner = new Window();
        owner.Show();
        var engine = new FakeMediaEngine();
        var service = new AvaloniaMediaPlayerService(() => owner, engine, Substitute.For<ILogger>());

        var first = service.ShowAsync("first.mp4", "First", CancellationToken.None);
        var firstPlayback = Assert.IsType<FakePlayback>(engine.Created[0]);
        var second = service.ShowAsync("second.mp4", "Second", CancellationToken.None);

        await first.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(firstPlayback.Stopped);
        Assert.True(firstPlayback.Disposed);
        await WaitUntilAsync(() => engine.Created.Count == 2);
        Assert.Equal(2, engine.Created.Count);

        var secondPlayback = Assert.IsType<FakePlayback>(engine.Created[1]);
        service.CloseCurrent();
        await second.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(secondPlayback.Stopped);
        Assert.True(secondPlayback.Disposed);
        owner.Close();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(10, timeout.Token);
    }

    [AvaloniaFact]
    public void IsSupported_InitializationFails_ReturnsFalseAndLogsWarningOnce()
    {
        var logger = Substitute.For<ILogger>();
        var service = new AvaloniaMediaPlayerService(() => null, new FakeMediaEngine(initializationSucceeds: false), logger);

        Assert.False(service.IsSupported);
        Assert.False(service.IsSupported);
        logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<Arg.AnyType>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<Arg.AnyType, Exception?, string>>());
    }

    private sealed class FakeMediaEngine(bool initializationSucceeds = true) : IMediaEngine
    {
        public List<IMediaPlayback> Created { get; } = [];

        public bool Initialize() => initializationSucceeds;

        public IMediaPlayback Create(string filePath, bool isVideo)
        {
            var playback = new FakePlayback();
            Created.Add(playback);
            return playback;
        }

        public void Dispose() { }
    }

    private sealed class FakePlayback : IMediaPlayback
    {
        public bool Stopped { get; private set; }

        public bool Disposed { get; private set; }

        public LibVLCSharp.Shared.MediaPlayer? VideoPlayer => null;

        public bool IsPlaying => false;

        public long Length => 0;

        public long Time { get; set; }

        public int Volume { get; set; }

        public void Play() { }

        public void Pause() { }

        public void Stop() => Stopped = true;

        public void Dispose() => Disposed = true;
    }
}
