using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;

namespace ImageGen.Avalonia.Services.Secrets;

/// <summary>
/// macOS: Keychain through the system tool <c>/usr/bin/security</c> (ADR 0008). The value is passed in hex through the
/// standard input of <c>security -i</c>, so the key does not appear in the arguments of a process.
/// NOT VERIFIED: the project has no macOS machine (task 040, comments).
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class KeychainSecretBackend : ISecretBackend
{
    private const string SecurityTool = "/usr/bin/security";
    private const string Service = "com.eliseev.imagegen";
    private const int ItemNotFound = 44;
    private static readonly Regex SafeName = new("^[A-Za-z0-9_.-]+$", RegexOptions.CultureInvariant);

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) =>
        Task.FromResult(OperatingSystem.IsMacOS() && File.Exists(SecurityTool));

    public async Task<string?> GetAsync(string name, CancellationToken cancellationToken)
    {
        var (exitCode, output, _) = await RunAsync(
            ["find-generic-password", "-s", Service, "-a", CheckName(name), "-w"],
            stdin: null,
            cancellationToken);
        if (exitCode == ItemNotFound)
        {
            return null;
        }

        EnsureSuccess(exitCode, "чтение");
        return output.TrimEnd('\n');
    }

    public async Task SetAsync(string name, string value, CancellationToken cancellationToken)
    {
        var hex = Convert.ToHexString(Encoding.UTF8.GetBytes(value));
        var command = $"add-generic-password -U -s {Service} -a {CheckName(name)} -X {hex}\n";
        var (exitCode, _, _) = await RunAsync(["-i"], command, cancellationToken);
        EnsureSuccess(exitCode, "запись");
    }

    public async Task RemoveAsync(string name, CancellationToken cancellationToken)
    {
        var (exitCode, _, _) = await RunAsync(
            ["delete-generic-password", "-s", Service, "-a", CheckName(name)],
            stdin: null,
            cancellationToken);
        if (exitCode != ItemNotFound)
        {
            EnsureSuccess(exitCode, "удаление");
        }
    }

    private static string CheckName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!SafeName.IsMatch(name))
        {
            throw new ArgumentException("Недопустимое имя секрета.", nameof(name));
        }

        return name;
    }

    private static void EnsureSuccess(int exitCode, string operation)
    {
        if (exitCode != 0)
        {
            // The message names the operation and the exit code only: never the value.
            throw new InvalidOperationException($"Связка ключей macOS: ошибка при операции «{operation}» (код {exitCode}).");
        }
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunAsync(
        string[] arguments,
        string? stdin,
        CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(SecurityTool)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Не удалось запустить security.");
        if (stdin is not null)
        {
            await process.StandardInput.WriteAsync(stdin.AsMemory(), cancellationToken);
        }

        process.StandardInput.Close();
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, output, error);
    }
}
