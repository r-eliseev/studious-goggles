using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;

namespace ImageGen.E2E.Android;

internal static class AndroidSession
{
    private const string AppPackage = "com.eliseev.imagegen";

    public static AndroidDriver Create()
    {
        var appPath = Environment.GetEnvironmentVariable("APP_APK");
        if (string.IsNullOrWhiteSpace(appPath) || !File.Exists(appPath))
        {
            throw new InvalidOperationException("APP_APK must point to an existing Android APK.");
        }

        var options = new AppiumOptions
        {
            PlatformName = "Android",
            AutomationName = "UiAutomator2",
        };
        options.AddAdditionalAppiumOption("app", Path.GetFullPath(appPath));
        options.AddAdditionalAppiumOption("appPackage", AppPackage);
        options.AddAdditionalAppiumOption("noReset", false);
        options.AddAdditionalAppiumOption("newCommandTimeout", 120);

        return new AndroidDriver(new Uri("http://127.0.0.1:4723"), options, TimeSpan.FromMinutes(3));
    }
}
