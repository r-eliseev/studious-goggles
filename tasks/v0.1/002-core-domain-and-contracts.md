---
id: 002
status: done
assignee: claude-qnrqyt
claimed: 2026-10-27T00:00:00Z
created: 2026-10-27
---

# Ядро: доменная модель и интерфейсы

## Описание
Завести в `ImageGen.Core` доменные типы скелета (сессия, сообщение, прогон, генерация, модель), интерфейсы платформы и хранилища, исключение провайдера и интерфейс генератора картинок. Логики почти нет: это общий словарь для задач 003–017. Понятия описаны в `docs/specs/chat-sessions.md`.

Зависит от: 001.
Среда: Linux или Windows (нужен только .NET 10 SDK, MAUI-нагрузки не нужны).

## Критерии готовности
- Все файлы из плана созданы с содержимым как в плане (имена, свойства, порядок значений enum, числовые значения enum).
- Тесты из плана проходят.
- `dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes` проходит.

## Анализ
- Числовые значения enum хранятся в БД (задача 003), поэтому заданы явно и не меняются.
- Тексты интерфейса ядро не формирует: ошибки передаются кодом `ErrorKind`.
- JSON настроек сессии сериализуется через source generation (`CoreJsonContext`), без рефлексии.
- Правила кода: `docs/conventions.md`, раздел «Правила для задач скелета v0.1».

