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
}
