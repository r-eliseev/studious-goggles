using ImageGen.Core.Domain.Descriptions;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Search;
using ImageGen.Data.Repositories;

namespace ImageGen.Data.Tests.Repositories;

public class SearchTextSourceTests
{
    [Fact]
    public async Task ListAll_ReturnsTitlesPromptsAndDescriptions()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var session = TestData.Session();
        session.Title = "Fox title";
        await new SessionRepository(test.Database).AddAsync(session, ct);
        var runs = new RunRepository(test.Database);
        var run = TestData.Run(session.Id, TestData.BaseTime);
        run.Prompt = "red fox in snow";
        await runs.AddRunAsync(run, [TestData.Generation(run, 0)], ct);
        await new ImageDescriptionRepository(test.Database).AddAsync(new ImageDescription
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            SourceGenerationId = Guid.NewGuid(),
            Mode = DescriptionMode.Description,
            ProviderId = "openrouter",
            ModelId = "vision",
            Text = "a fox sitting on a rock",
            CreatedAt = TestData.BaseTime,
        }, ct);

        var texts = await new SearchTextSource(test.Database).ListAllAsync(ct);

        Assert.Contains(texts, text => text.SessionId == session.Id && text.Kind == SearchTextKind.Title && text.Text == "Fox title");
        Assert.Contains(texts, text => text.SessionId == session.Id && text.Kind == SearchTextKind.Prompt && text.Text == "red fox in snow");
        Assert.Contains(texts, text => text.SessionId == session.Id && text.Kind == SearchTextKind.Description && text.Text == "a fox sitting on a rock");
    }

    [Fact]
    public async Task ListSourcesAsync_UsesStableKeysForTitleRunPromptAndDescription()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var session = TestData.Session();
        session.Title = "Fox title";
        await new SessionRepository(test.Database).AddAsync(session, ct);
        var run = TestData.Run(session.Id, TestData.BaseTime);
        run.Prompt = "red fox in snow";
        await new RunRepository(test.Database).AddRunAsync(run, [TestData.Generation(run, 0)], ct);
        var description = new ImageGen.Core.Domain.Descriptions.ImageDescription
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            SourceGenerationId = Guid.NewGuid(),
            Mode = ImageGen.Core.Domain.Descriptions.DescriptionMode.Description,
            ProviderId = "openrouter",
            ModelId = "vision",
            Text = "a fox sitting on a rock",
            CreatedAt = TestData.BaseTime,
        };
        await new ImageDescriptionRepository(test.Database).AddAsync(description, ct);

        var sources = await new SearchTextSource(test.Database).ListSourcesAsync(ct);

        Assert.Contains(sources, source => source.SourceKey == $"title:{session.Id:D}" && source.SessionId == session.Id && source.Kind == SearchTextKind.Title && source.Text == "Fox title");
        Assert.Contains(sources, source => source.SourceKey == $"prompt:{run.Id:D}" && source.SessionId == session.Id && source.Kind == SearchTextKind.Prompt && source.Text == "red fox in snow");
        Assert.Contains(sources, source => source.SourceKey == $"desc:{description.Id:D}" && source.SessionId == session.Id && source.Kind == SearchTextKind.Description && source.Text == "a fox sitting on a rock");
        Assert.Equal(3, await new SearchTextSource(test.Database).CountSourcesAsync(ct));
    }

    [Fact]
    public async Task ListSourcesByKeysAsync_ReturnsOnlyRequestedExistingKeys()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var session = TestData.Session();
        session.Title = "Fox title";
        await new SessionRepository(test.Database).AddAsync(session, ct);
        var run = TestData.Run(session.Id, TestData.BaseTime);
        run.Prompt = "red fox in snow";
        await new RunRepository(test.Database).AddRunAsync(run, [TestData.Generation(run, 0)], ct);
        var source = new SearchTextSource(test.Database);

        var sources = await source.ListSourcesByKeysAsync(
            [$"title:{session.Id:D}", $"prompt:{run.Id:D}", "prompt:missing"], ct);
        var empty = await source.ListSourcesByKeysAsync([], ct);

        Assert.Equal(2, sources.Count);
        Assert.Contains(sources, item => item.SourceKey == $"title:{session.Id:D}" && item.Text == "Fox title");
        Assert.Contains(sources, item => item.SourceKey == $"prompt:{run.Id:D}" && item.Text == "red fox in snow");
        Assert.Empty(empty);
    }

    [Fact]
    public async Task ListSourcesByKeysAsync_GroupsMixedKeysAndSkipsUnknownOrUnreadableKeys()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var session = TestData.Session();
        session.Title = "Fox title";
        await new SessionRepository(test.Database).AddAsync(session, ct);
        var run = TestData.Run(session.Id, TestData.BaseTime);
        run.Prompt = "red fox in snow";
        await new RunRepository(test.Database).AddRunAsync(run, [TestData.Generation(run, 0)], ct);
        var description = new ImageDescription
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            SourceGenerationId = Guid.NewGuid(),
            Mode = DescriptionMode.Description,
            ProviderId = "openrouter",
            ModelId = "vision",
            Text = "a fox sitting on a rock",
            CreatedAt = TestData.BaseTime,
        };
        await new ImageDescriptionRepository(test.Database).AddAsync(description, ct);

        var sources = await new SearchTextSource(test.Database).ListSourcesByKeysAsync(
            [$"title:{session.Id:D}", $"prompt:{run.Id:D}", $"desc:{description.Id:D}", "other:123", "prompt:not-a-guid"], ct);

        Assert.Equal(3, sources.Count);
        Assert.Contains(sources, source => source.SourceKey == $"title:{session.Id:D}");
        Assert.Contains(sources, source => source.SourceKey == $"prompt:{run.Id:D}");
        Assert.Contains(sources, source => source.SourceKey == $"desc:{description.Id:D}");
    }

    [Fact]
    public async Task PromptKeyQueryPlan_SearchesRunsByPrimaryKey()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        await using var connection = await test.Database.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN " + string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            SearchTextSource.PromptSourcesByKeysQuery,
            "@k0");
        command.Parameters.AddWithValue("@k0", Guid.NewGuid().ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(ct);
        var details = new List<string>();
        while (await reader.ReadAsync(ct))
        {
            details.Add(reader.GetString(3));
        }

        Assert.NotEmpty(details);
        Assert.DoesNotContain(details, detail => detail.Contains("SCAN runs", StringComparison.OrdinalIgnoreCase));
    }
}
