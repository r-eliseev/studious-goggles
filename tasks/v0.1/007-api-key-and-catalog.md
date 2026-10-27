---
id: 007
status: done
assignee:
claimed:
created: 2026-10-27
---

# Ядро: ключ OpenRouter и каталог моделей

## Описание
`ApiKeyService`: есть ли ключ, проверить и сохранить ключ (сохраняется только принятый OpenRouter), удалить, показать маску из последних 4 символов. `ModelCatalogService`: список моделей картинок с кешем в БД; при сбое сети отдаёт кеш; поиск модели по id.

Зависит от: 006.
Среда: Linux или Windows (нужен только .NET 10 SDK, MAUI-нагрузки не нужны).

## Критерии готовности
- Файлы из плана созданы с содержимым как в плане.
- Все тесты `ImageGen.Core.Tests` проходят.
- `dotnet format` для `ImageGen.Core` и `ImageGen.Core.Tests` проходит.

## Анализ
- Спецификация: F1.1–F1.3 (`docs/specs/image-client.md`), S1.3 и S5.1 (`docs/specs/chat-sessions.md`).
- Ключ не сохраняется при ответах «неверный» и «нет связи», чтобы приложение не работало с неизвестным ключом.
- Каталог перечитывается из API только при пустом кеше или явном обновлении (кнопка Refresh на экране моделей, задача 015).
- Код проверен сборкой и тестами 2026-10-27 в пробной копии решения.

## План
1. Создать файлы (содержимое дословно):

   `ImageGen/ImageGen.Core/Services/ApiKeyService.cs`
   ```csharp
   using ImageGen.Core.Domain;
   using ImageGen.Core.OpenRouter;
   using ImageGen.Core.Platform;

   namespace ImageGen.Core.Services;

   public sealed class ApiKeyService(ISecretStore secretStore, IOpenRouterClient client)
   {
       public async Task<bool> HasKeyAsync(CancellationToken cancellationToken)
       {
           var key = await secretStore.GetAsync(SecretNames.OpenRouterApiKey, cancellationToken);
           return !string.IsNullOrWhiteSpace(key);
       }

       /// <summary>Checks the key and saves it only when OpenRouter accepts it.</summary>
       public async Task<ApiKeyStatus> SaveAsync(string apiKey, CancellationToken cancellationToken)
       {
           ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
           var trimmed = apiKey.Trim();
           var status = await client.CheckKeyAsync(trimmed, cancellationToken);
           if (status == ApiKeyStatus.Valid)
           {
               await secretStore.SetAsync(SecretNames.OpenRouterApiKey, trimmed, cancellationToken);
           }

           return status;
       }

       public Task RemoveAsync(CancellationToken cancellationToken) =>
           secretStore.RemoveAsync(SecretNames.OpenRouterApiKey, cancellationToken);

       /// <summary>Returns "••••" plus the last 4 characters, or null when no key is saved.</summary>
       public async Task<string?> GetMaskedKeyAsync(CancellationToken cancellationToken)
       {
           var key = await secretStore.GetAsync(SecretNames.OpenRouterApiKey, cancellationToken);
           if (string.IsNullOrWhiteSpace(key))
           {
               return null;
           }

           return "••••" + (key.Length <= 4 ? key : key[^4..]);
       }
   }
   ```

   `ImageGen/ImageGen.Core/Services/ModelCatalogService.cs`
   ```csharp
   using ImageGen.Core.Domain;
   using ImageGen.Core.OpenRouter;
   using ImageGen.Core.Providers;
   using ImageGen.Core.Storage;

   namespace ImageGen.Core.Services;

   public sealed class ModelCatalogService(IOpenRouterClient client, IImageModelRepository repository)
   {
       /// <summary>
       /// Returns image models sorted by name. Uses the cache unless it is empty or <paramref name="forceRefresh"/> is true.
       /// If loading fails and the cache is not empty, returns the cache.
       /// </summary>
       public async Task<IReadOnlyList<ImageModel>> GetImageModelsAsync(bool forceRefresh, CancellationToken cancellationToken)
       {
           var cached = await repository.ListAsync(cancellationToken);
           if (!forceRefresh && cached.Count > 0)
           {
               return cached;
           }

           IReadOnlyList<ImageModel> loaded;
           try
           {
               loaded = await client.ListImageModelsAsync(cancellationToken);
           }
           catch (ProviderException) when (cached.Count > 0)
           {
               return cached;
           }

           await repository.ReplaceAllAsync(loaded, cancellationToken);
           return loaded.OrderBy(m => m.Name, StringComparer.Ordinal).ToList();
       }

       /// <summary>Finds a model by id using the cache (loads it when empty). Returns null when unknown.</summary>
       public async Task<ImageModel?> FindAsync(string modelId, CancellationToken cancellationToken)
       {
           var models = await GetImageModelsAsync(forceRefresh: false, cancellationToken);
           return models.FirstOrDefault(m => m.Id == modelId);
       }
   }
   ```

