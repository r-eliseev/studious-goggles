using ImageGen.Core.Domain;
using ImageGen.Data.Repositories;

namespace ImageGen.Data.Tests.Repositories;

public class ImageModelRepositoryTests
{
    [Fact]
    public async Task ReplaceAllAsync_TwiceWithDifferentLists_KeepsOnlyLastListSortedByName()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var repository = new ImageModelRepository(test.Database);

        await repository.ReplaceAllAsync([Model("old/model", "Old")], ct);
        await repository.ReplaceAllAsync([Model("z/z", "Zeta"), Model("a/a", "Alpha")], ct);

        var list = await repository.ListAsync(ct);
        Assert.Equal(["Alpha", "Zeta"], list.Select(m => m.Name));
        Assert.True(list[0].SupportsSeed);
        Assert.Equal(4, list[0].MaxImagesPerRequest);
        Assert.Equal(TestData.BaseTime, list[0].FetchedAt);
    }

    private static ImageModel Model(string id, string name) => new()
    {
        Id = id,
        Name = name,
        SupportsSeed = true,
        SupportsImageInput = false,
        MaxImagesPerRequest = 4,
        FetchedAt = TestData.BaseTime,
    };
}
