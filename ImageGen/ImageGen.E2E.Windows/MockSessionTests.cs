using Allure.NUnit;
using FlaUI.Core.AutomationElements;
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
        FindByName("All models").Click();
        var search = Window.FindFirstDescendant(condition => condition.ByControlType(FlaUI.Core.Definitions.ControlType.Edit));
        Assert.That(search, Is.Not.Null, "The model picker search field should be available.");
        search!.AsTextBox().Enter("e2e/image");
        FindByName("E2E Image", TimeSpan.FromSeconds(15)).Click();

        Find(TestIds.ChatInput).AsTextBox().Enter("a red square");
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
