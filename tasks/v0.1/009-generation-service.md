---
id: 009
status: planned
assignee:
claimed:
created: 2026-10-27
---

# Ядро: прогоны и генерации

## Описание
`GenerationService` создаёт прогон на моделях сессии (N общих сидов на все модели), выполняет генерации параллельно в фоне (не больше 4 одновременно), сохраняет картинку в файл и запись в БД до события `GenerationUpdated`, отмечает отфильтрованные модерацией. Действия над генерацией: Repeat (тот же промпт, модель, сид), New seed (новый сид), Edit (новый промпт, та же модель и сид). При запуске приложения незавершённые генерации помечаются «Interrupted». `OpenRouterImageGenerator` — реализация `IImageGenerator` через `IOpenRouterClient`.

Зависит от: 006, 007.

## Критерии готовности
- Файлы из плана созданы с содержимым как в плане.
- Все тесты `ImageGen.Core.Tests` проходят.
- `dotnet format` для `ImageGen.Core` и `ImageGen.Core.Tests` проходит.

## Анализ
- Спецификация: S3.1–S3.8, S4.1–S4.4 (`docs/specs/chat-sessions.md`).
- Сиды от 0 до 2147483647 (`int.MaxValue`): этот диапазон принимают все модели с seed.
- Сид не отправляется модели, у которой в каталоге нет параметра `seed` (`SeedApplied = false`); неизвестная каталогу модель тоже считается без seed.
- Фоновые задачи запускаются через `Task.Run` и отслеживаются в словаре; `WhenIdleAsync` нужен тестам, чтобы дождаться окончания.
- Событие приходит в фоновом потоке и несёт копию генерации; MAUI переносит обновление в UI-поток (задача 014).
- Код проверен сборкой и тестами 2026-10-27 в пробной копии решения.

