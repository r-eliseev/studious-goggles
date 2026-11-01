using ImageGen.Core.Domain.Generations;
using ImageGen.Data.Repositories;

namespace ImageGen.Data.Tests.Repositories;

public class ExpenseRepositoryTests
{
    [Fact]
    public async Task RunRepository_UpdateGeneration_PersistsExpenseOnceAndSessionDeletionKeepsIt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var sessions = new SessionRepository(test.Database);
        var session = TestData.Session();
        await sessions.AddAsync(session, ct);
        var runs = new RunRepository(test.Database);
        var run = TestData.Run(session.Id, TestData.BaseTime);
        var generation = TestData.Generation(run, 0);
        await runs.AddRunAsync(run, [generation], ct);

        generation.Status = GenerationStatus.Succeeded;
        generation.CostUsd = 0.25m;
        generation.CompletedAt = TestData.BaseTime.AddMinutes(1);
        await runs.UpdateGenerationAsync(generation, ct);
        await runs.UpdateGenerationAsync(generation, ct);

        var expenses = new ExpenseRepository(test.Database);
        Assert.Equal(0.25m, await expenses.SumAsync(ct));
        await sessions.DeleteAsync(session.Id, ct);
        Assert.Equal(0.25m, await expenses.SumAsync(ct));
    }

    [Fact]
    public async Task ExpenseRepository_NullCostIsNotRecordedAndZeroCostIsRecorded()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var session = TestData.Session();
        await new SessionRepository(test.Database).AddAsync(session, ct);
        var runs = new RunRepository(test.Database);
        var run = TestData.Run(session.Id, TestData.BaseTime);
        var generation = TestData.Generation(run, 0);
        await runs.AddRunAsync(run, [generation], ct);

        generation.Status = GenerationStatus.Failed;
        generation.CostUsd = null;
        await runs.UpdateGenerationAsync(generation, ct);
        var expenses = new ExpenseRepository(test.Database);
        Assert.Equal(0m, await expenses.SumAsync(ct));

        generation.CostUsd = 0m;
        await runs.UpdateGenerationAsync(generation, ct);
        Assert.Equal(0m, await expenses.SumAsync(ct));
        Assert.Equal(1, await expenses.CountAsync(ct));
    }

    [Fact]
    public async Task ExpenseRepository_MigrationBackfillsExistingGenerationAndIsIdempotent()
    {
        var ct = TestContext.Current.CancellationToken;
        var directory = Path.Combine(Path.GetTempPath(), "imagegen-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, SqliteDatabase.FileName);
        try
        {
            await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path};Foreign Keys=True"))
            {
                await connection.OpenAsync(ct);
                var assembly = typeof(SqliteDatabase).Assembly;
                for (var version = 1; version <= 9; version++)
                {
                    var name = $"Migrations.{version:D4}_{(version switch { 1 => "initial", 2 => "providers", 3 => "generation_parameters", 4 => "chat_attachments", 5 => "image_descriptions", 6 => "generation_jobs", 7 => "conversation_summaries", 8 => "message_error_kind", _ => "image_model_prices" })}.sql";
                    await using var stream = assembly.GetManifestResourceStream(name)!;
                    using var reader = new StreamReader(stream);
                    var sql = await reader.ReadToEndAsync(ct);
                    await using var command = connection.CreateCommand();
                    command.CommandText = sql + $"\nPRAGMA user_version = {version};";
                    await command.ExecuteNonQueryAsync(ct);
                }

                var session = TestData.Session();
                await using var seed = connection.CreateCommand();
                seed.CommandText = """
                    INSERT INTO sessions (id, title, created_at, updated_at, settings_json) VALUES (@id, 'legacy', @created, @created, '{}');
                    INSERT INTO runs (id, session_id, prompt, origin, created_at) VALUES (@run, @id, 'prompt', 0, @created);
                    INSERT INTO generations (id, run_id, session_id, provider_id, model_id, prompt, seed, seed_applied, status, cost_usd, created_at, completed_at, sort_order, outcomes_json, reference_attachment_ids_json)
                    VALUES (@generation, @run, @id, 'openrouter', 'model', 'prompt', 1, 1, 2, '0.5', @created, @created, 0, '[]', '[]');
                    """;
                seed.Parameters.AddWithValue("@id", session.Id.ToString("D"));
                seed.Parameters.AddWithValue("@run", Guid.NewGuid().ToString("D"));
                seed.Parameters.AddWithValue("@generation", Guid.NewGuid().ToString("D"));
                seed.Parameters.AddWithValue("@created", TestData.BaseTime.ToString("O"));
                await seed.ExecuteNonQueryAsync(ct);
            }

            var reopened = new SqliteDatabase(path);
            await reopened.InitializeAsync(ct);
            await reopened.InitializeAsync(ct);
            var expenses = new ExpenseRepository(reopened);
            Assert.Equal(0.5m, await expenses.SumAsync(ct));
            Assert.Equal(1, await expenses.CountAsync(ct));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }
}
