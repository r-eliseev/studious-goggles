using ImageGen.Core.Domain.Descriptions;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Providers;
using ImageGen.Data.Repositories;

namespace ImageGen.Data.Tests;

public class ImageDescriptionRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AddAsync_ThenList_ReturnsDescriptionWithPrice()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var sessionId = await CreateSessionAsync(test, ct);
        var repository = new ImageDescriptionRepository(test.Database);
        var description = Description(sessionId, costUsd: 0.0004m);

        await repository.AddAsync(description, ct);

        var stored = Assert.Single(await repository.ListBySessionAsync(sessionId, ct));
        Assert.Equal(description.Text, stored.Text);
        Assert.Equal(DescriptionMode.Prompt, stored.Mode);
        Assert.Equal(ProviderIds.OpenRouter, stored.ProviderId);
        Assert.Equal(0.0004m, stored.CostUsd);
        Assert.Equal(description.SourceGenerationId, stored.SourceGenerationId);
    }

    [Fact]
    public async Task AddAsync_NoPrice_ReadsBackAsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var sessionId = await CreateSessionAsync(test, ct);
        var repository = new ImageDescriptionRepository(test.Database);

        await repository.AddAsync(Description(sessionId, costUsd: null), ct);

        Assert.Null(Assert.Single(await repository.ListBySessionAsync(sessionId, ct)).CostUsd);
    }

    private static ImageDescription Description(Guid sessionId, decimal? costUsd) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = sessionId,
        SourceGenerationId = Guid.NewGuid(),
        Mode = DescriptionMode.Prompt,
        ProviderId = ProviderIds.OpenRouter,
        ModelId = "vendor/vision",
        Text = "A red fox in snow.",
        CostUsd = costUsd,
        CreatedAt = Now,
    };

    private static async Task<Guid> CreateSessionAsync(TestDatabase test, CancellationToken ct)
    {
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Title = "S",
            CreatedAt = Now,
            UpdatedAt = Now,
            Settings = SessionSettings.CreateDefault(),
        };
        await new ImageGen.Data.Repositories.SessionRepository(test.Database).AddAsync(session, ct);
        return session.Id;
    }
}
