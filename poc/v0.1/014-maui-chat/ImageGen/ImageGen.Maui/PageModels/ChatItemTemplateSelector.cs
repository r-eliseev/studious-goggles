namespace ImageGen.Maui.PageModels;

/// <summary>Chooses the chat row template: message or run.</summary>
public sealed class ChatItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate? MessageTemplate { get; set; }

    public DataTemplate? RunTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container) =>
        (item is RunItemViewModel ? RunTemplate : MessageTemplate)
        ?? throw new InvalidOperationException("Chat templates are not set.");
}
