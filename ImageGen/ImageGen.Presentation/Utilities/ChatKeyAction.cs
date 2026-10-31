namespace ImageGen.Presentation.Utilities;

/// <summary>What the chat input does with one hardware key event (<see cref="ChatInputKeys.Decide"/>).</summary>
public enum ChatKeyAction
{
    /// <summary>The field handles the key as usual: typing, Backspace, cursor keys.</summary>
    PassToField,

    /// <summary>The key is consumed and nothing else happens.</summary>
    Swallow,

    /// <summary>The key is consumed and the message is sent.</summary>
    Send,
}
