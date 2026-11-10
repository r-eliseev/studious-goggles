using LibVLCSharp.Shared;

namespace ImageGen.Avalonia.Services;

internal interface IMediaEngine : IDisposable
{
    bool Initialize();

    IMediaPlayback Create(string filePath, bool isVideo);
}

internal interface IMediaPlayback : IDisposable
{
    MediaPlayer? VideoPlayer { get; }

    bool IsPlaying { get; }

    long Length { get; }

    long Time { get; set; }

    int Volume { get; set; }

    void Play();

    void Pause();

    void Stop();
}
