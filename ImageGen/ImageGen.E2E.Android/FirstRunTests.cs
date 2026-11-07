using System.Globalization;
using ImageGen.E2E.Common;
using ImageGen.Presentation.Resources.Strings;
using NUnit.Framework;
using OpenQA.Selenium;

namespace ImageGen.E2E.Android;

[TestFixture]
[NonParallelizable]
public sealed class FirstRunTests : AndroidTestBase
{
    private const string InvalidKey = "sk-or-e2e-invalid";

    [Test]
    public void Launch_ShowsFirstRunKeyScreen()
    {
        Assert.That(Find(TestIds.ApiKeyEntry).Displayed, Is.True);
        Assert.That(Find(TestIds.ApiKeySave).Displayed, Is.True);
    }

    [Test]
    public void InvalidKey_ShowsError()
    {
        Find(TestIds.ApiKeyEntry).SendKeys(InvalidKey);
        Find(TestIds.ApiKeySave).Click();

        var message = WaitForText(TestIds.ApiKeyStatus, TimeSpan.FromSeconds(30)).Text;
        var invalidFormat = string.Format(CultureInfo.InvariantCulture, AppResources.ProviderKeyInvalidFormat, "OpenRouter");
        var networkError = string.Format(CultureInfo.InvariantCulture, AppResources.ProviderKeyNetworkErrorFormat, "OpenRouter");
        Assert.That(
            message.StartsWith(invalidFormat, StringComparison.Ordinal)
            || message.StartsWith(networkError, StringComparison.Ordinal),
            Is.True,
            "The status should contain the invalid-key or network error resource.");
        Assert.That(message, Does.Not.Contain(InvalidKey));
    }

    [Test]
    public void Rotate_KeepsKeyScreen()
    {
        Driver.Orientation = ScreenOrientation.Landscape;
        Driver.Orientation = ScreenOrientation.Portrait;

        Assert.That(Find(TestIds.ApiKeyEntry).Displayed, Is.True);
    }
}
