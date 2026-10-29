using Avalonia.Input;

namespace ImageGen.Avalonia.Services;

/// <summary>The modifier of the app's shortcuts: Cmd on macOS, Ctrl on Windows and Linux (task 073).</summary>
public static class PlatformKeys
{
    public static KeyModifiers Primary => For(OperatingSystem.IsMacOS());

    public static KeyModifiers For(bool isMacOs) => isMacOs ? KeyModifiers.Meta : KeyModifiers.Control;

    /// <summary>The shortcut as a menu shows it: «⌘N» on macOS, «Ctrl+N» elsewhere.</summary>
    public static string Label(string key, bool isMacOs) => isMacOs ? "⌘" + key : "Ctrl+" + key;
}
