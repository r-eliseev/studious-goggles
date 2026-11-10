using System.Diagnostics;
using System.Text.Json;
using Allure.Net.Commons;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace ImageGen.E2E.Windows;

public abstract class WindowsTestBase
{
    private const string InvalidKey = "sk-or-e2e-invalid";
    private const int FinalFrameHold = 1500;
    private static readonly TimeSpan ElementTimeout = TimeSpan.FromSeconds(20);
    private readonly List<string> _errors = [];
    private Application? _application;
    private UIA3Automation? _automation;
    private Window? _window;
    private VideoRecorder? _recorder;
    private string? _artifactDirectory;
    private string? _dataDirectory;
    private Stopwatch? _stopwatch;

    protected Window Window => _window ?? throw new InvalidOperationException("The MAUI main window is not available.");

    [SetUp]
    public void SetUpWindowsTest()
    {
        _errors.Clear();
        var context = TestContext.CurrentContext;
        var reportsRoot = Environment.GetEnvironmentVariable("E2E_REPORTS_DIR");
        if (string.IsNullOrWhiteSpace(reportsRoot))
        {
            reportsRoot = Path.Combine(FindRepositoryRoot(), "tmp", "reports", "windows");
        }

        _artifactDirectory = Path.Combine(reportsRoot, $"{Sanitize(context.Test.ClassName ?? "Unknown")}.{Sanitize(context.Test.Name)}");
        Directory.CreateDirectory(_artifactDirectory);
        _stopwatch = Stopwatch.StartNew();

        _dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "User Name", "com.eliseev.imagegen", "Data");
        if (Directory.Exists(_dataDirectory))
        {
            Directory.Delete(_dataDirectory, recursive: true);
        }

        var providerConfig = Environment.GetEnvironmentVariable("E2E_PROVIDER_CONFIG");
        if (!string.IsNullOrWhiteSpace(providerConfig))
        {
            if (!File.Exists(providerConfig))
            {
                throw new FileNotFoundException("E2E_PROVIDER_CONFIG must point to an existing providers.json.", providerConfig);
            }

            Directory.CreateDirectory(_dataDirectory);
            File.Copy(providerConfig, Path.Combine(_dataDirectory, "providers.json"));
        }

        var executable = Environment.GetEnvironmentVariable("APP_EXE");
        if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
        {
            throw new InvalidOperationException("APP_EXE must point to an existing ImageGen.Maui.exe.");
        }

