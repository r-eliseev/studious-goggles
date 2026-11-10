using LibVLCSharp.Shared;

namespace ImageGen.Avalonia.Services;

internal sealed class LibVlcMediaEngine : IMediaEngine
{
    private LibVLC? _libVlc;

    public bool Initialize()
    {
        if (_libVlc is not null) return true;
        LibVLCSharp.Shared.Core.Initialize();
        _libVlc = new LibVLC(enableDebugLogs: false);
        return true;
    }

    public IMediaPlayback Create(string filePath, bool isVideo)
    {
        var libVlc = _libVlc ?? throw new InvalidOperationException("LibVLC has not been initialized.");
        var media = new Media(libVlc, filePath, FromType.FromPath);
        var player = new MediaPlayer(media);
        return new LibVlcMediaPlayback(media, player, isVideo);
    }

    public void Dispose()
    {
        _libVlc?.Dispose();
        _libVlc = null;
    }
}

internal sealed class LibVlcMediaPlayback(Media media, MediaPlayer player, bool isVideo) : IMediaPlayback
{
    public MediaPlayer? VideoPlayer => isVideo ? player : null;

    public bool IsPlaying => player.IsPlaying;

    public long Length => player.Length;

    public long Time
    {
        get => player.Time;
        set => player.Time = value;
    }

    public int Volume
    {
        get => player.Volume;
        set => player.Volume = value;
    }

    public void Play() => player.Play();

    public void Pause() => player.Pause();

    public void Stop() => player.Stop();

    public void Dispose()
    {
        player.Dispose();
        media.Dispose();
    }
}
