namespace ImageGen.Presentation.PageModels;

public sealed record AssistantProviderOption(string Id, string Name)
{
    public string DisplayName => $"{Name} ({Id})";
}
