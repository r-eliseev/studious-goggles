using System.Globalization;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Providers;
using ImageGen.Presentation.PageModels;

namespace ImageGen.Presentation.Tests.PageModels;

public class AssistantModelOptionViewModelTests
{
    [Fact]
    public void DetailsText_PolishCulture_UsesPointAsDecimalSeparator()
    {
        var model = new ChatModel(
            "vendor/assistant",
            "Assistant",
            SupportsImageInput: false,
            ContextLength: null,
            SupportsTools: true,
            PromptPricePerMillion: 0.1m,
            CompletionPricePerMillion: 0.5m);
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("pl-PL");
        try
        {
            var option = new AssistantModelOptionViewModel(
                new ModelRef(ProviderIds.OpenRouter, model.Id), model, isSelected: false, select: _ => { });

            Assert.Equal("vendor/assistant · 0.10 in, 0.50 out per 1M tokens (USD)", option.DetailsText);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
