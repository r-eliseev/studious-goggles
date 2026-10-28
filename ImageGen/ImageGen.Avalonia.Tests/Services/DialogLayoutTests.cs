using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using ImageGen.Avalonia.Services;

namespace ImageGen.Avalonia.Tests.Services;

public class DialogLayoutTests
{
    [AvaloniaFact]
    public void Confirm_NotMac_AcceptIsFirst()
    {
        var parts = DialogLayout.Build("Title", "Message", "OK", "Cancel", input: null, multiline: false, destructive: false, isMacOs: false);

        Assert.Equal("OK", AutomationProperties.GetName(parts.Buttons.Children[0]));
    }

    [AvaloniaFact]
    public void Confirm_Mac_AcceptIsLast()
    {
        var parts = DialogLayout.Build("Title", "Message", "OK", "Cancel", input: null, multiline: false, destructive: false, isMacOs: true);

        Assert.Equal("OK", AutomationProperties.GetName(parts.Buttons.Children[1]));
    }

    [AvaloniaFact]
    public void Confirm_Destructive_AcceptHasDangerClass()
    {
        var parts = DialogLayout.Build("Delete", "Sure?", "Delete", "Cancel", input: null, multiline: false, destructive: true, isMacOs: false);

        Assert.Contains("danger", parts.Accept.Classes);
        Assert.DoesNotContain("primary", parts.Accept.Classes);
    }

    [AvaloniaFact]
    public void Prompt_Multiline_AcceptsReturnAndKeepsText()
    {
        var parts = DialogLayout.Build("Edit", "Prompt", "Save", "Cancel", input: "a fox", multiline: true, destructive: false, isMacOs: false);

        Assert.NotNull(parts.Input);
        Assert.True(parts.Input!.AcceptsReturn);
        Assert.Equal("a fox", parts.Input.Text);
    }

    [AvaloniaFact]
    public void Alert_HasOnlyOneButton()
    {
        var parts = DialogLayout.Build("Title", "Message", "OK", cancel: null, input: null, multiline: false, destructive: false, isMacOs: false);

        Assert.Single(parts.Buttons.Children);
        Assert.Null(parts.Cancel);
        Assert.Null(parts.Input);
    }
}