2. Создать тесты (содержимое дословно):

   `ImageGen/ImageGen.Core.Tests/Services/ApiKeyServiceTests.cs`
   ```csharp
   using ImageGen.Core.Domain;
   using ImageGen.Core.OpenRouter;
   using ImageGen.Core.Platform;
   using ImageGen.Core.Services;

   namespace ImageGen.Core.Tests.Services;

   public class ApiKeyServiceTests
   {
       private readonly ISecretStore _secrets = Substitute.For<ISecretStore>();
       private readonly IOpenRouterClient _client = Substitute.For<IOpenRouterClient>();
       private readonly ApiKeyService _service;

       public ApiKeyServiceTests()
       {
           _service = new ApiKeyService(_secrets, _client);
       }

       [Fact]
       public async Task SaveAsync_ValidKey_TrimsAndSaves()
       {
           var ct = TestContext.Current.CancellationToken;
           _client.CheckKeyAsync("sk-1", Arg.Any<CancellationToken>()).Returns(ApiKeyStatus.Valid);

           var status = await _service.SaveAsync("  sk-1  ", ct);

           Assert.Equal(ApiKeyStatus.Valid, status);
           await _secrets.Received(1).SetAsync(SecretNames.OpenRouterApiKey, "sk-1", Arg.Any<CancellationToken>());
       }

       [Theory]
       [InlineData(ApiKeyStatus.Invalid)]
       [InlineData(ApiKeyStatus.NetworkError)]
       public async Task SaveAsync_NotValid_DoesNotSave(ApiKeyStatus checkResult)
       {
           var ct = TestContext.Current.CancellationToken;
           _client.CheckKeyAsync("sk-1", Arg.Any<CancellationToken>()).Returns(checkResult);

           var status = await _service.SaveAsync("sk-1", ct);

           Assert.Equal(checkResult, status);
           await _secrets.DidNotReceive().SetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
       }

       [Theory]
       [InlineData(null, false)]
       [InlineData("", false)]
       [InlineData("sk-1", true)]
       public async Task HasKeyAsync_StoredValue_ReturnsExpected(string? stored, bool expected)
       {
           _secrets.GetAsync(SecretNames.OpenRouterApiKey, Arg.Any<CancellationToken>()).Returns(stored);

           Assert.Equal(expected, await _service.HasKeyAsync(TestContext.Current.CancellationToken));
       }

       [Fact]
       public async Task GetMaskedKeyAsync_StoredKey_ShowsOnlyLastFourCharacters()
       {
           _secrets.GetAsync(SecretNames.OpenRouterApiKey, Arg.Any<CancellationToken>()).Returns("sk-or-v1-abcdef1234");

           Assert.Equal("••••1234", await _service.GetMaskedKeyAsync(TestContext.Current.CancellationToken));
       }
   }
   ```

   `ImageGen/ImageGen.Core.Tests/Services/ModelCatalogServiceTests.cs`
   ```csharp
   using ImageGen.Core.Domain;
   using ImageGen.Core.OpenRouter;
   using ImageGen.Core.Providers;
   using ImageGen.Core.Services;
   using ImageGen.Core.Storage;

   namespace ImageGen.Core.Tests.Services;

   public class ModelCatalogServiceTests
   {
       private readonly IOpenRouterClient _client = Substitute.For<IOpenRouterClient>();
       private readonly IImageModelRepository _repository = Substitute.For<IImageModelRepository>();
       private readonly ModelCatalogService _service;

       public ModelCatalogServiceTests()
       {
           _service = new ModelCatalogService(_client, _repository);
       }

       public static ImageModel Model(string id, string name, bool supportsSeed = true) => new()
       {
           Id = id,
           Name = name,
           SupportsSeed = supportsSeed,
           SupportsImageInput = false,
           MaxImagesPerRequest = 1,
           FetchedAt = DateTimeOffset.UnixEpoch,
       };

       [Fact]
       public async Task GetImageModelsAsync_CacheNotEmpty_ReturnsCacheWithoutRequest()
       {
           var ct = TestContext.Current.CancellationToken;
           _repository.ListAsync(Arg.Any<CancellationToken>()).Returns([Model("a", "A")]);

           var models = await _service.GetImageModelsAsync(forceRefresh: false, ct);

           Assert.Equal(["a"], models.Select(m => m.Id));
           await _client.DidNotReceive().ListImageModelsAsync(Arg.Any<CancellationToken>());
       }

       [Fact]
       public async Task GetImageModelsAsync_CacheEmpty_LoadsSavesAndSortsByName()
       {
           var ct = TestContext.Current.CancellationToken;
           _repository.ListAsync(Arg.Any<CancellationToken>()).Returns([]);
           IReadOnlyList<ImageModel> loaded = [Model("z", "Zeta"), Model("a", "Alpha")];
           _client.ListImageModelsAsync(Arg.Any<CancellationToken>()).Returns(loaded);

           var models = await _service.GetImageModelsAsync(forceRefresh: false, ct);

           Assert.Equal(["Alpha", "Zeta"], models.Select(m => m.Name));
           await _repository.Received(1).ReplaceAllAsync(loaded, Arg.Any<CancellationToken>());
       }

       [Fact]
       public async Task GetImageModelsAsync_ForceRefreshFails_ReturnsCache()
       {
           var ct = TestContext.Current.CancellationToken;
           _repository.ListAsync(Arg.Any<CancellationToken>()).Returns([Model("a", "A")]);
           _client.ListImageModelsAsync(Arg.Any<CancellationToken>())
               .Returns(Task.FromException<IReadOnlyList<ImageModel>>(new ProviderException(ErrorKind.Network, "down")));

           var models = await _service.GetImageModelsAsync(forceRefresh: true, ct);

           Assert.Equal(["a"], models.Select(m => m.Id));
       }

       [Fact]
       public async Task GetImageModelsAsync_CacheEmptyAndLoadFails_Throws()
       {
           var ct = TestContext.Current.CancellationToken;
           _repository.ListAsync(Arg.Any<CancellationToken>()).Returns([]);
           _client.ListImageModelsAsync(Arg.Any<CancellationToken>())
               .Returns(Task.FromException<IReadOnlyList<ImageModel>>(new ProviderException(ErrorKind.Network, "down")));

           await Assert.ThrowsAsync<ProviderException>(() => _service.GetImageModelsAsync(forceRefresh: false, ct));
       }

       [Fact]
       public async Task FindAsync_KnownAndUnknownId_ReturnsModelOrNull()
       {
           var ct = TestContext.Current.CancellationToken;
           _repository.ListAsync(Arg.Any<CancellationToken>()).Returns([Model("a", "A")]);

           Assert.NotNull(await _service.FindAsync("a", ct));
           Assert.Null(await _service.FindAsync("missing", ct));
       }
   }
   ```

