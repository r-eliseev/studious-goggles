using ImageGen.Presentation.PageModels;

namespace ImageGen.Maui.PageModels;

/// <summary>
/// Picks the template of a chat row: <see cref="RunTemplate"/> for runs, <see cref="SummaryTemplate"/> for the summary line
/// (task 084), <see cref="MessageTemplate"/> otherwise.
/// </summary>
public sealed class ChatItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate? MessageTemplate { get; set; }

    public DataTemplate? RunTemplate { get; set; }

    public DataTemplate? SummaryTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container) =>
        ChatItemTemplateChoice.Pick(item, MessageTemplate, RunTemplate, SummaryTemplate);
}
