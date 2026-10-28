using ImageGen.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace ImageGen.Maui.Platforms.Windows;

/// <summary>
/// Enter in the chat input sends the message, Shift+Enter adds a new line (same as the Avalonia shell). The WinUI
/// TextBox inserts the line break on KeyDown, so the key is taken earlier, on PreviewKeyDown.
/// </summary>
public static class ChatEditorKeyHandling
{
    public static void Register() =>
        EditorHandler.Mapper.AppendToMapping("ChatEditorEnterToSend", (handler, view) =>
        {
            if (view is ChatEditor editor)
            {
                handler.PlatformView.PreviewKeyDown += (_, args) => OnPreviewKeyDown(editor, args);
            }
        });

    private static void OnPreviewKeyDown(ChatEditor editor, KeyRoutedEventArgs args)
    {
        if (args.Key != VirtualKey.Enter || IsShiftDown())
        {
            return;
        }

        // Enter never adds a line here, even when the message cannot be sent yet (empty input, assistant busy).
        args.Handled = true;
        var command = editor.SendCommand;
        if (command?.CanExecute(null) == true)
        {
            command.Execute(null);
        }
    }

    private static bool IsShiftDown() =>
        InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
}
