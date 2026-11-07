using System.Globalization;
using FlaUI.Core.AutomationElements;
using ImageGen.E2E.Common;
using ImageGen.Presentation.Resources.Strings;
using NUnit.Framework;

namespace ImageGen.E2E.Windows;

[TestFixture]
[NonParallelizable]
public sealed class FirstRunTests : WindowsTestBase
{
    private const string InvalidKey = "sk-or-e2e-invalid";

    [Test]
    public void Launch_ShowsFirstRunKeyScreen()
    {
        Assert.That(Find(TestIds.ApiKeyEntry).IsEnabled, Is.True);
        Assert.That(Find(TestIds.ApiKeySave).IsEnabled, Is.True);
    }

    [Test]
    public void InvalidKey_ShowsError()
    {
        Find(TestIds.ApiKeyEntry).AsTextBox().Enter(InvalidKey);
        Find(TestIds.ApiKeySave).AsButton().Invoke();

        var message = WaitForText(TestIds.ApiKeyStatus, TimeSpan.FromSeconds(30)).Name;
        var invalidFormat = string.Format(CultureInfo.InvariantCulture, AppResources.ProviderKeyInvalidFormat, "OpenRouter");
        var networkError = string.Format(CultureInfo.InvariantCulture, AppResources.ProviderKeyNetworkErrorFormat, "OpenRouter");
        Assert.That(
            message.StartsWith(invalidFormat, StringComparison.Ordinal)
            || message.StartsWith(networkError, StringComparison.Ordinal),
            Is.True,
            "The status should contain the invalid-key or network error resource.");
        Assert.That(message, Does.Not.Contain(InvalidKey));
    }
}