## План
1. Создать файлы в `ImageGen/ImageGen.Core/Domain/` (пространство имён `ImageGen.Core.Domain`):

   `ChatRole.cs`
   ```csharp
   namespace ImageGen.Core.Domain;

   public enum ChatRole
   {
       User = 0,
       Assistant = 1,
   }
   ```

   `RunOrigin.cs`
   ```csharp
   namespace ImageGen.Core.Domain;

   /// <summary>What started a run.</summary>
   public enum RunOrigin
   {
       Assistant = 0,
       Repeat = 1,
       NewSeed = 2,
       Edit = 3,
   }
   ```

   `GenerationStatus.cs`
   ```csharp
   namespace ImageGen.Core.Domain;

   public enum GenerationStatus
   {
       Queued = 0,
       Running = 1,
       Succeeded = 2,
       Failed = 3,
       Cancelled = 4,
       Filtered = 5,
   }
   ```

   `ErrorKind.cs`
   ```csharp
   namespace ImageGen.Core.Domain;

   public enum ErrorKind
   {
       Unknown = 0,
       InvalidKey = 1,
       InsufficientFunds = 2,
       Moderated = 3,
       Forbidden = 4,
       BadRequest = 5,
       Timeout = 6,
       RateLimited = 7,
       ModelUnavailable = 8,
       Network = 9,
       Interrupted = 10,
       EmptyResponse = 11,
   }
   ```

   `ApiKeyStatus.cs`
   ```csharp
   namespace ImageGen.Core.Domain;

   public enum ApiKeyStatus
   {
       Valid = 0,
       Invalid = 1,
       NetworkError = 2,
   }
   ```

   `SessionSettings.cs`
   ```csharp
   namespace ImageGen.Core.Domain;

   public sealed class SessionSettings
   {
       public const string DefaultImageModelId = "black-forest-labs/flux.2-klein-4b";
       public const string DefaultAssistantModelId = "google/gemma-4-31b-it:free";
       public const int MinRunsPerModel = 1;
       public const int MaxRunsPerModel = 10;

       public List<string> ImageModelIds { get; set; } = [];

       public int RunsPerModel { get; set; } = MinRunsPerModel;

       public string AssistantModelId { get; set; } = DefaultAssistantModelId;

       public static SessionSettings CreateDefault() => new()
       {
           ImageModelIds = [DefaultImageModelId],
           RunsPerModel = MinRunsPerModel,
           AssistantModelId = DefaultAssistantModelId,
       };
   }
   ```

   `Session.cs`
   ```csharp
   namespace ImageGen.Core.Domain;

   public sealed class Session
   {
       public const string NewSessionTitle = "New session";
       public const int MaxTitleLength = 40;

       public required Guid Id { get; set; }

       public required string Title { get; set; }

       public required DateTimeOffset CreatedAt { get; set; }

       public required DateTimeOffset UpdatedAt { get; set; }

       public required SessionSettings Settings { get; set; }
   }
   ```

   `ChatMessage.cs`
   ```csharp
   namespace ImageGen.Core.Domain;

   public sealed class ChatMessage
   {
       public required Guid Id { get; set; }

       public required Guid SessionId { get; set; }

       public required ChatRole Role { get; set; }

       public required string Text { get; set; }

       public required DateTimeOffset CreatedAt { get; set; }

       /// <summary>Run started by this assistant message, if any.</summary>
       public Guid? RunId { get; set; }

       /// <summary>True for assistant messages that report an error.</summary>
       public bool IsError { get; set; }
   }
   ```

   `Run.cs`
   ```csharp
   namespace ImageGen.Core.Domain;

   public sealed class Run
   {
       public required Guid Id { get; set; }

       public required Guid SessionId { get; set; }

       public required string Prompt { get; set; }

       public required RunOrigin Origin { get; set; }

       public required DateTimeOffset CreatedAt { get; set; }
   }
   ```

   `Generation.cs`
   ```csharp
   namespace ImageGen.Core.Domain;

   public sealed class Generation
   {
       public required Guid Id { get; set; }

       public required Guid RunId { get; set; }

       public required Guid SessionId { get; set; }

       public required string ModelId { get; set; }

       /// <summary>Exact prompt sent to the model.</summary>
       public required string Prompt { get; set; }

       public required long Seed { get; set; }

       /// <summary>False when the model does not accept a seed and it was not sent.</summary>
       public required bool SeedApplied { get; set; }

       public required GenerationStatus Status { get; set; }

       /// <summary>Path relative to <c>IAppPaths.DataDirectory</c>.</summary>
       public string? ImagePath { get; set; }

       public string? MediaType { get; set; }

       public decimal? CostUsd { get; set; }

       public ErrorKind? ErrorKind { get; set; }

       public string? ErrorMessage { get; set; }

       public required DateTimeOffset CreatedAt { get; set; }

       public DateTimeOffset? CompletedAt { get; set; }

       /// <summary>Order inside the run, starting at 0.</summary>
       public required int SortOrder { get; set; }

       public bool IsFinished => Status is GenerationStatus.Succeeded or GenerationStatus.Failed
           or GenerationStatus.Cancelled or GenerationStatus.Filtered;
   }
   ```

   `ImageModel.cs`
   ```csharp
   namespace ImageGen.Core.Domain;

   public sealed class ImageModel
   {
       public required string Id { get; set; }

       public required string Name { get; set; }

       public required bool SupportsSeed { get; set; }

       public required bool SupportsImageInput { get; set; }

       public required int MaxImagesPerRequest { get; set; }

       public required DateTimeOffset FetchedAt { get; set; }
   }
   ```

   `SessionTimeline.cs`
   ```csharp
   namespace ImageGen.Core.Domain;

   public sealed record SessionTimeline(
       Session Session,
       IReadOnlyList<ChatMessage> Messages,
       IReadOnlyList<Run> Runs,
       IReadOnlyList<Generation> Generations);
   ```

2. Создать файлы в `ImageGen/ImageGen.Core/Platform/` (пространство имён `ImageGen.Core.Platform`):

   `ISecretStore.cs`
   ```csharp
   namespace ImageGen.Core.Platform;

   /// <summary>Encrypted platform storage for secrets such as API keys.</summary>
   public interface ISecretStore
   {
       Task<string?> GetAsync(string name, CancellationToken cancellationToken);

       Task SetAsync(string name, string value, CancellationToken cancellationToken);

       Task RemoveAsync(string name, CancellationToken cancellationToken);
   }
   ```

   `IAppPaths.cs`
   ```csharp
   namespace ImageGen.Core.Platform;

   public interface IAppPaths
   {
       /// <summary>Absolute path of the folder for the database and image files.</summary>
       string DataDirectory { get; }
   }
   ```

   `SecretNames.cs`
   ```csharp
   namespace ImageGen.Core.Platform;

   public static class SecretNames
   {
       public const string OpenRouterApiKey = "openrouter_api_key";
   }
   ```

