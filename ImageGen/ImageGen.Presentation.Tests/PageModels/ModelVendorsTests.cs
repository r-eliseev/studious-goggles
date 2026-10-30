using ImageGen.Presentation.PageModels;

namespace ImageGen.Presentation.Tests.PageModels;

public class ModelVendorsTests
{
    [Fact]
    public void Group_OpenRouterName_UsesLabelBeforeColon()
    {
        var groups = Group(("openrouter", "black-forest-labs/flux.2-klein-4b", "Black Forest Labs: FLUX.2 Klein 4B"));

        var group = Assert.Single(groups);
        Assert.Equal("Black Forest Labs", group.Label);
        Assert.Equal("black forest labs", group.Key);
    }

    [Fact]
    public void Group_TwoLabelsTie_TakesFirstAlphabetically()
    {
        var groups = Group(
            ("openrouter", "x-ai/a", "xAI: A"),
            ("openrouter", "x-ai/b", "SpaceXAI: B"));

        var group = Assert.Single(groups);
        Assert.Equal("SpaceXAI", group.Label);
        Assert.Equal(2, group.Items.Count);
    }

    [Fact]
    public void Group_TildeAlias_JoinsVendor()
    {
        var groups = Group(
            ("openrouter", "~anthropic/claude-latest", "Anthropic: Claude Latest"),
            ("openrouter", "anthropic/claude-x", "Anthropic: Claude X"));

        var group = Assert.Single(groups);
        Assert.Equal("Anthropic", group.Label);
        Assert.Equal(2, group.Items.Count);
    }

    [Fact]
    public void Group_TwoPrefixesSameLabel_Merge()
    {
        var groups = Group(
            ("openrouter", "meta/x", "Meta: X"),
            ("openrouter", "meta-llama/y", "Meta: Y"));

        var group = Assert.Single(groups);
        Assert.Equal("Meta", group.Label);
        Assert.Equal(2, group.Items.Count);
    }

    [Fact]
    public void Group_NoLabelInPrefix_UsesPrefix()
    {
        var group = Assert.Single(Group(("openrouter", "openrouter/auto", "Auto Router")));

        Assert.Equal("openrouter", group.Label);
    }

    [Fact]
    public void Group_NameWithoutLabel_JoinsPrefixLabel()
    {
        var groups = Group(
            ("openrouter", "mistralai/mistral-large", "Mistral Large"),
            ("openrouter", "mistralai/small", "Mistral: Small"));

        var group = Assert.Single(groups);
        Assert.Equal("Mistral", group.Label);
        Assert.Equal(2, group.Items.Count);
    }

    [Fact]
    public void Group_FalNamespace_IsOther()
    {
        var group = Assert.Single(Group(("fal", "fal-ai/flux/dev", "FLUX.1 [dev]")));

        Assert.Equal(ModelVendors.OtherKey, group.Key);
        Assert.Equal("Other", group.Label);
    }

    [Fact]
    public void Group_FalVendorPrefix_JoinsOpenRouterVendor()
    {
        var group = Assert.Single(Group(
            ("fal", "openai/gpt-image-2", "GPT Image 2 API"),
            ("openrouter", "openai/gpt-image-1", "OpenAI: GPT Image 1")));

        Assert.Equal("OpenAI", group.Label);
        Assert.Equal(2, group.Items.Count);
    }

    [Fact]
    public void Group_IdWithoutSlash_IsOther()
    {
        var group = Assert.Single(Group(("local", "dall-e-3", "DALL-E 3")));

        Assert.Equal(ModelVendors.OtherKey, group.Key);
        Assert.Equal("Other", group.Label);
    }

    [Fact]
    public void Group_Order_AlphabeticalOtherLast()
    {
        var groups = Group(
            ("openrouter", "zeta/a", "Zeta: A"),
            ("openrouter", "alpha/b", "alpha: b"),
            ("openrouter", "alpha/a", "alpha: A"),
            ("fal", "fal-ai/flux/dev", "FLUX.1 [dev]"));

        Assert.Equal(["alpha", "Zeta", "Other"], groups.Select(group => group.Label));
        Assert.Equal(["alpha: A", "alpha: b"], groups[0].Items.Select(item => item.name));
    }

    [Fact]
    public void Group_Empty_ReturnsEmpty()
    {
        Assert.Empty(ModelVendors.Group(
            Array.Empty<(string provider, string id, string name)>(),
            item => item.provider,
            item => item.id,
            item => item.name));
    }

    private static IReadOnlyList<VendorGroup<(string provider, string id, string name)>> Group(
        params (string provider, string id, string name)[] items) =>
        ModelVendors.Group(items, item => item.provider, item => item.id, item => item.name);
}
