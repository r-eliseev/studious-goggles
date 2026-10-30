using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Providers;

namespace ImageGen.Core.Tests.Providers;

public class ProviderConfigLoaderTests
{
    private const string BuiltIn = """
        {"providers":[
          {"id":"openrouter","kind":"openRouter","displayName":"OpenRouter",
           "baseAddress":"https://openrouter.ai/api/v1/","timeoutSeconds":180}
        ]}
        """;

    [Fact]
    public void BuiltIn_RegistersFalProvider()
    {
        var registry = ProviderConfigLoader.Build(ProviderConfigLoader.ReadBuiltInJson(), null);

        var fal = registry.Get(ProviderIds.Fal);
        Assert.Equal("fal", fal.Id);
        Assert.Equal(ProviderKind.Fal, fal.Kind);
        Assert.Equal("fal.ai", fal.DisplayName);
        Assert.Equal(new Uri("https://queue.fal.run/"), fal.BaseAddress);
        Assert.Equal(TimeSpan.FromSeconds(180), fal.Timeout);
        Assert.Empty(fal.ImageModels);
    }

    [Fact]
    public void Parse_ValidFile_ReturnsProviders()
    {
        var providers = ProviderConfigLoader.Parse(BuiltIn);

        var provider = Assert.Single(providers);
        Assert.Equal("openrouter", provider.Id);
        Assert.Equal(ProviderKind.OpenRouter, provider.Kind);
        Assert.Equal("OpenRouter", provider.DisplayName);
        Assert.Equal(new Uri("https://openrouter.ai/api/v1/"), provider.BaseAddress);
        Assert.Equal(TimeSpan.FromSeconds(180), provider.Timeout);
        Assert.Empty(provider.ImageModels);
    }

    [Fact]
    public void Parse_OpenAiCompatibleWithImageModels_ReadsModels()
    {
        const string json = """
            {"providers":[{"id":"my-images","kind":"openAiCompatible","displayName":"My images",
              "baseAddress":"http://localhost:8080/v1","timeoutSeconds":60,
              "imageModels":[{"id":"sd-xl","name":"SD XL","supportsSeed":true,"maxImagesPerRequest":2}]}]}
            """;

        var provider = Assert.Single(ProviderConfigLoader.Parse(json));

        Assert.Equal(ProviderKind.OpenAiCompatible, provider.Kind);
        var model = Assert.Single(provider.ImageModels);
        Assert.Equal("sd-xl", model.Id);
        Assert.Equal("SD XL", model.Name);
        Assert.True(model.SupportsSeed);
        Assert.Equal(2, model.MaxImagesPerRequest);
    }

    [Theory]
    [InlineData("carrier")]
    [InlineData("OpenRouter")]
    public void Parse_UnknownKind_ThrowsNamingKindAndProvider(string kind)
    {
        var json = $$"""{"providers":[{"id":"x","kind":"{{kind}}","displayName":"X","baseAddress":"https://x.test/v1/","timeoutSeconds":10}]}""";

        var ex = Assert.Throws<ProviderConfigException>(() => ProviderConfigLoader.Parse(json));

        Assert.Contains(kind, ex.Message);
        Assert.Contains("'x'", ex.Message);
    }

    [Fact]
    public void Parse_OpenAiCompatibleImageFormats_ReadsOptions()
    {
        const string json = """{"providers":[{"id":"together","kind":"openAiCompatible","displayName":"Together AI","baseAddress":"https://api.together.ai/v1","imageResponseFormat":"base64","imageOutputFormat":"png"}]}""";

        var provider = Assert.Single(ProviderConfigLoader.Parse(json));

        Assert.Equal("base64", provider.ImageResponseFormat);
        Assert.Equal("png", provider.ImageOutputFormat);
    }

    [Fact]
    public void Parse_NullImageFormat_KeepsDefault()
    {
        const string json = """{"providers":[{"id":"x","kind":"openAiCompatible","displayName":"X","baseAddress":"https://x.test/v1","imageResponseFormat":null,"imageOutputFormat":null}]}""";

        var provider = Assert.Single(ProviderConfigLoader.Parse(json));

        Assert.Null(provider.ImageResponseFormat);
        Assert.Null(provider.ImageOutputFormat);
    }

    [Theory]
    [InlineData("imageResponseFormat", "\"url\"")]
    [InlineData("imageResponseFormat", "\"\"")]
    [InlineData("imageResponseFormat", "42")]
    [InlineData("imageOutputFormat", "\"webp\"")]
    [InlineData("imageOutputFormat", "false")]
    public void Parse_InvalidImageFormat_Throws(string name, string value)
    {
        var json = "{\"providers\":[{\"id\":\"x\",\"kind\":\"openAiCompatible\",\"displayName\":\"X\",\"baseAddress\":\"https://x.test/v1\",\"" + name + "\":" + value + "}]}";

        Assert.Throws<ProviderConfigException>(() => ProviderConfigLoader.Parse(json));
    }

    [Fact]
    public void Parse_ImageFormatOnOtherKind_Throws()
    {
        const string json = """{"providers":[{"id":"x","kind":"openRouter","displayName":"X","baseAddress":"https://x.test/v1","imageResponseFormat":"base64"}]}""";

        Assert.Throws<ProviderConfigException>(() => ProviderConfigLoader.Parse(json));
    }