3. Создать файлы в `ImageGen/ImageGen.Core/Storage/` (пространство имён `ImageGen.Core.Storage`; реализации в задачах 004 и 005):

   `ISessionRepository.cs`
   ```csharp
   using ImageGen.Core.Domain;

   namespace ImageGen.Core.Storage;

   public interface ISessionRepository
   {
       Task AddAsync(Session session, CancellationToken cancellationToken);

       Task UpdateAsync(Session session, CancellationToken cancellationToken);

       Task<Session?> GetAsync(Guid id, CancellationToken cancellationToken);

       /// <summary>All sessions ordered by <see cref="Session.UpdatedAt"/> descending.</summary>
       Task<IReadOnlyList<Session>> ListAsync(CancellationToken cancellationToken);

       /// <summary>Deletes the session with its messages, runs and generations.</summary>
       Task DeleteAsync(Guid id, CancellationToken cancellationToken);
   }
   ```

   `IChatMessageRepository.cs`
   ```csharp
   using ImageGen.Core.Domain;

   namespace ImageGen.Core.Storage;

   public interface IChatMessageRepository
   {
       Task AddAsync(ChatMessage message, CancellationToken cancellationToken);

       /// <summary>Messages of the session ordered by <see cref="ChatMessage.CreatedAt"/> ascending.</summary>
       Task<IReadOnlyList<ChatMessage>> ListBySessionAsync(Guid sessionId, CancellationToken cancellationToken);
   }
   ```

   `IRunRepository.cs`
   ```csharp
   using ImageGen.Core.Domain;

   namespace ImageGen.Core.Storage;

   public interface IRunRepository
   {
       /// <summary>Saves the run and its generations in one transaction.</summary>
       Task AddRunAsync(Run run, IReadOnlyList<Generation> generations, CancellationToken cancellationToken);

       Task<Run?> GetRunAsync(Guid id, CancellationToken cancellationToken);

       /// <summary>Runs of the session ordered by <see cref="Run.CreatedAt"/> ascending.</summary>
       Task<IReadOnlyList<Run>> ListRunsBySessionAsync(Guid sessionId, CancellationToken cancellationToken);

       Task<Generation?> GetGenerationAsync(Guid id, CancellationToken cancellationToken);

       /// <summary>Generations of the session ordered by run creation time, then <see cref="Generation.SortOrder"/>.</summary>
       Task<IReadOnlyList<Generation>> ListGenerationsBySessionAsync(Guid sessionId, CancellationToken cancellationToken);

       /// <summary>Overwrites all mutable fields of the generation.</summary>
       Task UpdateGenerationAsync(Generation generation, CancellationToken cancellationToken);

       /// <summary>Marks every Queued or Running generation as Failed with <see cref="ErrorKind.Interrupted"/>.</summary>
       /// <returns>Number of updated generations.</returns>
       Task<int> MarkUnfinishedAsInterruptedAsync(DateTimeOffset completedAt, CancellationToken cancellationToken);
   }
   ```

   `IImageModelRepository.cs`
   ```csharp
   using ImageGen.Core.Domain;

   namespace ImageGen.Core.Storage;

   public interface IImageModelRepository
   {
       /// <summary>Deletes all cached models and inserts the given ones in one transaction.</summary>
       Task ReplaceAllAsync(IReadOnlyList<ImageModel> models, CancellationToken cancellationToken);

       /// <summary>Cached models ordered by <see cref="ImageModel.Name"/> ascending.</summary>
       Task<IReadOnlyList<ImageModel>> ListAsync(CancellationToken cancellationToken);
   }
   ```

   `IImageFileStore.cs`
   ```csharp
   namespace ImageGen.Core.Storage;

   public interface IImageFileStore
   {
       /// <summary>Writes the image and returns its path relative to the data directory.</summary>
       Task<string> SaveAsync(Guid sessionId, Guid generationId, byte[] content, string mediaType, CancellationToken cancellationToken);

       /// <summary>Converts a relative path returned by <see cref="SaveAsync"/> to an absolute path.</summary>
       string GetFullPath(string relativePath);

       /// <summary>Deletes all image files of the session. Does nothing if there are none.</summary>
       Task DeleteSessionFilesAsync(Guid sessionId, CancellationToken cancellationToken);
   }
   ```