## План
1. Создать файлы (содержимое дословно):

   `ImageGen/ImageGen.Core/OpenRouter/OpenRouterImageGenerator.cs`
   ```csharp
   using ImageGen.Core.Providers;

   namespace ImageGen.Core.OpenRouter;

   /// <summary>Image generation through OpenRouter behind the provider-neutral <see cref="IImageGenerator"/>.</summary>
   public sealed class OpenRouterImageGenerator(IOpenRouterClient client) : IImageGenerator
   {
       public async Task<ImageGenerationResult> GenerateAsync(ImageGenerationRequest request, CancellationToken cancellationToken)
       {
           ArgumentNullException.ThrowIfNull(request);
           var result = await client.GenerateImageAsync(
               new OpenRouterImageRequest(request.ModelId, request.Prompt, request.Seed),
               cancellationToken);
           return new ImageGenerationResult(result.Content, result.MediaType, result.CostUsd);
       }
   }
   ```

   `ImageGen/ImageGen.Core/Services/ISeedSource.cs`
   ```csharp
   namespace ImageGen.Core.Services;

   public interface ISeedSource
   {
       /// <summary>Returns a seed from 0 to 2147483647 inclusive.</summary>
       long NextSeed();
   }
   ```

   `ImageGen/ImageGen.Core/Services/RandomSeedSource.cs`
   ```csharp
   namespace ImageGen.Core.Services;

   public sealed class RandomSeedSource : ISeedSource
   {
       public const long MaxSeed = int.MaxValue;

       public long NextSeed() => Random.Shared.NextInt64(0, MaxSeed + 1);
   }
   ```

   `ImageGen/ImageGen.Core/Services/GenerationUpdatedEventArgs.cs`
   ```csharp
   using ImageGen.Core.Domain;

   namespace ImageGen.Core.Services;

   /// <summary>Carries a snapshot of the generation after it was saved.</summary>
   public sealed class GenerationUpdatedEventArgs(Generation generation) : EventArgs
   {
       public Generation Generation { get; } = generation;
   }
   ```

   `ImageGen/ImageGen.Core/Services/GenerationService.cs`
   ```csharp
   using System.Collections.Concurrent;
   using ImageGen.Core.Domain;
   using ImageGen.Core.Providers;
   using ImageGen.Core.Storage;
   using Microsoft.Extensions.Logging;

   namespace ImageGen.Core.Services;

   /// <summary>Creates runs and executes their generations in the background. Register as a singleton.</summary>
   public sealed class GenerationService(
       ISessionRepository sessions,
       IRunRepository runs,
       IImageGenerator imageGenerator,
       IImageFileStore files,
       ModelCatalogService catalog,
       ISeedSource seedSource,
       TimeProvider timeProvider,
       ILogger<GenerationService> logger)
   {
       public const int MaxParallelGenerations = 4;

       private readonly SemaphoreSlim _parallelism = new(MaxParallelGenerations, MaxParallelGenerations);
       private readonly ConcurrentDictionary<Guid, Task> _running = new();

       /// <summary>Raised on a background thread after a generation was saved with a new status.</summary>
       public event EventHandler<GenerationUpdatedEventArgs>? GenerationUpdated;

       /// <summary>Starts a run on the session models: RunsPerModel seeds shared by all models.</summary>
       public async Task<Run> StartRunAsync(Guid sessionId, string prompt, CancellationToken cancellationToken)
       {
           ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
           var session = await sessions.GetAsync(sessionId, cancellationToken)
               ?? throw new InvalidOperationException($"Session {sessionId} not found.");
           if (session.Settings.ImageModelIds.Count == 0)
           {
               throw new InvalidOperationException("No image models are selected for this session.");
           }

           var seeds = Enumerable.Range(0, session.Settings.RunsPerModel).Select(_ => seedSource.NextSeed()).ToList();
           var items = new List<(string ModelId, long Seed)>();
           foreach (var modelId in session.Settings.ImageModelIds)
           {
               foreach (var seed in seeds)
               {
                   items.Add((modelId, seed));
               }
           }

           return await CreateAndStartRunAsync(sessionId, prompt.Trim(), RunOrigin.Assistant, items, cancellationToken);
       }

       /// <summary>Same prompt, model and seed as the given generation.</summary>
       public async Task<Run> RepeatAsync(Guid generationId, CancellationToken cancellationToken)
       {
           var source = await GetGenerationOrThrowAsync(generationId, cancellationToken);
           return await CreateAndStartRunAsync(
               source.SessionId, source.Prompt, RunOrigin.Repeat, [(source.ModelId, source.Seed)], cancellationToken);
       }

       /// <summary>Same prompt and model as the given generation with a new random seed.</summary>
       public async Task<Run> NewSeedAsync(Guid generationId, CancellationToken cancellationToken)
       {
           var source = await GetGenerationOrThrowAsync(generationId, cancellationToken);
           return await CreateAndStartRunAsync(
               source.SessionId, source.Prompt, RunOrigin.NewSeed, [(source.ModelId, seedSource.NextSeed())], cancellationToken);
       }

       /// <summary>Same model and seed as the given generation with an edited prompt.</summary>
       public async Task<Run> EditAsync(Guid generationId, string newPrompt, CancellationToken cancellationToken)
       {
           ArgumentException.ThrowIfNullOrWhiteSpace(newPrompt);
           var source = await GetGenerationOrThrowAsync(generationId, cancellationToken);
           return await CreateAndStartRunAsync(
               source.SessionId, newPrompt.Trim(), RunOrigin.Edit, [(source.ModelId, source.Seed)], cancellationToken);
       }

       /// <summary>Marks generations left Queued or Running by a previous app start as interrupted.</summary>
       public Task<int> RecoverInterruptedAsync(CancellationToken cancellationToken) =>
           runs.MarkUnfinishedAsInterruptedAsync(timeProvider.GetUtcNow(), cancellationToken);

       /// <summary>Completes when all generations started so far have finished. Used by tests.</summary>
       public Task WhenIdleAsync() => Task.WhenAll(_running.Values);

       private async Task<Generation> GetGenerationOrThrowAsync(Guid generationId, CancellationToken cancellationToken) =>
           await runs.GetGenerationAsync(generationId, cancellationToken)
           ?? throw new InvalidOperationException($"Generation {generationId} not found.");

       private async Task<Run> CreateAndStartRunAsync(
           Guid sessionId,
           string prompt,
           RunOrigin origin,
           IReadOnlyList<(string ModelId, long Seed)> items,
           CancellationToken cancellationToken)
       {
           var now = timeProvider.GetUtcNow();
           var run = new Run
           {
               Id = Guid.NewGuid(),
               SessionId = sessionId,
               Prompt = prompt,
               Origin = origin,
               CreatedAt = now,
           };

           var generations = new List<Generation>();
           for (var index = 0; index < items.Count; index++)
           {
               var model = await catalog.FindAsync(items[index].ModelId, cancellationToken);
               generations.Add(new Generation
               {
                   Id = Guid.NewGuid(),
                   RunId = run.Id,
                   SessionId = sessionId,
                   ModelId = items[index].ModelId,
                   Prompt = prompt,
                   Seed = items[index].Seed,
                   SeedApplied = model?.SupportsSeed ?? false,
                   Status = GenerationStatus.Queued,
                   CreatedAt = now,
                   SortOrder = index,
               });
           }

           await runs.AddRunAsync(run, generations, cancellationToken);

           foreach (var generation in generations)
           {
               var task = Task.Run(() => ExecuteAsync(generation), CancellationToken.None);
               _running[generation.Id] = task;
               _ = task.ContinueWith(_ => _running.TryRemove(generation.Id, out var _), TaskScheduler.Default);
           }

           return run;
       }

       private async Task ExecuteAsync(Generation generation)
       {
           await _parallelism.WaitAsync();
           try
           {
               generation.Status = GenerationStatus.Running;
               await SaveAndNotifyAsync(generation);

               try
               {
                   var result = await imageGenerator.GenerateAsync(
                       new ImageGenerationRequest(generation.ModelId, generation.Prompt, generation.SeedApplied ? generation.Seed : null),
                       CancellationToken.None);
                   generation.ImagePath = await files.SaveAsync(
                       generation.SessionId, generation.Id, result.Content, result.MediaType, CancellationToken.None);
                   generation.MediaType = result.MediaType;
                   generation.CostUsd = result.CostUsd;
                   generation.Status = GenerationStatus.Succeeded;
               }
               catch (ProviderException ex)
               {
                   generation.Status = ex.Kind == ErrorKind.Moderated ? GenerationStatus.Filtered : GenerationStatus.Failed;
                   generation.ErrorKind = ex.Kind;
                   generation.ErrorMessage = ex.Message;
               }
               catch (Exception ex)
               {
                   logger.LogError(ex, "Generation {GenerationId} failed", generation.Id);
                   generation.Status = GenerationStatus.Failed;
                   generation.ErrorKind = ErrorKind.Unknown;
                   generation.ErrorMessage = ex.Message;
               }

               generation.CompletedAt = timeProvider.GetUtcNow();
               await SaveAndNotifyAsync(generation);
           }
           catch (Exception ex)
           {
               logger.LogError(ex, "Could not save generation {GenerationId}", generation.Id);
           }
           finally
           {
               _parallelism.Release();
           }
       }

       private async Task SaveAndNotifyAsync(Generation generation)
       {
           await runs.UpdateGenerationAsync(generation, CancellationToken.None);
           GenerationUpdated?.Invoke(this, new GenerationUpdatedEventArgs(Copy(generation)));
       }

       private static Generation Copy(Generation g) => new()
       {
           Id = g.Id,
           RunId = g.RunId,
           SessionId = g.SessionId,
           ModelId = g.ModelId,
           Prompt = g.Prompt,
           Seed = g.Seed,
           SeedApplied = g.SeedApplied,
           Status = g.Status,
           ImagePath = g.ImagePath,
           MediaType = g.MediaType,
           CostUsd = g.CostUsd,
           ErrorKind = g.ErrorKind,
           ErrorMessage = g.ErrorMessage,
           CreatedAt = g.CreatedAt,
           CompletedAt = g.CompletedAt,
           SortOrder = g.SortOrder,
       };
   }
   ```

