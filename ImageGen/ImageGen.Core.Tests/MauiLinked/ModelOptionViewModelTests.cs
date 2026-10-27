using System.ComponentModel;
using ImageGen.Core.Tests.Services;
using ImageGen.Maui.PageModels;

namespace ImageGen.Core.Tests.MauiLinked;

public class ModelOptionViewModelTests
{
    [Fact]
    public void Constructor_CopiesIdAndName()
    {
        var model = new ModelOptionViewModel(ModelCatalogServiceTests.Model("vendor/alpha", "Alpha"), isSelected: false);

        Assert.Equal("vendor/alpha", model.Id);
        Assert.Equal("Alpha", model.Name);
    }

    [Fact]
    public void DetailsText_SeedSupported_ShowsIdAndSeed()
    {
        var model = new ModelOptionViewModel(ModelCatalogServiceTests.Model("vendor/alpha", "Alpha", supportsSeed: true), false);

        Assert.Equal("vendor/alpha · seed", model.DetailsText);
    }

    [Fact]
    public void DetailsText_SeedNotSupported_ShowsIdAndNoSeed()
    {
        var model = new ModelOptionViewModel(ModelCatalogServiceTests.Model("vendor/beta", "Beta", supportsSeed: false), false);

        Assert.Equal("vendor/beta · no seed", model.DetailsText);
    }

    [Fact]
    public void IsSelected_StartsWithConstructorValue()
    {
        var model = new ModelOptionViewModel(ModelCatalogServiceTests.Model("vendor/alpha", "Alpha"), isSelected: true);

        Assert.True(model.IsSelected);
    }

    [Fact]
    public void IsSelected_Change_RaisesPropertyChanged()
    {
        var model = new ModelOptionViewModel(ModelCatalogServiceTests.Model("vendor/alpha", "Alpha"), isSelected: false);
        var changed = new List<string?>();
        model.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        model.IsSelected = true;

        Assert.True(model.IsSelected);
        Assert.Contains(nameof(ModelOptionViewModel.IsSelected), changed);
    }
}
