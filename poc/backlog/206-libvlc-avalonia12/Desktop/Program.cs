using Avalonia;
using LibVLCSharp.Shared;

namespace Desktop;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--play")
        {
            return Play(args[1]);
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    private static int Play(string path)
    {
        Core.Initialize();
        using var libVlc = new LibVLC("--vout=dummy", "--aout=dummy");
        using var player = new MediaPlayer(libVlc);
        using var media = new Media(libVlc, path, FromType.FromPath);
        using var playing = new ManualResetEventSlim();
        using var ended = new ManualResetEventSlim();
        player.Playing += (_, _) => playing.Set();
        player.EndReached += (_, _) => ended.Set();
        if (!player.Play(media) || !playing.Wait(TimeSpan.FromSeconds(15)) || !ended.Wait(TimeSpan.FromSeconds(20)))
        {
            Console.Error.WriteLine($"Playback timed out; state={player.State}; length={media.Duration} ms");
            return 1;
        }

        Console.WriteLine($"Playing and EndReached received; Length={media.Duration} ms");
        return 0;
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
