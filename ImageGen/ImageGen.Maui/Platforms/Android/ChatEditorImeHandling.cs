using Android.Text;
using Android.Views;
using Android.Views.InputMethods;
using ImageGen.Maui.Controls;
using Microsoft.Maui.Handlers;

namespace ImageGen.Maui.Platforms.Android;

/// <summary>
/// The on-screen keyboard shows «Send» for the chat input and a hardware Enter sends the message, Shift+Enter keeps
/// the new line (same as the Windows and Avalonia shells). The field is single-line for the keyboard, so the line
/// break key does not hide the action.
/// </summary>
public static class ChatEditorImeHandling
{
    public static void Register() =>
        EditorHandler.Mapper.AppendToMapping("ChatEditorImeSend", (handler, view) =>
        {
            if (view is not ChatEditor editor)
            {
                return;
            }

            var field = handler.PlatformView;
            field.ImeOptions = ImeAction.Send;
            field.SetRawInputType(InputTypes.ClassText | InputTypes.TextFlagCapSentences);
            field.EditorAction += (_, args) =>
            {
                if (args.ActionId == ImeAction.Send)
                {
                    args.Handled = true;
                    Send(editor);
                }
            };
            field.KeyPress += (_, args) =>
            {
                if (args.KeyCode == Keycode.Enter && args.Event?.Action == KeyEventActions.Down && args.Event.IsShiftPressed == false)
                {
                    args.Handled = true;
                    Send(editor);
                }
            };
        });

    private static void Send(ChatEditor editor)
    {
        var command = editor.SendCommand;
        if (command?.CanExecute(null) == true)
        {
            command.Execute(null);
        }
    }
}
