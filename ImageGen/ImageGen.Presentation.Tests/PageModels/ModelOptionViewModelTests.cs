using System.ComponentModel;
using ImageGen.Core.Domain.Models;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Tests.TestDoubles;

namespace ImageGen.Presentation.Tests.PageModels;

public class ModelOptionViewModelTests
{
    [Fact]
    public void Constructor_CopiesIdAndName()
    {
        var model = new ModelOptionViewModel(TestModels.Model("vendor/alpha", "Alpha"), "OpenRouter", isSelected: false);

        Assert.Equal("vendor/alpha", model.Id);
        Assert.Equal("Alpha", model.Name);
    }

    [Fact]
    public void ProviderText_IncludesDisplayNameAndId()
    {
        var model = new ModelOptionViewModel(TestModels.Model("vendor/alpha", "Alpha"), "OpenRouter", false);

        Assert.Equal("OpenRouter (openrouter)", model.ProviderText);
        Assert.Equal("Alpha, OpenRouter (openrouter)", model.AccessibilityName);
    }

    [Fact]
    public void SameModelNameFromDifferentProviders_HasDistinctAccessibleLabels()
    {
        var openRouter = TestModels.Model("vendor/model", "Same name");
        var together = TestModels.Model("vendor/model", "Same name");
        together.ProviderId = "together";

        var first = new ModelOptionViewModel(openRouter, "OpenRouter", false);
        var second = new ModelOptionViewModel(together, "Together AI", false);

        Assert.NotEqual(first.ProviderText, second.ProviderText);
        Assert.NotEqual(first.AccessibilityName, second.AccessibilityName);
    }

    [Fact]
    public void DetailsText_SeedSupported_ShowsIdAndSeed()
    {
        var model = new ModelOptionViewModel(TestModels.Model("vendor/alpha", "Alpha", supportsSeed: true), "OpenRouter", false);

        Assert.Equal("vendor/alpha · seed · price unknown", model.DetailsText);
    }

    [Fact]
    public void DetailsText_SeedNotSupported_ShowsIdAndNoSeed()
    {
        var model = new ModelOptionViewModel(TestModels.Model("vendor/beta", "Beta", supportsSeed: false), "OpenRouter", false);

        Assert.Equal("vendor/beta · no seed · price unknown", model.DetailsText);
    }

    [Fact]
    public void DetailsText_WithPrice_ShowsPrice()
    {
        var imageModel = TestModels.Model("black-forest-labs/flux.2-klein-4b", "FLUX.2 Klein 4B");
        imageModel.Prices = [new ImageModelPrice("output_image", ImagePriceUnit.Megapixel, 0.014m, null)];
        var model = new ModelOptionViewModel(imageModel, "OpenRouter", isSelected: false);

        Assert.Equal("black-forest-labs/flux.2-klein-4b · seed · $0.014 / MP", model.DetailsText);
        Assert.Equal("$0.014 / MP", model.PriceText);
    }

    [Fact]
    public void IsSelected_StartsWithConstructorValue()
    {
        var model = new ModelOptionViewModel(TestModels.Model("vendor/alpha", "Alpha"), "OpenRouter", isSelected: true);

        Assert.True(model.IsSelected);
    }

    [Fact]
    public void IsSelected_Change_RaisesPropertyChanged()
    {
        var model = new ModelOptionViewModel(TestModels.Model("vendor/alpha", "Alpha"), "OpenRouter", isSelected: false);
        var changed = new List<string?>();
        model.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        model.IsSelected = true;

        Assert.True(model.IsSelected);
        Assert.Contains(nameof(ModelOptionViewModel.IsSelected), changed);
    }
}
