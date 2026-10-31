namespace ImageGen.Presentation.Utilities;

/// <summary>
/// The key rule of the chat input on platforms that report key down and key up separately (Android, task 134):
/// Enter sends on key down, its key up is consumed so the field does not run its own send action again, Shift+Enter and
/// every other key (Backspace among them) go to the field untouched.
/// </summary>
public static class ChatInputKeys
{
    public static ChatKeyAction Decide(bool isEnter, bool isKeyDown, bool isShiftPressed)
    {
        if (!isEnter || isShiftPressed)
        {
            return ChatKeyAction.PassToField;
        }

        return isKeyDown ? ChatKeyAction.Send : ChatKeyAction.Swallow;
    }
}
