namespace ImageGen.Presentation.PageModels;

/// <summary>
/// Chooses the template of a chat row. Kept free of MAUI types so that it is tested on Linux;
/// <c>ChatItemTemplateSelector</c> passes its two templates here.
/// </summary>
public static class ChatItemTemplateChoice
{
    /// <summary>
    /// Returns the template of the row kind: <paramref name="runTemplate"/> for a run, <paramref name="summaryTemplate"/> for
    /// the summary line (task 084), otherwise <paramref name="messageTemplate"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The template for the row kind is not set.</exception>
    public static T Pick<T>(object item, T? messageTemplate, T? runTemplate, T? summaryTemplate = null) where T : class
    {
        ArgumentNullException.ThrowIfNull(item);
        var template = item switch
        {
            RunItemViewModel => runTemplate,
            SummaryMarkerViewModel => summaryTemplate,
            _ => messageTemplate,
        };
        return template ?? throw new InvalidOperationException(
            $"No chat template is set for {item.GetType().Name}.");
    }
}
