---
id: 004
status: planned
assignee:
claimed:
created: 2026-10-27
---

# Хранилище: репозитории сессий, сообщений, прогонов и моделей

## Описание
Реализовать интерфейсы `ISessionRepository`, `IChatMessageRepository`, `IRunRepository`, `IImageModelRepository` (задача 002) на SQLite поверх `SqliteDatabase` (задача 003). Все запросы параметризованы, колонки читаются по имени.

Зависит от: 002, 003.
Среда: Linux или Windows (нужен только .NET 10 SDK, MAUI-нагрузки не нужны).

## Критерии готовности
- Файлы из плана созданы с содержимым как в плане.
- Все тесты `ImageGen.Data.Tests` проходят (тесты из плана и тесты задачи 003).
- `dotnet format` для `ImageGen.Data` и `ImageGen.Data.Tests` проходит.

## Анализ
- Правила преобразования значений (Guid, дата, decimal, bool, enum) — в анализе задачи 003; здесь они собраны в `SqliteValues`.
- `DeleteAsync` сессии удаляет сообщения, прогоны и генерации каскадом (`ON DELETE CASCADE`, внешние ключи включены в строке подключения).
- `MarkUnfinishedAsInterruptedAsync` пишет английский текст ошибки: это упрощение скелета (ядро не должно формировать тексты интерфейса; вынести в `.resx` позже).
- Код проверен сборкой и тестами 2026-10-27 в пробной копии решения.