    [Fact]
    public void Parse_ApiKeyField_ThrowsBecauseKeysAreNotStoredInConfig()
    {
        const string json = """{"providers":[{"id":"x","kind":"openRouter","displayName":"X","baseAddress":"https://x.test/","timeoutSeconds":10,"apiKey":"sk-secret"}]}""";

        var ex = Assert.Throws<ProviderConfigException>(() => ProviderConfigLoader.Parse(json));

        Assert.Contains("apiKey", ex.Message);
        Assert.DoesNotContain("sk-secret", ex.Message);
    }

    [Fact]
    public void Parse_MissingId_Throws()
    {
        const string json = """{"providers":[{"kind":"openRouter","displayName":"X","baseAddress":"https://x.test/","timeoutSeconds":10}]}""";

        var ex = Assert.Throws<ProviderConfigException>(() => ProviderConfigLoader.Parse(json));

        Assert.Contains("id", ex.Message);
    }

    [Fact]
    public void Parse_InvalidJson_ThrowsProviderConfigException()
    {
        var ex = Assert.Throws<ProviderConfigException>(() => ProviderConfigLoader.Parse("{not json"));

        Assert.Contains("JSON", ex.Message);
    }

    [Fact]
    public void Build_UserEntryWithBuiltInId_ReplacesBuiltIn()
    {
        const string user = """{"providers":[{"id":"openrouter","kind":"openRouter","displayName":"Proxy","baseAddress":"https://proxy.test/api/v1/","timeoutSeconds":30}]}""";

        var registry = ProviderConfigLoader.Build(BuiltIn, user);

        var provider = registry.Get("openrouter");
        Assert.Equal("Proxy", provider.DisplayName);
        Assert.Equal(new Uri("https://proxy.test/api/v1/"), provider.BaseAddress);
        Assert.Single(registry.All);
    }

    [Fact]
    public void Build_UserEntryWithNewId_AddsProvider()
    {
        const string user = """{"providers":[{"id":"local","kind":"openAiCompatible","displayName":"Local","baseAddress":"http://localhost:1234/v1/","timeoutSeconds":60}]}""";

        var registry = ProviderConfigLoader.Build(BuiltIn, user);

        Assert.Equal(["openrouter", "local"], registry.All.Select(p => p.Id));
    }

    [Fact]
    public void Build_NoUserFile_ReturnsBuiltInOnly()
    {
        var registry = ProviderConfigLoader.Build(BuiltIn, null);

        Assert.Equal(["openrouter"], registry.All.Select(p => p.Id));
    }

    [Theory]
    [InlineData("Bad Id")]
    [InlineData("-leading")]
    [InlineData("has/slash")]
    public void Registry_InvalidId_Throws(string id)
    {
        var json = $$"""{"providers":[{"id":"{{id}}","kind":"openRouter","displayName":"X","baseAddress":"https://x.test/","timeoutSeconds":10}]}""";

        Assert.Throws<ProviderConfigException>(() => ProviderConfigLoader.Build(json, null));
    }

    [Fact]
    public void Registry_DuplicateIdInOneFile_Throws()
    {
        const string json = """
            {"providers":[
              {"id":"a","kind":"openRouter","displayName":"A","baseAddress":"https://a.test/","timeoutSeconds":10},
              {"id":"a","kind":"openRouter","displayName":"B","baseAddress":"https://b.test/","timeoutSeconds":10}
            ]}
            """;

        var ex = Assert.Throws<ProviderConfigException>(() => ProviderConfigLoader.Build(json, null));

        Assert.Contains("'a'", ex.Message);
    }

    [Theory]
    [InlineData("/relative/path")]
    [InlineData("http://example.com/v1/")]
    public void Registry_AddressNotAbsoluteHttps_Throws(string address)
    {
        var json = $$"""{"providers":[{"id":"a","kind":"openRouter","displayName":"A","baseAddress":"{{address}}","timeoutSeconds":10}]}""";

        Assert.Throws<ProviderConfigException>(() => ProviderConfigLoader.Build(json, null));
    }

    [Fact]
    public void Registry_HttpLocalhost_IsAllowed()
    {
        var json = """{"providers":[{"id":"a","kind":"openAiCompatible","displayName":"A","baseAddress":"http://localhost:1234/v1","timeoutSeconds":10}]}""";

        var provider = ProviderConfigLoader.Build(json, null).Get("a");

        Assert.Equal(new Uri("http://localhost:1234/v1/"), provider.BaseAddress);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Registry_TimeoutNotPositive_Throws(int seconds)
    {
        var json = $$"""{"providers":[{"id":"a","kind":"openRouter","displayName":"A","baseAddress":"https://a.test/","timeoutSeconds":{{seconds}}}]}""";

        Assert.Throws<ProviderConfigException>(() => ProviderConfigLoader.Build(json, null));
    }

    [Fact]
    public void Registry_ImageModelsOnNonOpenAiProvider_Throws()
    {
        const string json = """{"providers":[{"id":"a","kind":"openRouter","displayName":"A","baseAddress":"https://a.test/","timeoutSeconds":10,"imageModels":[{"id":"m","name":"M","supportsSeed":false,"maxImagesPerRequest":1}]}]}""";

        var ex = Assert.Throws<ProviderConfigException>(() => ProviderConfigLoader.Build(json, null));

        Assert.Contains("imageModels", ex.Message);
    }

    [Fact]
    public void Get_UnknownId_ThrowsProviderExceptionWithoutNetworkCall()
    {
        var registry = ProviderConfigLoader.Build(BuiltIn, null);

        var ex = Assert.Throws<ProviderException>(() => registry.Get("fal"));

        Assert.Equal(ErrorKind.ModelUnavailable, ex.Kind);
        Assert.Contains("fal", ex.Message);
    }
}