2. Создать тесты (содержимое дословно; используют `ModelCatalogServiceTests.Model` из задачи 007):

   `ImageGen/ImageGen.Core.Tests/Services/GenerationServiceTests.cs`
   ```csharp
   using System.Collections.Concurrent;
   using ImageGen.Core.Domain;
   using ImageGen.Core.OpenRouter;
   using ImageGen.Core.Providers;
   using ImageGen.Core.Services;
   using ImageGen.Core.Storage;
   using Microsoft.Extensions.Logging.Abstractions;

   namespace ImageGen.Core.Tests.Services;

   public class GenerationServiceTests
   {
       private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

       private readonly ISessionRepository _sessions = Substitute.For<ISessionRepository>();
       private readonly IRunRepository _runs = Substitute.For<IRunRepository>();
       private readonly IImageGenerator _generator = Substitute.For<IImageGenerator>();
       private readonly IImageFileStore _files = Substitute.For<IImageFileStore>();
       private readonly IImageModelRepository _models = Substitute.For<IImageModelRepository>();
       private readonly ISeedSource _seeds = Substitute.For<ISeedSource>();
       private readonly ConcurrentBag<Generation> _events = [];
       private readonly GenerationService _service;
       private readonly Session _session;
       private Run? _savedRun;
       private IReadOnlyList<Generation> _savedGenerations = [];

       public GenerationServiceTests()
       {
           var time = Substitute.For<TimeProvider>();
           time.GetUtcNow().Returns(Now);
           _models.ListAsync(Arg.Any<CancellationToken>()).Returns(
           [
               ModelCatalogServiceTests.Model("seeded", "Seeded", supportsSeed: true),
               ModelCatalogServiceTests.Model("unseeded", "Unseeded", supportsSeed: false),
           ]);
           var catalog = new ModelCatalogService(Substitute.For<IOpenRouterClient>(), _models);
           _session = new Session
           {
               Id = Guid.NewGuid(),
               Title = "s",
               CreatedAt = Now,
               UpdatedAt = Now,
               Settings = new SessionSettings { ImageModelIds = ["seeded", "unseeded"], RunsPerModel = 2 },
           };
           _sessions.GetAsync(_session.Id, Arg.Any<CancellationToken>()).Returns(_session);
           _runs.AddRunAsync(Arg.Do<Run>(r => _savedRun = r), Arg.Do<IReadOnlyList<Generation>>(g => _savedGenerations = g), Arg.Any<CancellationToken>())
               .Returns(Task.CompletedTask);
           _seeds.NextSeed().Returns(11L, 22L, 33L);
           _files.SaveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
               .Returns("images/x.png");
           _generator.GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>())
               .Returns(new ImageGenerationResult([1, 2, 3], "image/png", 0.01m));
           _service = new GenerationService(
               _sessions, _runs, _generator, _files, catalog, _seeds, time, NullLogger<GenerationService>.Instance);
           _service.GenerationUpdated += (_, e) => _events.Add(e.Generation);
       }

       [Fact]
       public async Task StartRunAsync_TwoModelsTwoRuns_CreatesFourGenerationsWithSharedSeeds()
       {
           var run = await _service.StartRunAsync(_session.Id, " a fox ", TestContext.Current.CancellationToken);
           await _service.WhenIdleAsync();

           Assert.Same(run, _savedRun);
           Assert.Equal("a fox", run.Prompt);
           Assert.Equal(RunOrigin.Assistant, run.Origin);
           Assert.Equal(
               [("seeded", 11L), ("seeded", 22L), ("unseeded", 11L), ("unseeded", 22L)],
               _savedGenerations.Select(g => (g.ModelId, g.Seed)));
           Assert.Equal([0, 1, 2, 3], _savedGenerations.Select(g => g.SortOrder));
           Assert.All(_savedGenerations.Where(g => g.ModelId == "seeded"), g => Assert.True(g.SeedApplied));
           Assert.All(_savedGenerations.Where(g => g.ModelId == "unseeded"), g => Assert.False(g.SeedApplied));
       }

       [Fact]
       public async Task StartRunAsync_Success_SavesFileThenMarksSucceeded()
       {
           await _service.StartRunAsync(_session.Id, "a fox", TestContext.Current.CancellationToken);
           await _service.WhenIdleAsync();

           Assert.All(_savedGenerations, g =>
           {
               Assert.Equal(GenerationStatus.Succeeded, g.Status);
               Assert.Equal("images/x.png", g.ImagePath);
               Assert.Equal("image/png", g.MediaType);
               Assert.Equal(0.01m, g.CostUsd);
               Assert.Equal(Now, g.CompletedAt);
           });
           Assert.Equal(4, _events.Count(e => e.Status == GenerationStatus.Running));
           Assert.Equal(4, _events.Count(e => e.Status == GenerationStatus.Succeeded));
       }

       [Fact]
       public async Task StartRunAsync_SeedOnlyForSeededModel_IsSentToGenerator()
       {
           await _service.StartRunAsync(_session.Id, "a fox", TestContext.Current.CancellationToken);
           await _service.WhenIdleAsync();

           await _generator.Received(2).GenerateAsync(
               Arg.Is<ImageGenerationRequest>(r => r.ModelId == "seeded" && r.Seed != null), Arg.Any<CancellationToken>());
           await _generator.Received(2).GenerateAsync(
               Arg.Is<ImageGenerationRequest>(r => r.ModelId == "unseeded" && r.Seed == null), Arg.Any<CancellationToken>());
       }

       [Fact]
       public async Task StartRunAsync_ModeratedError_MarksFiltered()
       {
           _generator.GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>())
               .Returns(Task.FromException<ImageGenerationResult>(new ProviderException(ErrorKind.Moderated, "flagged", 403)));

           await _service.StartRunAsync(_session.Id, "a fox", TestContext.Current.CancellationToken);
           await _service.WhenIdleAsync();

           Assert.All(_savedGenerations, g =>
           {
               Assert.Equal(GenerationStatus.Filtered, g.Status);
               Assert.Equal(ErrorKind.Moderated, g.ErrorKind);
           });
       }

       [Fact]
       public async Task StartRunAsync_OneModelFails_OthersSucceed()
       {
           _generator.GenerateAsync(Arg.Is<ImageGenerationRequest>(r => r.ModelId == "unseeded"), Arg.Any<CancellationToken>())
               .Returns(Task.FromException<ImageGenerationResult>(new ProviderException(ErrorKind.RateLimited, "slow down", 429)));

           await _service.StartRunAsync(_session.Id, "a fox", TestContext.Current.CancellationToken);
           await _service.WhenIdleAsync();

           Assert.All(_savedGenerations.Where(g => g.ModelId == "seeded"), g => Assert.Equal(GenerationStatus.Succeeded, g.Status));
           Assert.All(_savedGenerations.Where(g => g.ModelId == "unseeded"), g =>
           {
               Assert.Equal(GenerationStatus.Failed, g.Status);
               Assert.Equal(ErrorKind.RateLimited, g.ErrorKind);
               Assert.Equal("slow down", g.ErrorMessage);
           });
       }

       [Fact]
       public async Task StartRunAsync_NoModels_ThrowsInvalidOperation()
       {
           _session.Settings.ImageModelIds = [];

           await Assert.ThrowsAsync<InvalidOperationException>(
               () => _service.StartRunAsync(_session.Id, "a fox", TestContext.Current.CancellationToken));
       }

       private Generation Source() => new()
       {
           Id = Guid.NewGuid(),
           RunId = Guid.NewGuid(),
           SessionId = _session.Id,
           ModelId = "seeded",
           Prompt = "old prompt",
           Seed = 777,
           SeedApplied = true,
           Status = GenerationStatus.Succeeded,
           CreatedAt = Now,
           SortOrder = 0,
       };

       [Fact]
       public async Task RepeatAsync_UsesSamePromptModelAndSeed()
       {
           var source = Source();
           _runs.GetGenerationAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);

           var run = await _service.RepeatAsync(source.Id, TestContext.Current.CancellationToken);
           await _service.WhenIdleAsync();

           Assert.Equal(RunOrigin.Repeat, run.Origin);
           var generation = Assert.Single(_savedGenerations);
           Assert.Equal(("seeded", "old prompt", 777L), (generation.ModelId, generation.Prompt, generation.Seed));
       }

       [Fact]
       public async Task NewSeedAsync_UsesNewSeed()
       {
           var source = Source();
           _runs.GetGenerationAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);

           var run = await _service.NewSeedAsync(source.Id, TestContext.Current.CancellationToken);
           await _service.WhenIdleAsync();

           Assert.Equal(RunOrigin.NewSeed, run.Origin);
           var generation = Assert.Single(_savedGenerations);
           Assert.Equal(("seeded", "old prompt", 11L), (generation.ModelId, generation.Prompt, generation.Seed));
       }

       [Fact]
       public async Task EditAsync_UsesNewPromptSameModelAndSeed()
       {
           var source = Source();
           _runs.GetGenerationAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);

           var run = await _service.EditAsync(source.Id, " new prompt ", TestContext.Current.CancellationToken);
           await _service.WhenIdleAsync();

           Assert.Equal(RunOrigin.Edit, run.Origin);
           Assert.Equal("new prompt", run.Prompt);
           var generation = Assert.Single(_savedGenerations);
           Assert.Equal(("seeded", "new prompt", 777L), (generation.ModelId, generation.Prompt, generation.Seed));
       }

       [Fact]
       public async Task RecoverInterruptedAsync_CallsRepositoryWithNow()
       {
           _runs.MarkUnfinishedAsInterruptedAsync(Now, Arg.Any<CancellationToken>()).Returns(3);

           Assert.Equal(3, await _service.RecoverInterruptedAsync(TestContext.Current.CancellationToken));
       }
   }
   ```

Файлы: `ImageGen/ImageGen.Core/OpenRouter/OpenRouterImageGenerator.cs`, `ImageGen/ImageGen.Core/Services/ISeedSource.cs`, `ImageGen/ImageGen.Core/Services/RandomSeedSource.cs`, `ImageGen/ImageGen.Core/Services/GenerationUpdatedEventArgs.cs`, `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `ImageGen/ImageGen.Core.Tests/Services/GenerationServiceTests.cs`

### Проверка
```
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
scripts/check.sh
```

## Отчёт

## Комментарии
