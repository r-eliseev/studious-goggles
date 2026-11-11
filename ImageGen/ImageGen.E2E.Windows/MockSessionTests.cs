using Allure.NUnit;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using ImageGen.E2E.Common;
using NUnit.Framework;

namespace ImageGen.E2E.Windows;

[TestFixture]
[AllureNUnit]
[NonParallelizable]
public sealed class MockSessionTests : WindowsTestBase
{
    [Test]
    public void FirstRunWithMock_GeneratesImage()
    {
        Find(TestIds.ApiKeyProvider).Click();
        FindByName("E2E Mock").Click();
        Find(TestIds.ApiKeyEntry).AsTextBox().Enter("e2e-mock-key");
        Find(TestIds.ApiKeySave).AsButton().Invoke();

        Find(TestIds.ApiKeyAssistantModel, TimeSpan.FromSeconds(30)).Click();
        FindByName("E2E Assistant").Click();
        Find(TestIds.ApiKeyUseModel).AsButton().Invoke();

        FindByName("New session").AsButton().Invoke();
        Find(TestIds.ChatModels).AsButton().Invoke();
        FindByName("e2e (1)").Click();
        FindByName("E2E Image", TimeSpan.FromSeconds(15)).Click();
        FindByName("Save").AsButton().Invoke();

        Find(TestIds.ChatInput).Click();
        Keyboard.Type("a red square");
        var sendEnabled = Retry.WhileFalse(
                () => Find(TestIds.ChatSend).IsEnabled,
                TimeSpan.FromSeconds(10),
                TimeSpan.FromMilliseconds(250))
            .Success;
        Assert.That(sendEnabled, Is.True, "The send button should enable after entering a prompt.");
        Find(TestIds.ChatSend).AsButton().Invoke();
        WaitForText(TestIds.ChatRun, TimeSpan.FromSeconds(30));
        WaitForText(
            TestIds.ChatGeneration,
            TimeSpan.FromSeconds(60),
            text => text.Contains("e2e/image", StringComparison.Ordinal)
                && text.Contains("Done", StringComparison.Ordinal)
                && !text.Contains("Error", StringComparison.Ordinal));
    }

}
