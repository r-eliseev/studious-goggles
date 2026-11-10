using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
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
        var content = Encoding.Unicode.GetBytes(text + '\0');
        var buffer = GlobalAlloc(MovableMemory, (UIntPtr)content.Length);
        if (buffer == IntPtr.Zero)
        {
            throw new InvalidOperationException("Could not allocate Windows clipboard memory.");
        }

        try
        {
            var lockedBuffer = GlobalLock(buffer);
            if (lockedBuffer == IntPtr.Zero)
            {
                throw new InvalidOperationException("Could not lock Windows clipboard memory.");
            }

            try
            {
                Marshal.Copy(content, 0, lockedBuffer, content.Length);
            }
            finally
            {
                GlobalUnlock(buffer);
            }

            if (!OpenClipboard(IntPtr.Zero))
            {
                throw new InvalidOperationException("Could not open the Windows clipboard.");
            }

            try
            {
                if (!EmptyClipboard() || SetClipboardData(UnicodeText, buffer) == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Could not set the Windows clipboard text.");
                }

                buffer = IntPtr.Zero;
            }
            finally
            {
                CloseClipboard();
            }
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                GlobalFree(buffer);
            }
        }
    }

    private const uint UnicodeText = 13;
    private const uint MovableMemory = 2;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr memory);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint format, IntPtr memory);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();
}