## План
1. Создать файлы (содержимое дословно):

   `ImageGen/ImageGen.Data/SqliteValues.cs`
   ```csharp
   using System.Globalization;
   using Microsoft.Data.Sqlite;

   namespace ImageGen.Data;

   /// <summary>Conversions between .NET values and SQLite column values.</summary>
   internal static class SqliteValues
   {
       public static string FromGuid(Guid value) => value.ToString("D");

       public static object FromNullableGuid(Guid? value) => value.HasValue ? FromGuid(value.Value) : DBNull.Value;

       public static string FromDate(DateTimeOffset value) => value.ToString("O", CultureInfo.InvariantCulture);

       public static object FromNullableDate(DateTimeOffset? value) => value.HasValue ? FromDate(value.Value) : DBNull.Value;

       public static object FromNullableDecimal(decimal? value) =>
           value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : DBNull.Value;

       public static object FromNullableString(string? value) => value is null ? DBNull.Value : value;

       public static long FromBool(bool value) => value ? 1 : 0;

       public static Guid GetGuid(SqliteDataReader reader, string column) =>
           Guid.Parse(reader.GetString(reader.GetOrdinal(column)));

       public static Guid? GetNullableGuid(SqliteDataReader reader, string column)
       {
           var ordinal = reader.GetOrdinal(column);
           return reader.IsDBNull(ordinal) ? null : Guid.Parse(reader.GetString(ordinal));
       }

       public static DateTimeOffset GetDate(SqliteDataReader reader, string column) =>
           DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal(column)), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

       public static DateTimeOffset? GetNullableDate(SqliteDataReader reader, string column)
       {
           var ordinal = reader.GetOrdinal(column);
           return reader.IsDBNull(ordinal)
               ? null
               : DateTimeOffset.Parse(reader.GetString(ordinal), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
       }

       public static decimal? GetNullableDecimal(SqliteDataReader reader, string column)
       {
           var ordinal = reader.GetOrdinal(column);
           return reader.IsDBNull(ordinal) ? null : decimal.Parse(reader.GetString(ordinal), CultureInfo.InvariantCulture);
       }

       public static string GetString(SqliteDataReader reader, string column) => reader.GetString(reader.GetOrdinal(column));

       public static string? GetNullableString(SqliteDataReader reader, string column)
       {
           var ordinal = reader.GetOrdinal(column);
           return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
       }

       public static long GetInt64(SqliteDataReader reader, string column) => reader.GetInt64(reader.GetOrdinal(column));

       public static int GetInt32(SqliteDataReader reader, string column) => reader.GetInt32(reader.GetOrdinal(column));

       public static int? GetNullableInt32(SqliteDataReader reader, string column)
       {
           var ordinal = reader.GetOrdinal(column);
           return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
       }

       public static bool GetBool(SqliteDataReader reader, string column) => reader.GetInt64(reader.GetOrdinal(column)) != 0;
   }
   ```

   `ImageGen/ImageGen.Data/Repositories/SessionRepository.cs`
   ```csharp
   using System.Text.Json;
   using ImageGen.Core.Domain;
   using ImageGen.Core.Serialization;
   using ImageGen.Core.Storage;
   using Microsoft.Data.Sqlite;

   namespace ImageGen.Data.Repositories;

   public sealed class SessionRepository(SqliteDatabase database) : ISessionRepository
   {
       private const string SelectColumns = "SELECT id, title, created_at, updated_at, settings_json FROM sessions";

       public async Task AddAsync(Session session, CancellationToken cancellationToken)
       {
           ArgumentNullException.ThrowIfNull(session);
           await using var connection = await database.OpenAsync(cancellationToken);
           await using var command = connection.CreateCommand();
           command.CommandText = """
               INSERT INTO sessions (id, title, created_at, updated_at, settings_json)
               VALUES (@id, @title, @created_at, @updated_at, @settings_json);
               """;
           AddParameters(command, session);
           await command.ExecuteNonQueryAsync(cancellationToken);
       }

       public async Task UpdateAsync(Session session, CancellationToken cancellationToken)
       {
           ArgumentNullException.ThrowIfNull(session);
           await using var connection = await database.OpenAsync(cancellationToken);
           await using var command = connection.CreateCommand();
           command.CommandText = """
               UPDATE sessions
               SET title = @title, created_at = @created_at, updated_at = @updated_at, settings_json = @settings_json
               WHERE id = @id;
               """;
           AddParameters(command, session);
           await command.ExecuteNonQueryAsync(cancellationToken);
       }

       public async Task<Session?> GetAsync(Guid id, CancellationToken cancellationToken)
       {
           await using var connection = await database.OpenAsync(cancellationToken);
           await using var command = connection.CreateCommand();
           command.CommandText = SelectColumns + " WHERE id = @id;";
           command.Parameters.AddWithValue("@id", SqliteValues.FromGuid(id));
           await using var reader = await command.ExecuteReaderAsync(cancellationToken);
           return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
       }

       public async Task<IReadOnlyList<Session>> ListAsync(CancellationToken cancellationToken)
       {
           await using var connection = await database.OpenAsync(cancellationToken);
           await using var command = connection.CreateCommand();
           command.CommandText = SelectColumns + " ORDER BY updated_at DESC;";
           await using var reader = await command.ExecuteReaderAsync(cancellationToken);
           var result = new List<Session>();
           while (await reader.ReadAsync(cancellationToken))
           {
               result.Add(Read(reader));
           }

           return result;
       }

       public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
       {
           await using var connection = await database.OpenAsync(cancellationToken);
           await using var command = connection.CreateCommand();
           command.CommandText = "DELETE FROM sessions WHERE id = @id;";
           command.Parameters.AddWithValue("@id", SqliteValues.FromGuid(id));
           await command.ExecuteNonQueryAsync(cancellationToken);
       }

       private static void AddParameters(SqliteCommand command, Session session)
       {
           command.Parameters.AddWithValue("@id", SqliteValues.FromGuid(session.Id));
           command.Parameters.AddWithValue("@title", session.Title);
           command.Parameters.AddWithValue("@created_at", SqliteValues.FromDate(session.CreatedAt));
           command.Parameters.AddWithValue("@updated_at", SqliteValues.FromDate(session.UpdatedAt));
           command.Parameters.AddWithValue(
               "@settings_json",
               JsonSerializer.Serialize(session.Settings, CoreJsonContext.Default.SessionSettings));
       }

       private static Session Read(SqliteDataReader reader) => new()
       {
           Id = SqliteValues.GetGuid(reader, "id"),
           Title = SqliteValues.GetString(reader, "title"),
           CreatedAt = SqliteValues.GetDate(reader, "created_at"),
           UpdatedAt = SqliteValues.GetDate(reader, "updated_at"),
           Settings = JsonSerializer.Deserialize(
                   SqliteValues.GetString(reader, "settings_json"),
                   CoreJsonContext.Default.SessionSettings)
               ?? SessionSettings.CreateDefault(),
       };
   }
   ```

   `ImageGen/ImageGen.Data/Repositories/ChatMessageRepository.cs`
   ```csharp
   using ImageGen.Core.Domain;
   using ImageGen.Core.Storage;
   using Microsoft.Data.Sqlite;

   namespace ImageGen.Data.Repositories;

   public sealed class ChatMessageRepository(SqliteDatabase database) : IChatMessageRepository
   {
       public async Task AddAsync(ChatMessage message, CancellationToken cancellationToken)
       {
           ArgumentNullException.ThrowIfNull(message);
           await using var connection = await database.OpenAsync(cancellationToken);
           await using var command = connection.CreateCommand();
           command.CommandText = """
               INSERT INTO chat_messages (id, session_id, role, text, created_at, run_id, is_error)
               VALUES (@id, @session_id, @role, @text, @created_at, @run_id, @is_error);
               """;
           command.Parameters.AddWithValue("@id", SqliteValues.FromGuid(message.Id));
           command.Parameters.AddWithValue("@session_id", SqliteValues.FromGuid(message.SessionId));
           command.Parameters.AddWithValue("@role", (int)message.Role);
           command.Parameters.AddWithValue("@text", message.Text);
           command.Parameters.AddWithValue("@created_at", SqliteValues.FromDate(message.CreatedAt));
           command.Parameters.AddWithValue("@run_id", SqliteValues.FromNullableGuid(message.RunId));
           command.Parameters.AddWithValue("@is_error", SqliteValues.FromBool(message.IsError));
           await command.ExecuteNonQueryAsync(cancellationToken);
       }

       public async Task<IReadOnlyList<ChatMessage>> ListBySessionAsync(Guid sessionId, CancellationToken cancellationToken)
       {
           await using var connection = await database.OpenAsync(cancellationToken);
           await using var command = connection.CreateCommand();
           command.CommandText = """
               SELECT id, session_id, role, text, created_at, run_id, is_error
               FROM chat_messages
               WHERE session_id = @session_id
               ORDER BY created_at ASC;
               """;
           command.Parameters.AddWithValue("@session_id", SqliteValues.FromGuid(sessionId));
           await using var reader = await command.ExecuteReaderAsync(cancellationToken);
           var result = new List<ChatMessage>();
           while (await reader.ReadAsync(cancellationToken))
           {
               result.Add(Read(reader));
           }

           return result;
       }

       private static ChatMessage Read(SqliteDataReader reader) => new()
       {
           Id = SqliteValues.GetGuid(reader, "id"),
           SessionId = SqliteValues.GetGuid(reader, "session_id"),
           Role = (ChatRole)SqliteValues.GetInt32(reader, "role"),
           Text = SqliteValues.GetString(reader, "text"),
           CreatedAt = SqliteValues.GetDate(reader, "created_at"),
           RunId = SqliteValues.GetNullableGuid(reader, "run_id"),
           IsError = SqliteValues.GetBool(reader, "is_error"),
       };
   }
   ```

   `ImageGen/ImageGen.Data/Repositories/RunRepository.cs`
   ```csharp
   using ImageGen.Core.Domain;
   using ImageGen.Core.Storage;
   using Microsoft.Data.Sqlite;

   namespace ImageGen.Data.Repositories;

   public sealed class RunRepository(SqliteDatabase database) : IRunRepository
   {
       private const string GenerationColumns = """
           g.id, g.run_id, g.session_id, g.model_id, g.prompt, g.seed, g.seed_applied, g.status,
           g.image_path, g.media_type, g.cost_usd, g.error_kind, g.error_message, g.created_at, g.completed_at, g.sort_order
           """;

       public async Task AddRunAsync(Run run, IReadOnlyList<Generation> generations, CancellationToken cancellationToken)
       {
           ArgumentNullException.ThrowIfNull(run);
           ArgumentNullException.ThrowIfNull(generations);
           await using var connection = await database.OpenAsync(cancellationToken);
           await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

           await using (var command = connection.CreateCommand())
           {
               command.Transaction = transaction;
               command.CommandText = """
                   INSERT INTO runs (id, session_id, prompt, origin, created_at)
                   VALUES (@id, @session_id, @prompt, @origin, @created_at);
                   """;
               command.Parameters.AddWithValue("@id", SqliteValues.FromGuid(run.Id));
               command.Parameters.AddWithValue("@session_id", SqliteValues.FromGuid(run.SessionId));
               command.Parameters.AddWithValue("@prompt", run.Prompt);
               command.Parameters.AddWithValue("@origin", (int)run.Origin);
               command.Parameters.AddWithValue("@created_at", SqliteValues.FromDate(run.CreatedAt));
               await command.ExecuteNonQueryAsync(cancellationToken);
           }

           foreach (var generation in generations)
           {
               await using var command = connection.CreateCommand();
               command.Transaction = transaction;
               command.CommandText = """
                   INSERT INTO generations (id, run_id, session_id, model_id, prompt, seed, seed_applied, status,
                       image_path, media_type, cost_usd, error_kind, error_message, created_at, completed_at, sort_order)
                   VALUES (@id, @run_id, @session_id, @model_id, @prompt, @seed, @seed_applied, @status,
                       @image_path, @media_type, @cost_usd, @error_kind, @error_message, @created_at, @completed_at, @sort_order);
                   """;
               AddGenerationParameters(command, generation);
               await command.ExecuteNonQueryAsync(cancellationToken);
           }

           await transaction.CommitAsync(cancellationToken);
       }

       public async Task<Run?> GetRunAsync(Guid id, CancellationToken cancellationToken)
       {
           await using var connection = await database.OpenAsync(cancellationToken);
           await using var command = connection.CreateCommand();
           command.CommandText = "SELECT id, session_id, prompt, origin, created_at FROM runs WHERE id = @id;";
           command.Parameters.AddWithValue("@id", SqliteValues.FromGuid(id));
           await using var reader = await command.ExecuteReaderAsync(cancellationToken);
           return await reader.ReadAsync(cancellationToken) ? ReadRun(reader) : null;
       }

       public async Task<IReadOnlyList<Run>> ListRunsBySessionAsync(Guid sessionId, CancellationToken cancellationToken)
       {
           await using var connection = await database.OpenAsync(cancellationToken);
           await using var command = connection.CreateCommand();
           command.CommandText = """
               SELECT id, session_id, prompt, origin, created_at
               FROM runs
               WHERE session_id = @session_id
               ORDER BY created_at ASC;
               """;
           command.Parameters.AddWithValue("@session_id", SqliteValues.FromGuid(sessionId));
           await using var reader = await command.ExecuteReaderAsync(cancellationToken);
           var result = new List<Run>();
           while (await reader.ReadAsync(cancellationToken))
           {
               result.Add(ReadRun(reader));
           }

           return result;
       }

       public async Task<Generation?> GetGenerationAsync(Guid id, CancellationToken cancellationToken)
       {
           await using var connection = await database.OpenAsync(cancellationToken);
           await using var command = connection.CreateCommand();
           command.CommandText = $"SELECT {GenerationColumns} FROM generations g WHERE g.id = @id;";
           command.Parameters.AddWithValue("@id", SqliteValues.FromGuid(id));
           await using var reader = await command.ExecuteReaderAsync(cancellationToken);
           return await reader.ReadAsync(cancellationToken) ? ReadGeneration(reader) : null;
       }

       public async Task<IReadOnlyList<Generation>> ListGenerationsBySessionAsync(Guid sessionId, CancellationToken cancellationToken)
       {
           await using var connection = await database.OpenAsync(cancellationToken);
           await using var command = connection.CreateCommand();
           command.CommandText = $"""
               SELECT {GenerationColumns}
               FROM generations g
               JOIN runs r ON r.id = g.run_id
               WHERE g.session_id = @session_id
               ORDER BY r.created_at ASC, g.sort_order ASC;
               """;
           command.Parameters.AddWithValue("@session_id", SqliteValues.FromGuid(sessionId));
           await using var reader = await command.ExecuteReaderAsync(cancellationToken);
           var result = new List<Generation>();
           while (await reader.ReadAsync(cancellationToken))
           {
               result.Add(ReadGeneration(reader));
           }

           return result;
       }

       public async Task UpdateGenerationAsync(Generation generation, CancellationToken cancellationToken)
       {
           ArgumentNullException.ThrowIfNull(generation);
           await using var connection = await database.OpenAsync(cancellationToken);
           await using var command = connection.CreateCommand();
           command.CommandText = """
               UPDATE generations
               SET run_id = @run_id, session_id = @session_id, model_id = @model_id, prompt = @prompt, seed = @seed,
                   seed_applied = @seed_applied, status = @status, image_path = @image_path, media_type = @media_type,
                   cost_usd = @cost_usd, error_kind = @error_kind, error_message = @error_message,
                   created_at = @created_at, completed_at = @completed_at, sort_order = @sort_order
               WHERE id = @id;
               """;
           AddGenerationParameters(command, generation);
           await command.ExecuteNonQueryAsync(cancellationToken);
       }

       public async Task<int> MarkUnfinishedAsInterruptedAsync(DateTimeOffset completedAt, CancellationToken cancellationToken)
       {
           await using var connection = await database.OpenAsync(cancellationToken);
           await using var command = connection.CreateCommand();
           command.CommandText = """
               UPDATE generations
               SET status = @failed, error_kind = @interrupted, error_message = @message, completed_at = @completed_at
               WHERE status IN (@queued, @running);
               """;
           command.Parameters.AddWithValue("@failed", (int)GenerationStatus.Failed);
           command.Parameters.AddWithValue("@interrupted", (int)ErrorKind.Interrupted);
           command.Parameters.AddWithValue("@message", "The app was closed before the generation finished.");
           command.Parameters.AddWithValue("@completed_at", SqliteValues.FromDate(completedAt));
           command.Parameters.AddWithValue("@queued", (int)GenerationStatus.Queued);
           command.Parameters.AddWithValue("@running", (int)GenerationStatus.Running);
           return await command.ExecuteNonQueryAsync(cancellationToken);
       }

       private static void AddGenerationParameters(SqliteCommand command, Generation generation)
       {
           command.Parameters.AddWithValue("@id", SqliteValues.FromGuid(generation.Id));
           command.Parameters.AddWithValue("@run_id", SqliteValues.FromGuid(generation.RunId));
           command.Parameters.AddWithValue("@session_id", SqliteValues.FromGuid(generation.SessionId));
           command.Parameters.AddWithValue("@model_id", generation.ModelId);
           command.Parameters.AddWithValue("@prompt", generation.Prompt);
           command.Parameters.AddWithValue("@seed", generation.Seed);
           command.Parameters.AddWithValue("@seed_applied", SqliteValues.FromBool(generation.SeedApplied));
           command.Parameters.AddWithValue("@status", (int)generation.Status);
           command.Parameters.AddWithValue("@image_path", SqliteValues.FromNullableString(generation.ImagePath));
           command.Parameters.AddWithValue("@media_type", SqliteValues.FromNullableString(generation.MediaType));
           command.Parameters.AddWithValue("@cost_usd", SqliteValues.FromNullableDecimal(generation.CostUsd));
           command.Parameters.AddWithValue(
               "@error_kind",
               generation.ErrorKind.HasValue ? (int)generation.ErrorKind.Value : DBNull.Value);
           command.Parameters.AddWithValue("@error_message", SqliteValues.FromNullableString(generation.ErrorMessage));
           command.Parameters.AddWithValue("@created_at", SqliteValues.FromDate(generation.CreatedAt));
           command.Parameters.AddWithValue("@completed_at", SqliteValues.FromNullableDate(generation.CompletedAt));
           command.Parameters.AddWithValue("@sort_order", generation.SortOrder);
       }

       private static Run ReadRun(SqliteDataReader reader) => new()
       {
           Id = SqliteValues.GetGuid(reader, "id"),
           SessionId = SqliteValues.GetGuid(reader, "session_id"),
           Prompt = SqliteValues.GetString(reader, "prompt"),
           Origin = (RunOrigin)SqliteValues.GetInt32(reader, "origin"),
           CreatedAt = SqliteValues.GetDate(reader, "created_at"),
       };

       private static Generation ReadGeneration(SqliteDataReader reader)
       {
           var errorKind = SqliteValues.GetNullableInt32(reader, "error_kind");
           return new Generation
           {
               Id = SqliteValues.GetGuid(reader, "id"),
               RunId = SqliteValues.GetGuid(reader, "run_id"),
               SessionId = SqliteValues.GetGuid(reader, "session_id"),
               ModelId = SqliteValues.GetString(reader, "model_id"),
               Prompt = SqliteValues.GetString(reader, "prompt"),
               Seed = SqliteValues.GetInt64(reader, "seed"),
               SeedApplied = SqliteValues.GetBool(reader, "seed_applied"),
               Status = (GenerationStatus)SqliteValues.GetInt32(reader, "status"),
               ImagePath = SqliteValues.GetNullableString(reader, "image_path"),
               MediaType = SqliteValues.GetNullableString(reader, "media_type"),
               CostUsd = SqliteValues.GetNullableDecimal(reader, "cost_usd"),
               ErrorKind = errorKind.HasValue ? (ErrorKind)errorKind.Value : null,
               ErrorMessage = SqliteValues.GetNullableString(reader, "error_message"),
               CreatedAt = SqliteValues.GetDate(reader, "created_at"),
               CompletedAt = SqliteValues.GetNullableDate(reader, "completed_at"),
               SortOrder = SqliteValues.GetInt32(reader, "sort_order"),
           };
       }
   }
   ```

   `ImageGen/ImageGen.Data/Repositories/ImageModelRepository.cs`
   ```csharp
   using ImageGen.Core.Domain;
   using ImageGen.Core.Storage;
   using Microsoft.Data.Sqlite;

   namespace ImageGen.Data.Repositories;

   public sealed class ImageModelRepository(SqliteDatabase database) : IImageModelRepository
   {
       public async Task ReplaceAllAsync(IReadOnlyList<ImageModel> models, CancellationToken cancellationToken)
       {
           ArgumentNullException.ThrowIfNull(models);
           await using var connection = await database.OpenAsync(cancellationToken);
           await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

           await using (var delete = connection.CreateCommand())
           {
               delete.Transaction = transaction;
               delete.CommandText = "DELETE FROM image_models;";
               await delete.ExecuteNonQueryAsync(cancellationToken);
           }

           foreach (var model in models)
           {
               await using var insert = connection.CreateCommand();
               insert.Transaction = transaction;
               insert.CommandText = """
                   INSERT INTO image_models (id, name, supports_seed, supports_image_input, max_images_per_request, fetched_at)
                   VALUES (@id, @name, @supports_seed, @supports_image_input, @max_images_per_request, @fetched_at);
                   """;
               insert.Parameters.AddWithValue("@id", model.Id);
               insert.Parameters.AddWithValue("@name", model.Name);
               insert.Parameters.AddWithValue("@supports_seed", SqliteValues.FromBool(model.SupportsSeed));
               insert.Parameters.AddWithValue("@supports_image_input", SqliteValues.FromBool(model.SupportsImageInput));
               insert.Parameters.AddWithValue("@max_images_per_request", model.MaxImagesPerRequest);
               insert.Parameters.AddWithValue("@fetched_at", SqliteValues.FromDate(model.FetchedAt));
               await insert.ExecuteNonQueryAsync(cancellationToken);
           }

           await transaction.CommitAsync(cancellationToken);
       }

       public async Task<IReadOnlyList<ImageModel>> ListAsync(CancellationToken cancellationToken)
       {
           await using var connection = await database.OpenAsync(cancellationToken);
           await using var command = connection.CreateCommand();
           command.CommandText = """
               SELECT id, name, supports_seed, supports_image_input, max_images_per_request, fetched_at
               FROM image_models
               ORDER BY name ASC;
               """;
           await using var reader = await command.ExecuteReaderAsync(cancellationToken);
           var result = new List<ImageModel>();
           while (await reader.ReadAsync(cancellationToken))
           {
               result.Add(new ImageModel
               {
                   Id = SqliteValues.GetString(reader, "id"),
                   Name = SqliteValues.GetString(reader, "name"),
                   SupportsSeed = SqliteValues.GetBool(reader, "supports_seed"),
                   SupportsImageInput = SqliteValues.GetBool(reader, "supports_image_input"),
                   MaxImagesPerRequest = SqliteValues.GetInt32(reader, "max_images_per_request"),
                   FetchedAt = SqliteValues.GetDate(reader, "fetched_at"),
               });
           }

           return result;
       }
   }
   ```

