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
}
