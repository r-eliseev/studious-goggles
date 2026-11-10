using System.ComponentModel;
using System.Diagnostics;

namespace ImageGen.E2E.Windows;

internal sealed class VideoRecorder : IDisposable
{
    internal static void CaptureFinalFrame(Action screenshot, Action hold, Action stopRecording)
    {
        screenshot();
        hold();
        stopRecording();
    }

    private Process? _process;
    private Task<string>? _standardError;
    private string? _logPath;

    public void Start(string path)
    {
        if (_process is not null)
        {
            throw new InvalidOperationException("The screen recorder is already running.");
        }

        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        _logPath = Path.Combine(Path.GetDirectoryName(fullPath)!, "ffmpeg.log");
        var startInfo = new ProcessStartInfo("ffmpeg")
        {
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in BuildArguments(fullPath))
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            _process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start FFmpeg.");
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException("FFmpeg was not found in PATH; install FFmpeg before running Windows UI tests.", exception);
        }

        _standardError = _process.StandardError.ReadToEndAsync();
    }

    public void Stop()
    {
        if (_process is null)
        {
            return;
        }

        using var process = _process;
        _process = null;
        try
        {
            if (!process.HasExited)
            {
                process.StandardInput.WriteLine("q");
                process.StandardInput.Flush();
                if (!process.WaitForExit(10_000))
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit();
                }
            }
        }
        catch (IOException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }
        }

        var output = _standardError?.GetAwaiter().GetResult() ?? string.Empty;
        if (_logPath is not null)
        {
            File.WriteAllText(_logPath, output);
        }
        _standardError = null;
    }

    public void Dispose() => Stop();

    internal static string[] BuildArguments(string path) =>
    [
        "-y", "-f", "gdigrab", "-framerate", "15", "-i", "desktop",
        "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p",
        "-movflags", "+frag_keyframe+empty_moov", path,
    ];
}
