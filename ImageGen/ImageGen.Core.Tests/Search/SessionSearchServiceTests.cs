using ImageGen.Core.Search;

namespace ImageGen.Core.Tests.Search;

public class SessionSearchServiceTests
{
    private static readonly Guid First = Guid.NewGuid();
    private static readonly Guid Second = Guid.NewGuid();

    private static SessionSearchService Create(params SearchText[] texts)
    {
        var source = Substitute.For<ISearchTextSource>();
        source.ListAllAsync(Arg.Any<CancellationToken>()).Returns(texts);
        return new SessionSearchService(source);
    }

    [Fact]
    public async Task Search_TitleBeatsPrompt()
    {
        var service = Create(
            new SearchText(First, SearchTextKind.Prompt, "red fox in snow"),
            new SearchText(Second, SearchTextKind.Title, "red fox"));

        var hits = await service.SearchAsync("fox", TestContext.Current.CancellationToken);

        Assert.Equal([Second, First], hits.Select(hit => hit.SessionId));
    }

    [Fact]
    public async Task Search_SnippetHasEllipsisAndStaysShort()
    {
        var text = new string('a', 150) + " lighthouse " + new string('b', 100);
        var service = Create(new SearchText(First, SearchTextKind.Prompt, text));

        var hit = (await service.SearchAsync("lighthouse", TestContext.Current.CancellationToken)).Single();

        Assert.StartsWith("…", hit.Snippet);
        Assert.EndsWith("…", hit.Snippet);
        Assert.Contains("lighthouse", hit.Snippet);
        Assert.True(hit.Snippet.Length <= 80);
    }

    [Fact]
    public async Task Search_EmptyQuery_ReturnsEmpty()
    {
        var service = Create(new SearchText(First, SearchTextKind.Title, "fox"));

        var hits = await service.SearchAsync("   ", TestContext.Current.CancellationToken);

        Assert.Empty(hits);
    }

    [Fact]
    public async Task Search_GroupsBySession_KeepsBestHit()
    {
        var service = Create(
            new SearchText(First, SearchTextKind.Prompt, "fox"),
            new SearchText(First, SearchTextKind.Title, "fox"),
            new SearchText(First, SearchTextKind.Description, "fox"));

        var hit = Assert.Single(await service.SearchAsync("fox", TestContext.Current.CancellationToken));

        Assert.Equal(SearchTextKind.Title, hit.Kind);
        Assert.Equal(3 * 3, hit.Score);
    }
}
