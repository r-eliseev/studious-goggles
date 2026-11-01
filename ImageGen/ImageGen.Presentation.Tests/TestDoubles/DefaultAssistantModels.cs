using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;

namespace ImageGen.Presentation.Tests.TestDoubles;

internal static class DefaultAssistantModels
{
    public static IDefaultAssistantModel Create(ModelRef? current = null)
    {
        var result = Substitute.For<IDefaultAssistantModel>();
        result.Current.Returns(current ?? new ModelRef(ProviderIds.OpenRouter, SessionSettings.DefaultAssistantModelId));
        return result;
    }
}
