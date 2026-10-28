namespace ImageGen.Presentation.PageModels;

/// <summary>
/// Chooses the template of a chat row. Kept free of MAUI types so that it is tested on Linux;
/// <c>ChatItemTemplateSelector</c> passes its two templates here.
/// </summary>
public static class ChatItemTemplateChoice
{
    /// <summary>Returns <paramref name="runTemplate"/> for a run row, otherwise <paramref name="messageTemplate"/>.</summary>
    /// <exception cref="InvalidOperationException">The template for the row kind is not set.</exception>
    public static T Pick<T>(object item, T? messageTemplate, T? runTemplate) where T : class
    {
        ArgumentNullException.ThrowIfNull(item);
        var template = item is RunItemViewModel ? runTemplate : messageTemplate;
        return template ?? throw new InvalidOperationException(
            $"No chat template is set for {item.GetType().Name}.");
    }
}