4. Создать файлы в `ImageGen/ImageGen.Core/Providers/` (пространство имён `ImageGen.Core.Providers`):

   `ProviderException.cs`
   ```csharp
   using ImageGen.Core.Domain;

   namespace ImageGen.Core.Providers;

   /// <summary>Error returned by a provider. The message never contains API keys.</summary>
   public sealed class ProviderException : Exception
   {
       public ProviderException(ErrorKind kind, string message, int? statusCode = null, Exception? innerException = null)
           : base(message, innerException)
       {
           Kind = kind;
           StatusCode = statusCode;
       }

       public ErrorKind Kind { get; }

       public int? StatusCode { get; }
   }
   ```

   `ImageGenerationRequest.cs`
   ```csharp
   namespace ImageGen.Core.Providers;

   /// <param name="Seed">Null when the seed must not be sent.</param>
   public sealed record ImageGenerationRequest(string ModelId, string Prompt, long? Seed);
   ```

   `ImageGenerationResult.cs`
   ```csharp
   namespace ImageGen.Core.Providers;

   public sealed record ImageGenerationResult(byte[] Content, string MediaType, decimal? CostUsd);
   ```

   `IImageGenerator.cs`
   ```csharp
   namespace ImageGen.Core.Providers;

   public interface IImageGenerator
   {
       /// <summary>Generates one image. Throws <see cref="ProviderException"/> on provider errors.</summary>
       Task<ImageGenerationResult> GenerateAsync(ImageGenerationRequest request, CancellationToken cancellationToken);
   }
   ```

5. Создать `ImageGen/ImageGen.Core/Serialization/CoreJsonContext.cs`:
   ```csharp
   using System.Text.Json.Serialization;
   using ImageGen.Core.Domain;

   namespace ImageGen.Core.Serialization;

   [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
   [JsonSerializable(typeof(SessionSettings))]
   public sealed partial class CoreJsonContext : JsonSerializerContext
   {
   }
   ```

6. Тесты в `ImageGen/ImageGen.Core.Tests/Domain/`:

   `SessionSettingsTests.cs`, класс `SessionSettingsTests`:
   - `CreateDefault_ReturnsDefaultModelAndOneRun`: `ImageModelIds` равен `["black-forest-labs/flux.2-klein-4b"]`, `RunsPerModel == 1`, `AssistantModelId == "google/gemma-4-31b-it:free"`.
   - `Serialize_ThenDeserialize_KeepsValues`: настройки с `ImageModelIds = ["a", "b"]`, `RunsPerModel = 3`, `AssistantModelId = "x"` → `JsonSerializer.Serialize(settings, CoreJsonContext.Default.SessionSettings)` → `JsonSerializer.Deserialize(json, CoreJsonContext.Default.SessionSettings)`; значения совпадают.
   - `Serialize_UsesCamelCaseNames`: JSON содержит `"imageModelIds"`, `"runsPerModel"`, `"assistantModelId"`.

   `GenerationTests.cs`, класс `GenerationTests`:
   - `IsFinished_ForEachStatus_ReturnsExpected` — `[Theory]` с `[InlineData]` на все 6 статусов: `Queued` и `Running` → `false`, остальные → `true`.

Файлы: `ImageGen/ImageGen.Core/Domain/*.cs`, `ImageGen/ImageGen.Core/Platform/*.cs`, `ImageGen/ImageGen.Core/Storage/*.cs`, `ImageGen/ImageGen.Core/Providers/*.cs`, `ImageGen/ImageGen.Core/Serialization/CoreJsonContext.cs`, `ImageGen/ImageGen.Core.Tests/Domain/*.cs`

### Проверка
```
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
scripts/check.sh
```

## Отчёт
- Созданы файлы плана дословно: `ImageGen.Core/Domain/` (enum-ы с явными значениями, `Session`, `ChatMessage`, `Run`, `Generation`, `ImageModel`, `SessionSettings`, `SessionTimeline`), `Platform/` (`ISecretStore`, `IAppPaths`, `SecretNames`), `Storage/` (интерфейсы репозиториев и `IImageFileStore`), `Providers/` (`ProviderException`, `ImageGenerationRequest`, `ImageGenerationResult`, `IImageGenerator`), `Serialization/CoreJsonContext.cs`.
- Тесты: `ImageGen.Core.Tests/Domain/SessionSettingsTests.cs` (3 теста) и `GenerationTests.cs` (6 случаев `IsFinished`).
- Проверено: `dotnet format --verify-no-changes` для Core и Core.Tests — без изменений; `dotnet test` — 10 тестов, все пройдены.

## Комментарии
