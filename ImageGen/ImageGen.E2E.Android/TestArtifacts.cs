using System.Diagnostics;
using System.Text.Json;
using Allure.Net.Commons;

namespace ImageGen.E2E.Android;

internal sealed class TestArtifacts
{
    private const string InvalidKey = "sk-or-e2e-invalid";
    private readonly List<string> _errors = [];

    public TestArtifacts(string className, string testName)
    {
        var safeClass = Sanitize(className);
        var safeTest = Sanitize(testName);
        var reportsRoot = Environment.GetEnvironmentVariable("E2E_REPORTS_DIR");
        if (string.IsNullOrWhiteSpace(reportsRoot))
        {
            reportsRoot = Path.Combine(FindRepositoryRoot(), "tmp", "reports", "android");
        }

        DirectoryPath = Path.Combine(reportsRoot, $"{safeClass}.{safeTest}");
        Directory.CreateDirectory(DirectoryPath);
        StartedAt = DateTimeOffset.UtcNow;
        Stopwatch = Stopwatch.StartNew();
    }

    public string DirectoryPath { get; }

    public DateTimeOffset StartedAt { get; }

    private Stopwatch Stopwatch { get; }

    public void Capture(string className, string testName, string status, Action? screenshot, Func<string>? pageSource, Func<string?>? stopRecording)
    {
        if (status == "Failed")
        {
            Try("screenshot", () => Save("screenshot.png", screenshot));
            Try("page source", () => SaveText("ui.xml", pageSource?.Invoke() ?? string.Empty, "application/xml"));
        }

        Try("screen recording", () =>
        {
            var encoded = stopRecording?.Invoke();
            if (!string.IsNullOrWhiteSpace(encoded))
            {
                var bytes = Convert.FromBase64String(encoded);
                SaveBytes("recording.mp4", bytes, "video/mp4");
            }
        });
        Try("logcat", SaveLogcat);
        Try("metadata", () =>
        {
            var metadata = new
            {
                name = $"{className}.{testName}",
                status,
                durationMs = Stopwatch.ElapsedMilliseconds,
                gitSha = Environment.GetEnvironmentVariable("GITHUB_SHA"),
            };
            SaveText("metadata.json", JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }), "application/json");
        });

        if (_errors.Count > 0)
        {
            File.WriteAllLines(Path.Combine(DirectoryPath, "errors.txt"), _errors);
        }
    }

    private void Save(string fileName, Action? write)
    {
        if (write is null)
        {
            return;
        }

        write();
        Attach(fileName, MimeType(fileName), File.ReadAllBytes(Path.Combine(DirectoryPath, fileName)));
    }

    private void SaveText(string fileName, string content, string mediaType)
    {
        File.WriteAllText(Path.Combine(DirectoryPath, fileName), content);
        Attach(fileName, mediaType, File.ReadAllBytes(Path.Combine(DirectoryPath, fileName)));
    }

    private void SaveBytes(string fileName, byte[] content, string mediaType)
    {
        File.WriteAllBytes(Path.Combine(DirectoryPath, fileName), content);
        Attach(fileName, mediaType, content);
    }

    private void SaveLogcat()
    {
        var androidHome = Environment.GetEnvironmentVariable("ANDROID_HOME");
        if (string.IsNullOrWhiteSpace(androidHome))
        {
            throw new InvalidOperationException("ANDROID_HOME is not set.");
        }

        var adb = Path.Combine(androidHome, "platform-tools", "adb");
        var pid = RunProcess(adb, ["shell", "pidof", "-s", "com.eliseev.imagegen"]).Trim();
        if (string.IsNullOrWhiteSpace(pid))
        {
            throw new InvalidOperationException("Could not find the Android application process.");
        }

        var since = StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff");
        var log = RunProcess(adb, ["logcat", "-d", "-T", since, $"--pid={pid}"])
            .Replace(InvalidKey, "[redacted]", StringComparison.Ordinal);
        SaveText("logcat.txt", log, "text/plain");
        if (log.Contains(InvalidKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The invalid test key was found in logcat.");
        }
    }

    private static string RunProcess(string executable, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start adb.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"adb returned exit code {process.ExitCode}: {error}");
        }

        return output;
    }

    private void Try(string operation, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            _errors.Add($"{operation}: {exception.GetType().Name}");
        }
    }

    private void Attach(string name, string mediaType, byte[] content)
    {
        Try($"Allure attachment {name}", () => AllureApi.AddAttachment(name, mediaType, content));
    }

    private static string MimeType(string fileName) => Path.GetExtension(fileName) switch
    {
        ".png" => "image/png",
        ".xml" => "application/xml",
        ".mp4" => "video/mp4",
        _ => "application/octet-stream",
    };

    private static string Sanitize(string value) => string.Concat(value.Select(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_' ? character : '_'));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            {
                return directory.FullName;
            }
        }

        return Environment.CurrentDirectory;
    }
}