        _recorder = new VideoRecorder();
        _recorder.Start(Path.Combine(_artifactDirectory, "recording.mp4"));
        _application = Application.Launch(Path.GetFullPath(executable));
        _automation = new UIA3Automation();
        _window = Retry.WhileNull(
                () => _application.GetMainWindow(_automation),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromMilliseconds(250))
            .Result;
        if (_window is null)
        {
            throw new InvalidOperationException("The MAUI main window was not found within 30 seconds.");
        }
    }

    [TearDown]
    public void TearDownWindowsTest()
    {
        var context = TestContext.CurrentContext;
        var failed = context.Result.Outcome.Status == TestStatus.Failed;
        if (_artifactDirectory is null)
        {
            return;
        }

        VideoRecorder.CaptureFinalFrame(
            () => Try("screenshot", () =>
            {
                if (_window is not null)
                {
                    var path = Path.Combine(_artifactDirectory, "screenshot.png");
                    Capture.Element(_window).ToFile(path);
                    Attach("screenshot.png", "image/png", path);
                }
            }),
            () => Thread.Sleep(FinalFrameHold),
            () => Try("screen recording", () =>
            {
                _recorder?.Stop();
                Attach("recording.mp4", "video/mp4", Path.Combine(_artifactDirectory, "recording.mp4"));
            }));

        if (failed)
        {
            Try("UI tree", () =>
            {
                if (_window is not null)
                {
                    var path = Path.Combine(_artifactDirectory, "ui.txt");
                    var tree = _window.FindAllDescendants()
                        .Select(element => $"{element.ControlType}|{element.AutomationId}|{element.Name}");
                    File.WriteAllLines(path, tree);
                    Attach("ui.txt", "text/plain", path);
                }
            });
        }

        Try("close application", () =>
        {
            if (_application is not null)
            {
                _application.Close();
                if (!_application.HasExited)
                {
                    _application.Kill();
                }
            }
        });
        Try("application log", CopyApplicationLog);
        Try("metadata", () =>
        {
            var metadata = new
            {
                name = $"{context.Test.ClassName}.{context.Test.Name}",
                status = context.Result.Outcome.Status.ToString(),
                durationMs = _stopwatch?.ElapsedMilliseconds ?? 0,
                gitSha = Environment.GetEnvironmentVariable("GITHUB_SHA"),
            };
            var path = Path.Combine(_artifactDirectory, "metadata.json");
            File.WriteAllText(path, JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
            Attach("metadata.json", "application/json", path);
        });

        Try("write errors", () =>
        {
            if (_errors.Count > 0)
            {
                File.WriteAllLines(Path.Combine(_artifactDirectory, "errors.txt"), _errors);
            }
        });
        Try("dispose automation", () => _automation?.Dispose());
        Try("dispose application", () => _application?.Dispose());
        _automation = null;
        _application = null;
        _window = null;
        _recorder = null;
    }

    protected AutomationElement Find(string id) => Find(id, ElementTimeout);

    protected AutomationElement Find(string id, TimeSpan timeout)
    {
        var element = Retry.WhileNull(
                () => Window.FindFirstDescendant(condition => condition.ByAutomationId(id)),
                timeout,
                TimeSpan.FromMilliseconds(250))
            .Result;
        return element ?? throw new InvalidOperationException($"AutomationId '{id}' was not found within {timeout}.");
    }

    protected AutomationElement FindByName(string name, TimeSpan? timeout = null)
    {
        var automation = _automation ?? throw new InvalidOperationException("The UI automation session is not available.");
        var element = Retry.WhileNull(
                () => automation.GetDesktop().FindAllDescendants()
                    .FirstOrDefault(candidate => candidate.Name.Contains(name, StringComparison.OrdinalIgnoreCase)),
                timeout ?? ElementTimeout,
                TimeSpan.FromMilliseconds(250))
            .Result;
        return element ?? throw new InvalidOperationException($"Element '{name}' was not found on the desktop.");
    }

    protected AutomationElement WaitForText(string id, TimeSpan timeout)
        => WaitForText(id, timeout, text => !string.IsNullOrWhiteSpace(text));

    protected AutomationElement WaitForText(string id, TimeSpan timeout, Func<string, bool> predicate)
    {
        var element = Retry.WhileNull(
                () =>
                {
                    var candidate = Window.FindFirstDescendant(condition => condition.ByAutomationId(id));
                    return candidate is null || !predicate(candidate.Name) ? null : candidate;
                },
                timeout,
                TimeSpan.FromMilliseconds(250))
            .Result;
        return element ?? throw new InvalidOperationException($"AutomationId '{id}' did not receive text within {timeout}.");
    }

    private void CopyApplicationLog()
    {
        if (_dataDirectory is null)
        {
            throw new InvalidOperationException("The MAUI data directory was not initialized.");
        }

        var logDirectory = Path.Combine(_dataDirectory, "logs");
        var latest = Directory.Exists(logDirectory)
            ? Directory.GetFiles(logDirectory, "imagegen-*.log").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
            : null;
        if (latest is null)
        {
            throw new FileNotFoundException("No MAUI application log was found.");
        }

        var content = File.ReadAllText(latest).Replace(InvalidKey, "[redacted]", StringComparison.Ordinal);
        var path = Path.Combine(_artifactDirectory!, "app.log");
        File.WriteAllText(path, content);
        Attach("app.log", "text/plain", path);
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

    private static void Attach(string name, string mediaType, string path) =>
        AllureApi.AddAttachment(name, mediaType, File.ReadAllBytes(path));

    private static string Sanitize(string value) =>
        string.Concat(value.Select(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_' ? character : '_'));

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
