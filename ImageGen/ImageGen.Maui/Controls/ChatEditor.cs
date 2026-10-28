using System.Windows.Input;

namespace ImageGen.Maui.Controls;

/// <summary>
/// The chat input: an <see cref="Editor"/> that also knows the command to run on Enter. The key handling itself is
/// platform code (Platforms/Windows/ChatEditorKeyHandling.cs); Shift+Enter keeps the new line.
/// </summary>
public sealed class ChatEditor : Editor
{
    public static readonly BindableProperty SendCommandProperty =
        BindableProperty.Create(nameof(SendCommand), typeof(ICommand), typeof(ChatEditor));

    public ICommand? SendCommand
    {
        get => (ICommand?)GetValue(SendCommandProperty);
        set => SetValue(SendCommandProperty, value);
    }
}
