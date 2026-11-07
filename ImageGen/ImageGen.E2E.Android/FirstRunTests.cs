using System.Globalization;
using Allure.NUnit;
using ImageGen.E2E.Common;
using ImageGen.Presentation.Resources.Strings;
using NUnit.Framework;
using OpenQA.Selenium;

namespace ImageGen.E2E.Android;

[TestFixture]
[AllureNUnit]
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

        var invalidFormat = string.Format(CultureInfo.InvariantCulture, AppResources.ProviderKeyInvalidFormat, "OpenRouter");
        var networkError = string.Format(CultureInfo.InvariantCulture, AppResources.ProviderKeyNetworkErrorFormat, "OpenRouter");
        var message = WaitForText(
            TestIds.ApiKeyStatus,
            TimeSpan.FromSeconds(30),
            text => text.StartsWith(invalidFormat, StringComparison.Ordinal)
                || text.StartsWith(networkError, StringComparison.Ordinal)).Text;
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
