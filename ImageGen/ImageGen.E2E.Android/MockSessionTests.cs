using System.Diagnostics;
using Allure.NUnit;
using ImageGen.E2E.Common;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Support.UI;

namespace ImageGen.E2E.Android;

[TestFixture]
[AllureNUnit]
[NonParallelizable]
public sealed class MockSessionTests : AndroidTestBase
{
    private const string AppPackage = "com.eliseev.imagegen";
    private const string ProviderConfigPath = "/data/data/com.eliseev.imagegen/files/providers.json";

    [SetUp]
    public void RestoreProviderConfigAfterAppReset()
    {
        Driver.ExecuteScript("mobile: clearApp", new Dictionary<string, object> { ["appId"] = AppPackage });

        var androidHome = Environment.GetEnvironmentVariable("ANDROID_HOME")
            ?? Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT")
            ?? throw new InvalidOperationException("ANDROID_HOME or ANDROID_SDK_ROOT must be set.");
        var adb = Path.Combine(androidHome, "platform-tools", "adb");
        var config = Path.Combine(AppContext.BaseDirectory, "e2e-providers.json");
        if (!File.Exists(config))
        {
            throw new FileNotFoundException("The E2E provider config was not copied to the test output directory.", config);
        }

        if (RunAdb(adb, "shell", "id", "-u").Trim() != "0")
        {
            throw new InvalidOperationException("The Android emulator must run adb as root to install the E2E provider config.");
        }

        RunAdb(adb, "shell", "mkdir", "-p", "/data/data/com.eliseev.imagegen/files");
        RunAdb(adb, "push", config, ProviderConfigPath);
        var owner = RunAdb(adb, "shell", "stat", "-c", "%u:%g", "/data/data/com.eliseev.imagegen").Trim();
        RunAdb(adb, "shell", "chown", owner, ProviderConfigPath);
        RunAdb(adb, "shell", "chmod", "600", ProviderConfigPath);
        RunAdb(adb, "shell", "restorecon", ProviderConfigPath);

        Driver.ExecuteScript("mobile: activateApp", new Dictionary<string, object> { ["appId"] = AppPackage });
    }

    [Test]
    public void FirstRunWithMock_GeneratesImage()
    {
        Find(TestIds.ApiKeyProvider).Click();
        Driver.FindElement(By.XPath("//*[contains(@text, 'E2E Mock')]")).Click();
        Find(TestIds.ApiKeyEntry).SendKeys("e2e-mock-key");
        Find(TestIds.ApiKeySave).Click();

        Find(TestIds.ApiKeyAssistantModel, TimeSpan.FromSeconds(30)).Click();
        Driver.FindElement(By.XPath("//*[contains(@text, 'E2E Assistant')]")).Click();
        Find(TestIds.ApiKeyUseModel).Click();

        Find(TestIds.SessionsListNew).Click();
        Find(TestIds.ChatModels).Click();
        EnterModelSearch("e2e/image");
        var e2eGroup = new WebDriverWait(Driver, TimeSpan.FromSeconds(15)).Until(driver =>
        {
            try
            {
                return driver.FindElements(MobileBy.AndroidUIAutomator("new UiSelector().descriptionContains(\"e2e (1)\")"))
                    .FirstOrDefault();
            }
            catch (StaleElementReferenceException)
            {
                return null;
            }
        });
        Assert.That(e2eGroup, Is.Not.Null, "The e2e provider model group should be available.");
        e2eGroup!.Click();
        Driver.FindElement(MobileBy.AccessibilityId("E2E Image, E2E Mock (e2e-mock)")).Click();
        Driver.FindElement(By.XPath("//*[@text='Save']")).Click();

        Find(TestIds.ChatInput).SendKeys("a red square");
        Find(TestIds.ChatSend).Click();
        WaitForText(TestIds.ChatRun, TimeSpan.FromSeconds(30), text => text.Contains("a red square", StringComparison.Ordinal));

        var successfulImage = new WebDriverWait(Driver, TimeSpan.FromSeconds(60)).Until(driver =>
            driver.FindElements(By.Id($"{AppPackage}:id/{TestIds.ChatGeneration}"))
                .FirstOrDefault(card =>
                {
                    var summary = string.Join(" ", card.Text, card.GetAttribute("contentDescription"));
                    return summary.Contains("e2e/image", StringComparison.Ordinal)
                        && summary.Contains("Done", StringComparison.Ordinal)
                        && !summary.Contains("Error", StringComparison.Ordinal);
                }));

        Assert.That(successfulImage, Is.Not.Null, "The E2E image generation should finish successfully.");
    }


    private void EnterModelSearch(string searchText)
    {
        new WebDriverWait(Driver, TimeSpan.FromSeconds(15)).Until(driver =>
        {
            try
            {
                var search = driver.FindElement(MobileBy.AccessibilityId("Search models"));
                search.Clear();
                search.SendKeys(searchText);
                return true;
            }
            catch (NoSuchElementException)
            {
                return false;
            }
            catch (StaleElementReferenceException)
            {
                return false;
            }
        });
    }

    private static string RunAdb(string adb, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(adb)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start adb.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"adb {string.Join(' ', arguments)} did not finish within 30 seconds.");
        }

        Task.WaitAll(outputTask, errorTask);
        var output = outputTask.Result;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"adb {string.Join(' ', arguments)} failed: {errorTask.Result}");
        }

        return output;
    }
}
