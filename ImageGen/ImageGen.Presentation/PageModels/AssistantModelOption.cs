using ImageGen.Core.Domain.Models;

namespace ImageGen.Presentation.PageModels;

public sealed record AssistantModelOption(ModelRef Reference, string Name, string ProviderName)
{
    public string DisplayName => $"{Name} · {ProviderName} ({Reference.ProviderId})";
}
