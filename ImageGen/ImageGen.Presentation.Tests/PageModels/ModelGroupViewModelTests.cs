using ImageGen.Core.Domain.Models;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Tests.TestDoubles;

namespace ImageGen.Presentation.Tests.PageModels;

public class ModelGroupViewModelTests
{
    [Fact]
    public void ImageGroup_ExpandedAtCreation_HasRowsWithoutCallback()
    {
        var item = new ModelOptionViewModel(TestModels.Model("vendor/alpha", "Alpha"), "OpenRouter", true);
        var callbackCount = 0;

        var group = new ImageModelGroupViewModel("vendor", "vendor", [item], true, _ => callbackCount++);

        Assert.Same(item, Assert.Single(group.Rows));
        Assert.Equal(0, callbackCount);
    }

    [Fact]
    public void ImageGroup_CollapsedAtCreation_HasNoRowsAndToggleFillsThemOnce()
    {
        var item = new ModelOptionViewModel(TestModels.Model("vendor/alpha", "Alpha"), "OpenRouter", true);
        var callbackCount = 0;
        var group = new ImageModelGroupViewModel("vendor", "vendor", [item], false, _ => callbackCount++);

        Assert.Empty(group.Rows);

        group.ToggleCommand.Execute(null);

        Assert.True(group.IsExpanded);
        Assert.Same(item, Assert.Single(group.Rows));
        Assert.Equal(1, callbackCount);
    }

    [Fact]
    public void AssistantGroup_ExpandedAtCreation_HasRowsWithoutCallback()
    {
        var item = AssistantItem("vendor/assistant", "Assistant");
        var callbackCount = 0;

        var group = new AssistantModelGroupViewModel("vendor", "vendor", [item], true, _ => callbackCount++);

        Assert.Same(item, Assert.Single(group.Rows));
        Assert.Equal(0, callbackCount);
    }

    [Fact]
    public void AssistantGroup_CollapsedAtCreation_HasNoRowsAndToggleFillsThemOnce()
    {
        var item = AssistantItem("vendor/assistant", "Assistant");
        var callbackCount = 0;
        var group = new AssistantModelGroupViewModel("vendor", "vendor", [item], false, _ => callbackCount++);

        Assert.Empty(group.Rows);

        group.ToggleCommand.Execute(null);

        Assert.True(group.IsExpanded);
        Assert.Same(item, Assert.Single(group.Rows));
        Assert.Equal(1, callbackCount);
    }

    private static AssistantModelOptionViewModel AssistantItem(string id, string name) =>
        new(new ModelRef("openrouter", id), "OpenRouter (openrouter)", new ChatModel(id, name, SupportsImageInput: false, ContextLength: 1000), false, _ => { });
}
