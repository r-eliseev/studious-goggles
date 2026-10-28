using System.Text.Json;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Providers;
using ImageGen.Core.Serialization;

namespace ImageGen.Core.Tests.Domain;

public class ModelRefTests
{
    [Fact]
    public void Parse_PrefixedText_SplitsProviderAndModel()
    {
        var reference = ModelRef.Parse("fal:fal-ai/flux/dev");

        Assert.Equal("fal", reference.ProviderId);
        Assert.Equal("fal-ai/flux/dev", reference.ModelId);
    }

    [Fact]
    public void Parse_TextWithoutProviderPrefix_UsesOpenRouter()
    {
        var reference = ModelRef.Parse("black-forest-labs/flux.2-klein-4b");

        Assert.Equal(ProviderIds.OpenRouter, reference.ProviderId);
        Assert.Equal("black-forest-labs/flux.2-klein-4b", reference.ModelId);
    }

    [Fact]
    public void Parse_LegacyModelWithColonSuffix_KeepsWholeTextAsModel()
    {
        // Free OpenRouter models carry ":free" after a slash; the text before the colon is not a provider id.
        var reference = ModelRef.Parse("google/gemma-4-31b-it:free");

        Assert.Equal(ProviderIds.OpenRouter, reference.ProviderId);
        Assert.Equal("google/gemma-4-31b-it:free", reference.ModelId);
    }

    [Fact]
    public void Parse_PrefixedModelWithColonSuffix_KeepsColonInModel()
    {
        var reference = ModelRef.Parse("openrouter:google/gemma-4-31b-it:free");

        Assert.Equal(ProviderIds.OpenRouter, reference.ProviderId);
        Assert.Equal("google/gemma-4-31b-it:free", reference.ModelId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("openrouter:")]
    [InlineData(":model")]
    public void Parse_EmptyProviderOrModel_Throws(string text)
    {
        Assert.Throws<ArgumentException>(() => ModelRef.Parse(text));
    }

    [Fact]
    public void ToString_ThenParse_ReturnsSameReference()
    {
        var reference = new ModelRef("openrouter", "google/gemma-4-31b-it:free");

        var restored = ModelRef.Parse(reference.ToString());

        Assert.Equal("openrouter:google/gemma-4-31b-it:free", reference.ToString());
        Assert.Equal(reference, restored);
    }

    [Fact]
    public void Json_LegacyStrings_DeserializeAsOpenRouterReferences()
    {
        const string json = """{"imageModelIds":["black-forest-labs/flux.2-klein-4b"],"runsPerModel":1,"assistantModelId":"google/gemma-4-31b-it:free"}""";

        var settings = JsonSerializer.Deserialize(json, CoreJsonContext.Default.SessionSettings)!;

        Assert.Equal(new ModelRef(ProviderIds.OpenRouter, "black-forest-labs/flux.2-klein-4b"), settings.ImageModels[0]);
        Assert.Equal(new ModelRef(ProviderIds.OpenRouter, "google/gemma-4-31b-it:free"), settings.AssistantModel);
    }
}
