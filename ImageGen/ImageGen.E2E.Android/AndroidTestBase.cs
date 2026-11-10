using System.Diagnostics;
using ImageGen.E2E.Common;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;

namespace ImageGen.E2E.Android;

public abstract class AndroidTestBase
{
    private const string AppPackage = "com.eliseev.imagegen";
    private AndroidDriver? _driver;
    private TestArtifacts? _artifacts;

    protected AndroidDriver Driver => _driver ?? throw new InvalidOperationException("The Android session has not started.");

    [SetUp]
    public void SetUpAndroidTest()
    {
        var context = TestContext.CurrentContext;
        _artifacts = new TestArtifacts(context.Test.ClassName ?? "Unknown", context.Test.Name);
        _driver ??= AndroidSession.Create();
        Driver.ExecuteScript("mobile: clearApp", new Dictionary<string, object> { ["appId"] = AppPackage });
        Driver.ExecuteScript("mobile: activateApp", new Dictionary<string, object> { ["appId"] = AppPackage });
        Driver.StartRecordingScreen(new AndroidStartScreenRecordingOptions().WithTimeLimit(TimeSpan.FromSeconds(1800)));
    }

    [TearDown]
    public void TearDownAndroidTest()
    {
        if (_artifacts is null)
        {
            return;
        }

        var context = TestContext.CurrentContext;
        var status = context.Result.Outcome.Status.ToString();
        var failed = context.Result.Outcome.Status == TestStatus.Failed;
        _artifacts.Capture(
            context.Test.ClassName ?? "Unknown",
            context.Test.Name,
            status,
            () => Driver.GetScreenshot().SaveAsFile(Path.Combine(_artifacts.DirectoryPath, "screenshot.png")),
            failed ? () => Driver.PageSource : null,
            DriverOrNull is { } driver ? driver.StopRecordingScreen : null);
    }

    [OneTimeTearDown]
    public void TearDownAndroidSession()
    {
        _driver?.Quit();
        _driver?.Dispose();
        _driver = null;
    }

    protected IWebElement Find(string id) => Find(id, TimeSpan.FromSeconds(20));

    protected IWebElement Find(string id, TimeSpan timeout)
    {
        try
        {
            return new WebDriverWait(Driver, timeout).Until(driver =>
            {
                try
                {
                    return driver.FindElement(By.Id(ResourceIdFor(id)));
                }
                catch (NoSuchElementException)
                {
                    return null;
                }
            }) ?? throw new NoSuchElementException();
        }
        catch (WebDriverTimeoutException exception)
        {
            throw new WebDriverTimeoutException($"Element with automation id '{id}' was not found.", exception);
        }
    }

    protected IWebElement WaitForText(string id, TimeSpan timeout)
        => WaitForText(id, timeout, text => !string.IsNullOrWhiteSpace(text));

    protected IWebElement WaitForText(string id, TimeSpan timeout, Func<string, bool> predicate)
    {
        try
        {
            return new WebDriverWait(Driver, timeout).Until(driver =>
            {
                try
                {
                    var element = driver.FindElement(By.Id(ResourceIdFor(id)));
                    return predicate(element.Text) ? element : null;
                }
                catch (NoSuchElementException)
                {
                    return null;
                }
            }) ?? throw new WebDriverTimeoutException();
        }
        catch (WebDriverTimeoutException exception)
        {
            throw new WebDriverTimeoutException($"Element with automation id '{id}' did not receive text within {timeout}.", exception);
        }
    }

    private AndroidDriver? DriverOrNull => _driver;

    private static string ResourceIdFor(string id) =>
        id.Contains(":id/", StringComparison.Ordinal) ? id : $"{AppPackage}:id/{id}";
}
