using System.Diagnostics;
using System.Globalization;
using Allure.NUnit;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using ImageGen.E2E.Common;
using ImageGen.Presentation.Resources.Strings;
using NUnit.Framework;

namespace ImageGen.E2E.Windows;

[TestFixture]
[AllureNUnit]
[NonParallelizable]
public sealed class FirstRunTests : WindowsTestBase
{
    private const string InvalidKey = "sk-or-e2e-invalid";

    [Test]
    public void Launch_ShowsFirstRunKeyScreen()
    {
        Assert.That(Find(TestIds.ApiKeyEntry).IsOffscreen, Is.False);
        Assert.That(Find(TestIds.ApiKeySave).IsOffscreen, Is.False);
    }

    [Test]
    public void InvalidKey_ShowsError()
    {
        Find(TestIds.ApiKeyEntry).AsTextBox().Enter(InvalidKey);
        Find(TestIds.ApiKeySave).AsButton().Invoke();

        var invalidFormat = string.Format(CultureInfo.InvariantCulture, AppResources.ProviderKeyInvalidFormat, "OpenRouter");
        var networkError = string.Format(CultureInfo.InvariantCulture, AppResources.ProviderKeyNetworkErrorFormat, "OpenRouter");
        var message = WaitForText(
            TestIds.ApiKeyStatus,
            TimeSpan.FromSeconds(30),
            text => text.StartsWith(invalidFormat, StringComparison.Ordinal)
                || text.StartsWith(networkError, StringComparison.Ordinal)).Name;
        Assert.That(message, Does.Not.Contain(InvalidKey));
    }

    [Test]
    public void PasteInvalidKey_ShowsError()
    {
        SetClipboardText(InvalidKey);
        try
        {
            Find(TestIds.ApiKeyEntry).Click();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_V);
            Thread.Sleep(TimeSpan.FromSeconds(1));
            Find(TestIds.ApiKeySave).AsButton().Invoke();

            var invalidFormat = string.Format(CultureInfo.InvariantCulture, AppResources.ProviderKeyInvalidFormat, "OpenRouter");
            var networkError = string.Format(CultureInfo.InvariantCulture, AppResources.ProviderKeyNetworkErrorFormat, "OpenRouter");
            var message = WaitForText(
                TestIds.ApiKeyStatus,
                TimeSpan.FromSeconds(30),
                text => text.StartsWith(invalidFormat, StringComparison.Ordinal)
                    || text.StartsWith(networkError, StringComparison.Ordinal)).Name;
            Assert.That(message, Does.Not.Contain(InvalidKey));
        }
        finally
        {
            SetClipboardText(string.Empty);
        }
    }

    private static void SetClipboardText(string text)
    {
        var startInfo = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add($"Set-Clipboard -Value '{text}'");

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not set the Windows clipboard.");
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("Setting the Windows clipboard failed.");
        }
    }
}
