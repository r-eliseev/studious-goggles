namespace ImageGen.Presentation.PageModels;

/// <summary>
/// Text of an assistant message that started a run, as shown in the chat. Messages saved before task 057 end with
/// a <c>Prompt: &lt;prompt&gt;</c> line that repeats the run block, so the line is removed here, not in the database.
/// </summary>
public static class RunMessageText
{
    public static string Visible(string text, string runPrompt)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(runPrompt);

        var promptLine = "Prompt: " + runPrompt;
        if (text.Trim() == promptLine)
        {
            return string.Empty;
        }

        var suffix = "\n\n" + promptLine;
        return text.EndsWith(suffix, StringComparison.Ordinal)
            ? text[..^suffix.Length].TrimEnd()
            : text;
    }
}