Файлы: `ImageGen/ImageGen.Core/Services/ApiKeyService.cs`, `ImageGen/ImageGen.Core/Services/ModelCatalogService.cs`, `ImageGen/ImageGen.Core.Tests/Services/ApiKeyServiceTests.cs`, `ImageGen/ImageGen.Core.Tests/Services/ModelCatalogServiceTests.cs`

### Проверка
```
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
scripts/check.sh
```

## Отчёт
- Создан `ImageGen/ImageGen.Core/Services/ApiKeyService.cs`: `HasKeyAsync`, `SaveAsync` (trim; ключ сохраняется в `ISecretStore` только при `ApiKeyStatus.Valid`), `RemoveAsync`, `GetMaskedKeyAsync` (маска «••••» + последние 4 символа, `null` без ключа).
- Создан `ImageGen/ImageGen.Core/Services/ModelCatalogService.cs`: `GetImageModelsAsync(forceRefresh, ct)` (кеш из `IImageModelRepository`, перечитывание из API только при пустом кеше или `forceRefresh`, при `ProviderException` с непустым кешем отдаётся кеш, результат сортируется по имени), `FindAsync(modelId, ct)`.
- Созданы тесты `ImageGen/ImageGen.Core.Tests/Services/ApiKeyServiceTests.cs` (4 теста с учётом Theory) и `ImageGen/ImageGen.Core.Tests/Services/ModelCatalogServiceTests.cs` (5 тестов); содержимое дословно по плану, тесты не ослаблялись. Сеть не используется, ключи в тестах только тестовые строки.
- `dotnet test ImageGen/ImageGen.Core.Tests`: 59 из 59 пройдены (включая тесты 002, 006 и архитектурный тест).
- `dotnet format --verify-no-changes` для `ImageGen.Core` и `ImageGen.Core.Tests`: без изменений (код 0).
- `docs/architecture.md`, раздел «Состояние кода»: добавлена запись о задаче 007. Модули `ApiKeyService` и `ModelCatalogService` уже были в дереве модулей, поэтому остальная документация не менялась. Спецификации не менялись: поведение совпадает с F1.1–F1.3 и S1.3/S5.1.
- Отклонений от плана нет. Пакеты и `Directory.Packages.props` не менялись, `scripts/` и `.github/workflows/` не трогались (кроме вызова `release-task.sh`).
- Статус задачи в файле был `planned`; реализация начата по явному указанию пользователя в запуске (не через `approved`). Это отмечено для проверки владельцем.

## Комментарии
