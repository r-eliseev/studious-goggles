using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Providers;
using ImageGen.Data.Repositories;
using Microsoft.Data.Sqlite;

namespace ImageGen.Data.Tests;

public class GenerationParameterStorageTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task InitializeAsync_FromVersionTwo_KeepsRowsAndLeavesNewParametersEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        var directory = Path.Combine(Path.GetTempPath(), "imagegen-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, SqliteDatabase.FileName);
        try
        {
            await using (var connection = new SqliteConnection($"Data Source={path}"))
            {
                await connection.OpenAsync(ct);
                foreach (var migration in Migrations.All.Take(2))
                {
                    await using var apply = connection.CreateCommand();
                    apply.CommandText = migration.Script;
                    await apply.ExecuteNonQueryAsync(ct);
                }

                await using var insert = connection.CreateCommand();
                insert.CommandText = """
                    INSERT INTO image_models (provider_id, id, name, supports_seed, supports_image_input, max_images_per_request, fetched_at)
                    VALUES ('openrouter', 'vendor/old', 'Old', 1, 0, 1, '2026-10-27T10:00:00Z');
                    """;
                await insert.ExecuteNonQueryAsync(ct);

                await using var version = connection.CreateCommand();
                version.CommandText = $"PRAGMA user_version = {Migrations.All[1].Number};";
                await version.ExecuteNonQueryAsync(ct);
            }

            await InitializeThroughVersionAsync(path, 3, ct);

            await using var check = new SqliteConnection($"Data Source={path}");
            await check.OpenAsync(ct);
            await using var command = check.CreateCommand();
            command.CommandText = "SELECT supported_parameters_json FROM image_models WHERE id = 'vendor/old';";
            Assert.Equal(DBNull.Value, await command.ExecuteScalarAsync(ct));
            command.CommandText = "PRAGMA user_version;";
            Assert.Equal(3L, await command.ExecuteScalarAsync(ct));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task InitializeThroughVersionAsync(string path, int version, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={path}");
        await connection.OpenAsync(cancellationToken);
        foreach (var migration in Migrations.All.Where(migration => migration.Number > 2 && migration.Number <= version))
        {
            await using var apply = connection.CreateCommand();
            apply.CommandText = migration.Script;
            await apply.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var updateVersion = connection.CreateCommand();
        updateVersion.CommandText = $"PRAGMA user_version = {version};";
        await updateVersion.ExecuteNonQueryAsync(cancellationToken);
    }

    [Fact]
    public async Task ImageModelRepository_SavesAndReturnsCatalogParameters()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var repository = new ImageModelRepository(test.Database);
        var parameters = new List<ModelParameter>
        {
            new("aspect_ratio", ParameterKind.Enum, ["1:1", "3:2", "auto"]),
            new("n", ParameterKind.Range, [], 1, 4),
        };

        await repository.ReplaceAllAsync(ProviderIds.OpenRouter, [Model("a", parameters), Model("b", null)], ct);

        var models = await repository.ListAsync(ProviderIds.OpenRouter, ct);
        var withParameters = models.Single(m => m.Id == "a");
        Assert.Equal(["1:1", "3:2", "auto"], withParameters.Parameters!.Single(p => p.Name == "aspect_ratio").Values);
        Assert.Equal(4, withParameters.Parameters!.Single(p => p.Name == "n").Max);
        Assert.Null(models.Single(m => m.Id == "b").Parameters);
    }

    [Fact]
    public async Task RunRepository_SavesAndReturnsRunParametersAndGenerationOutcomes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Title = "S",
            CreatedAt = Now,
            UpdatedAt = Now,
            Settings = SessionSettings.CreateDefault(),
        };
        await new SessionRepository(test.Database).AddAsync(session, ct);
        var repository = new RunRepository(test.Database);
        var run = new Run
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Prompt = "fox",
            Origin = RunOrigin.Assistant,
            CreatedAt = Now,
            Parameters = new GenerationParameters { AspectRatio = "3:2", OutputFormat = "webp" },
        };
        var generation = new Generation
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            SessionId = session.Id,
            ProviderId = ProviderIds.OpenRouter,
            ModelId = "vendor/a",
            Prompt = "fox",
            Seed = 7,
            SeedApplied = true,
            Status = GenerationStatus.Queued,
            CreatedAt = Now,
            SortOrder = 0,
            Outcomes =
            [
                new ParameterOutcome("aspect_ratio", ParameterOutcomeKind.Replaced, "3:2", "4:3"),
                new ParameterOutcome("output_format", ParameterOutcomeKind.Dropped, "webp", null, ParameterDropReason.NoAllowedValue),
            ],
        };

        await repository.AddRunAsync(run, [generation], ct);

        var stored = await repository.GetRunAsync(run.Id, ct);
        Assert.Equal("3:2", stored!.Parameters.AspectRatio);
        Assert.Equal("webp", stored.Parameters.OutputFormat);
        var outcomes = (await repository.GetGenerationAsync(generation.Id, ct))!.Outcomes;
        Assert.Equal(2, outcomes.Count);
        Assert.Equal(ParameterOutcomeKind.Replaced, outcomes[0].Kind);
        Assert.Equal("4:3", outcomes[0].Sent);
        Assert.Equal(ParameterDropReason.NoAllowedValue, outcomes[1].Reason);
    }

    [Fact]
    public async Task RunRepository_SavesAndReturnsReferenceAttachmentIdsInOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Title = "S",
            CreatedAt = Now,
            UpdatedAt = Now,
            Settings = SessionSettings.CreateDefault(),
        };
        await new SessionRepository(test.Database).AddAsync(session, ct);
        var repository = new RunRepository(test.Database);
        var run = new Run { Id = Guid.NewGuid(), SessionId = session.Id, Prompt = "fox", Origin = RunOrigin.Assistant, CreatedAt = Now };
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var generation = new Generation
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            SessionId = session.Id,
            ProviderId = ProviderIds.OpenRouter,
            ModelId = "vendor/a",
            Prompt = "fox",
            Seed = 1,
            SeedApplied = true,
            Status = GenerationStatus.Queued,
            CreatedAt = Now,
            SortOrder = 0,
            ReferenceAttachmentIds = [first, second],
        };

        await repository.AddRunAsync(run, [generation], ct);

        Assert.Equal([first, second], (await repository.GetGenerationAsync(generation.Id, ct))!.ReferenceAttachmentIds);
    }

    private static ImageModel Model(string id, IReadOnlyList<ModelParameter>? parameters) => new()
    {
        ProviderId = ProviderIds.OpenRouter,
        Id = id,
        Name = id,
        SupportsSeed = true,
        SupportsImageInput = false,
        MaxImagesPerRequest = 1,
        FetchedAt = Now,
        Parameters = parameters,
    };
}