2. Создать тесты (содержимое дословно):

   `ImageGen/ImageGen.Data.Tests/TestData.cs`
   ```csharp
   using ImageGen.Core.Domain;

   namespace ImageGen.Data.Tests;

   /// <summary>Factory methods for test entities.</summary>
   public static class TestData
   {
       public static readonly DateTimeOffset BaseTime = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

       public static Session Session(DateTimeOffset? updatedAt = null) => new()
       {
           Id = Guid.NewGuid(),
           Title = "Test session",
           CreatedAt = BaseTime,
           UpdatedAt = updatedAt ?? BaseTime,
           Settings = SessionSettings.CreateDefault(),
       };

       public static ChatMessage Message(Guid sessionId, DateTimeOffset createdAt, string text = "hello") => new()
       {
           Id = Guid.NewGuid(),
           SessionId = sessionId,
           Role = ChatRole.User,
           Text = text,
           CreatedAt = createdAt,
       };

       public static Run Run(Guid sessionId, DateTimeOffset createdAt) => new()
       {
           Id = Guid.NewGuid(),
           SessionId = sessionId,
           Prompt = "a red fox",
           Origin = RunOrigin.Assistant,
           CreatedAt = createdAt,
       };

       public static Generation Generation(Run run, int sortOrder, GenerationStatus status = GenerationStatus.Queued) => new()
       {
           Id = Guid.NewGuid(),
           RunId = run.Id,
           SessionId = run.SessionId,
           ModelId = "black-forest-labs/flux.2-klein-4b",
           Prompt = run.Prompt,
           Seed = 42 + sortOrder,
           SeedApplied = true,
           Status = status,
           CreatedAt = run.CreatedAt,
           SortOrder = sortOrder,
       };
   }
   ```

   `ImageGen/ImageGen.Data.Tests/Repositories/SessionRepositoryTests.cs`
   ```csharp
   using ImageGen.Data.Repositories;

   namespace ImageGen.Data.Tests.Repositories;

   public class SessionRepositoryTests
   {
       [Fact]
       public async Task AddAsync_ThenGetAsync_ReturnsSameValues()
       {
           var ct = TestContext.Current.CancellationToken;
           await using var test = await TestDatabase.CreateAsync(ct);
           var repository = new SessionRepository(test.Database);
           var session = TestData.Session();
           session.Settings.ImageModelIds = ["a/b", "c/d"];
           session.Settings.RunsPerModel = 3;

           await repository.AddAsync(session, ct);
           var loaded = await repository.GetAsync(session.Id, ct);

           Assert.NotNull(loaded);
           Assert.Equal(session.Title, loaded.Title);
           Assert.Equal(session.CreatedAt, loaded.CreatedAt);
           Assert.Equal(session.UpdatedAt, loaded.UpdatedAt);
           Assert.Equal(["a/b", "c/d"], loaded.Settings.ImageModelIds);
           Assert.Equal(3, loaded.Settings.RunsPerModel);
       }

       [Fact]
       public async Task GetAsync_UnknownId_ReturnsNull()
       {
           var ct = TestContext.Current.CancellationToken;
           await using var test = await TestDatabase.CreateAsync(ct);
           var repository = new SessionRepository(test.Database);

           Assert.Null(await repository.GetAsync(Guid.NewGuid(), ct));
       }

       [Fact]
       public async Task UpdateAsync_ChangedTitle_IsSaved()
       {
           var ct = TestContext.Current.CancellationToken;
           await using var test = await TestDatabase.CreateAsync(ct);
           var repository = new SessionRepository(test.Database);
           var session = TestData.Session();
           await repository.AddAsync(session, ct);

           session.Title = "Renamed";
           await repository.UpdateAsync(session, ct);

           Assert.Equal("Renamed", (await repository.GetAsync(session.Id, ct))!.Title);
       }

       [Fact]
       public async Task ListAsync_ReturnsNewestUpdatedFirst()
       {
           var ct = TestContext.Current.CancellationToken;
           await using var test = await TestDatabase.CreateAsync(ct);
           var repository = new SessionRepository(test.Database);
           var older = TestData.Session(TestData.BaseTime);
           var newer = TestData.Session(TestData.BaseTime.AddMinutes(5));
           await repository.AddAsync(older, ct);
           await repository.AddAsync(newer, ct);

           var list = await repository.ListAsync(ct);

           Assert.Equal([newer.Id, older.Id], list.Select(s => s.Id));
       }

       [Fact]
       public async Task DeleteAsync_SessionWithChildren_DeletesChildren()
       {
           var ct = TestContext.Current.CancellationToken;
           await using var test = await TestDatabase.CreateAsync(ct);
           var sessions = new SessionRepository(test.Database);
           var messages = new ChatMessageRepository(test.Database);
           var runs = new RunRepository(test.Database);
           var session = TestData.Session();
           await sessions.AddAsync(session, ct);
           await messages.AddAsync(TestData.Message(session.Id, TestData.BaseTime), ct);
           var run = TestData.Run(session.Id, TestData.BaseTime);
           var generation = TestData.Generation(run, 0);
           await runs.AddRunAsync(run, [generation], ct);

           await sessions.DeleteAsync(session.Id, ct);

           Assert.Null(await sessions.GetAsync(session.Id, ct));
           Assert.Empty(await messages.ListBySessionAsync(session.Id, ct));
           Assert.Null(await runs.GetRunAsync(run.Id, ct));
           Assert.Null(await runs.GetGenerationAsync(generation.Id, ct));
       }
   }
   ```

   `ImageGen/ImageGen.Data.Tests/Repositories/ChatMessageRepositoryTests.cs`
   ```csharp
   using ImageGen.Core.Domain;
   using ImageGen.Data.Repositories;

   namespace ImageGen.Data.Tests.Repositories;

   public class ChatMessageRepositoryTests
   {
       [Fact]
       public async Task AddAsync_ThenList_ReturnsMessagesInCreationOrder()
       {
           var ct = TestContext.Current.CancellationToken;
           await using var test = await TestDatabase.CreateAsync(ct);
           var session = TestData.Session();
           await new SessionRepository(test.Database).AddAsync(session, ct);
           var repository = new ChatMessageRepository(test.Database);
           var second = TestData.Message(session.Id, TestData.BaseTime.AddSeconds(2), "second");
           var first = TestData.Message(session.Id, TestData.BaseTime.AddSeconds(1), "first");
           var runId = Guid.NewGuid();
           second.Role = ChatRole.Assistant;
           second.RunId = runId;
           second.IsError = true;

           await repository.AddAsync(second, ct);
           await repository.AddAsync(first, ct);
           var list = await repository.ListBySessionAsync(session.Id, ct);

           Assert.Equal(["first", "second"], list.Select(m => m.Text));
           Assert.Equal(ChatRole.Assistant, list[1].Role);
           Assert.Equal(runId, list[1].RunId);
           Assert.True(list[1].IsError);
           Assert.Null(list[0].RunId);
           Assert.False(list[0].IsError);
       }

       [Fact]
       public async Task ListBySessionAsync_OtherSession_IsNotReturned()
       {
           var ct = TestContext.Current.CancellationToken;
           await using var test = await TestDatabase.CreateAsync(ct);
           var sessions = new SessionRepository(test.Database);
           var a = TestData.Session();
           var b = TestData.Session();
           await sessions.AddAsync(a, ct);
           await sessions.AddAsync(b, ct);
           var repository = new ChatMessageRepository(test.Database);
           await repository.AddAsync(TestData.Message(a.Id, TestData.BaseTime), ct);

           Assert.Empty(await repository.ListBySessionAsync(b.Id, ct));
       }
   }
   ```

   `ImageGen/ImageGen.Data.Tests/Repositories/RunRepositoryTests.cs`
   ```csharp
   using ImageGen.Core.Domain;
   using ImageGen.Data.Repositories;

   namespace ImageGen.Data.Tests.Repositories;

   public class RunRepositoryTests
   {
       [Fact]
       public async Task AddRunAsync_ThenGet_ReturnsRunAndGenerations()
       {
           var ct = TestContext.Current.CancellationToken;
           await using var test = await TestDatabase.CreateAsync(ct);
           var session = TestData.Session();
           await new SessionRepository(test.Database).AddAsync(session, ct);
           var repository = new RunRepository(test.Database);
           var run = TestData.Run(session.Id, TestData.BaseTime);
           var g0 = TestData.Generation(run, 0);
           var g1 = TestData.Generation(run, 1);

           await repository.AddRunAsync(run, [g0, g1], ct);

           var loadedRun = await repository.GetRunAsync(run.Id, ct);
           Assert.NotNull(loadedRun);
           Assert.Equal(run.Prompt, loadedRun.Prompt);
           Assert.Equal(RunOrigin.Assistant, loadedRun.Origin);
           var loaded = await repository.GetGenerationAsync(g1.Id, ct);
           Assert.NotNull(loaded);
           Assert.Equal(g1.Seed, loaded.Seed);
           Assert.True(loaded.SeedApplied);
           Assert.Equal(GenerationStatus.Queued, loaded.Status);
           Assert.Null(loaded.ImagePath);
           Assert.Null(loaded.CostUsd);
           Assert.Null(loaded.ErrorKind);
           Assert.Equal(1, loaded.SortOrder);
       }

       [Fact]
       public async Task UpdateGenerationAsync_AllFields_AreSaved()
       {
           var ct = TestContext.Current.CancellationToken;
           await using var test = await TestDatabase.CreateAsync(ct);
           var session = TestData.Session();
           await new SessionRepository(test.Database).AddAsync(session, ct);
           var repository = new RunRepository(test.Database);
           var run = TestData.Run(session.Id, TestData.BaseTime);
           var generation = TestData.Generation(run, 0);
           await repository.AddRunAsync(run, [generation], ct);

           generation.Status = GenerationStatus.Failed;
           generation.ImagePath = "images/x/y.png";
           generation.MediaType = "image/png";
           generation.CostUsd = 0.0123m;
           generation.ErrorKind = ErrorKind.RateLimited;
           generation.ErrorMessage = "slow down";
           generation.CompletedAt = TestData.BaseTime.AddSeconds(7);
           await repository.UpdateGenerationAsync(generation, ct);

           var loaded = await repository.GetGenerationAsync(generation.Id, ct);
           Assert.NotNull(loaded);
           Assert.Equal(GenerationStatus.Failed, loaded.Status);
           Assert.Equal("images/x/y.png", loaded.ImagePath);
           Assert.Equal("image/png", loaded.MediaType);
           Assert.Equal(0.0123m, loaded.CostUsd);
           Assert.Equal(ErrorKind.RateLimited, loaded.ErrorKind);
           Assert.Equal("slow down", loaded.ErrorMessage);
           Assert.Equal(TestData.BaseTime.AddSeconds(7), loaded.CompletedAt);
       }

       [Fact]
       public async Task ListGenerationsBySessionAsync_OrdersByRunTimeThenSortOrder()
       {
           var ct = TestContext.Current.CancellationToken;
           await using var test = await TestDatabase.CreateAsync(ct);
           var session = TestData.Session();
           await new SessionRepository(test.Database).AddAsync(session, ct);
           var repository = new RunRepository(test.Database);
           var later = TestData.Run(session.Id, TestData.BaseTime.AddMinutes(1));
           var earlier = TestData.Run(session.Id, TestData.BaseTime);
           var l0 = TestData.Generation(later, 0);
           var e1 = TestData.Generation(earlier, 1);
           var e0 = TestData.Generation(earlier, 0);
           await repository.AddRunAsync(later, [l0], ct);
           await repository.AddRunAsync(earlier, [e1, e0], ct);

           var list = await repository.ListGenerationsBySessionAsync(session.Id, ct);
           var runs = await repository.ListRunsBySessionAsync(session.Id, ct);

           Assert.Equal([e0.Id, e1.Id, l0.Id], list.Select(g => g.Id));
           Assert.Equal([earlier.Id, later.Id], runs.Select(r => r.Id));
       }

       [Fact]
       public async Task MarkUnfinishedAsInterruptedAsync_OnlyQueuedAndRunning_AreMarked()
       {
           var ct = TestContext.Current.CancellationToken;
           await using var test = await TestDatabase.CreateAsync(ct);
           var session = TestData.Session();
           await new SessionRepository(test.Database).AddAsync(session, ct);
           var repository = new RunRepository(test.Database);
           var run = TestData.Run(session.Id, TestData.BaseTime);
           var queued = TestData.Generation(run, 0, GenerationStatus.Queued);
           var running = TestData.Generation(run, 1, GenerationStatus.Running);
           var succeeded = TestData.Generation(run, 2, GenerationStatus.Succeeded);
           await repository.AddRunAsync(run, [queued, running, succeeded], ct);
           var now = TestData.BaseTime.AddHours(1);

           var count = await repository.MarkUnfinishedAsInterruptedAsync(now, ct);

           Assert.Equal(2, count);
           foreach (var id in new[] { queued.Id, running.Id })
           {
               var loaded = (await repository.GetGenerationAsync(id, ct))!;
               Assert.Equal(GenerationStatus.Failed, loaded.Status);
               Assert.Equal(ErrorKind.Interrupted, loaded.ErrorKind);
               Assert.Equal(now, loaded.CompletedAt);
           }

           Assert.Equal(GenerationStatus.Succeeded, (await repository.GetGenerationAsync(succeeded.Id, ct))!.Status);
       }
   }
   ```

   `ImageGen/ImageGen.Data.Tests/Repositories/ImageModelRepositoryTests.cs`
   ```csharp
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
   ```

Файлы: `ImageGen/ImageGen.Data/SqliteValues.cs`, `ImageGen/ImageGen.Data/Repositories/*.cs`, `ImageGen/ImageGen.Data.Tests/TestData.cs`, `ImageGen/ImageGen.Data.Tests/Repositories/*.cs`

### Проверка
```
dotnet format ImageGen/ImageGen.Data/ImageGen.Data.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj --verify-no-changes
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
scripts/check.sh
```

## Отчёт

## Комментарии
