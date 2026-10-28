using ImageGen.Presentation.PageModels;

namespace ImageGen.Presentation.Tests.PageModels;

public class RunMessageTextTests
{
    [Fact]
    public void Visible_LegacyPromptLineAfterText_RemovesLineKeepsText()
    {
        var text = RunMessageText.Visible("Here you go\n\nPrompt: a fox", "a fox");

        Assert.Equal("Here you go", text);
    }

    [Fact]
    public void Visible_OnlyLegacyPromptLine_ReturnsEmpty()
    {
        var text = RunMessageText.Visible("Prompt: a fox", "a fox");

        Assert.Equal("", text);
    }

    [Fact]
    public void Visible_TextWithoutPromptLine_ReturnsTextUnchanged()
    {
        var text = RunMessageText.Visible("Here you go", "a fox");

        Assert.Equal("Here you go", text);
    }

    [Fact]
    public void Visible_PromptLineForAnotherPrompt_IsKept()
    {
        var text = RunMessageText.Visible("Prompt: a dog", "a fox");

        Assert.Equal("Prompt: a dog", text);
    }
}
