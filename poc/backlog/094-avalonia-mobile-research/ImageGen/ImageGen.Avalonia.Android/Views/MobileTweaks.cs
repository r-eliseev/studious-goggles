using Avalonia.Controls;
using Avalonia.Input.TextInput;

namespace ImageGen.Avalonia.Views;

public static class MobileTweaks
{
    public static void Apply(Control page)
    {
        var input = page.FindControl<TextBox>("Input");
        if (input is null)
        {
            return;
        }

        TextInputOptions.SetReturnKeyType(input, TextInputReturnKeyType.Send);
        TextInputOptions.SetMultiline(input, true);
    }
}
