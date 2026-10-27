---
id: 011
status: planned
assignee:
claimed:
created: 2026-10-27
---

# Ядро: регистрация сервисов в DI

## Описание
Наполнить `AddImageGenCore()`: `TimeProvider`, типизированный `HttpClient` для OpenRouter, генератор картинок, источник сидов и все сервисы как singleton. Тест сборки контейнера из `AddImageGenCore()` + `AddImageGenData()` с проверкой при построении.

Зависит от: 005, 010.
Среда: Linux или Windows (нужен только .NET 10 SDK, MAUI-нагрузки не нужны).

## Критерии готовности
- `ImageGen/ImageGen.Core/CoreServiceCollectionExtensions.cs` совпадает с планом.
- Тест `AddImageGenCoreAndData_WithPlatformServices_ResolvesAllServices` и все остальные тесты обоих тестовых проектов проходят.
- `dotnet format` для всех четырёх проектов ядра и хранилища проходит.

## Анализ
- Хост (MAUI, позже Web) обязан зарегистрировать `ISecretStore` и `IAppPaths` и вызвать `AddImageGenData()`.
- `GenerationService` — singleton: он держит фоновые генерации и событие для UI.
- Тайм-аут HTTP 3 минуты: генерация картинки у части моделей идёт дольше 100 секунд (значение по умолчанию у `HttpClient`).
- Тест композиции лежит в `ImageGen.Data.Tests`, потому что этот проект видит и ядро, и хранилище.
- Код проверен сборкой и тестами 2026-10-27 в пробной копии решения.

## План
1. Заменить содержимое файла (дословно):

   `ImageGen/ImageGen.Core/CoreServiceCollectionExtensions.cs`
   ```csharp
   using ImageGen.Core.OpenRouter;
   using ImageGen.Core.Providers;
   using ImageGen.Core.Services;
   using Microsoft.Extensions.DependencyInjection;

   namespace ImageGen.Core;

   public static class CoreServiceCollectionExtensions
   {
       /// <summary>
       /// Registers core services. The host must also register <c>ISecretStore</c>, <c>IAppPaths</c>
       /// and storage (<c>AddImageGenData</c> from ImageGen.Data).
       /// </summary>
       public static IServiceCollection AddImageGenCore(this IServiceCollection services)
       {
           ArgumentNullException.ThrowIfNull(services);
           services.AddSingleton(TimeProvider.System);
           services.AddHttpClient<IOpenRouterClient, OpenRouterClient>(client =>
           {
               client.BaseAddress = OpenRouterClient.BaseAddress;
               client.Timeout = TimeSpan.FromMinutes(3);
           });
           services.AddSingleton<IImageGenerator, OpenRouterImageGenerator>();
           services.AddSingleton<ISeedSource, RandomSeedSource>();
           services.AddSingleton<ApiKeyService>();
           services.AddSingleton<ModelCatalogService>();
           services.AddSingleton<SessionService>();
           services.AddSingleton<GenerationService>();
           services.AddSingleton<AssistantService>();
           return services;
       }
   }
   ```

2. Создать тест (содержимое дословно):

   `ImageGen/ImageGen.Data.Tests/CompositionTests.cs`
   ```csharp
   using ImageGen.Core;
   using ImageGen.Core.OpenRouter;
   using ImageGen.Core.Platform;
   using ImageGen.Core.Providers;
   using ImageGen.Core.Services;
   using Microsoft.Extensions.DependencyInjection;

   namespace ImageGen.Data.Tests;

   public class CompositionTests
   {
       [Fact]
       public void AddImageGenCoreAndData_WithPlatformServices_ResolvesAllServices()
       {
           var paths = Substitute.For<IAppPaths>();
           paths.DataDirectory.Returns(Path.GetTempPath());
           var services = new ServiceCollection();
           services.AddLogging();
           services.AddSingleton(paths);
           services.AddSingleton(Substitute.For<ISecretStore>());

           services.AddImageGenCore();
           services.AddImageGenData();
           using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

           Assert.NotNull(provider.GetRequiredService<IOpenRouterClient>());
           Assert.NotNull(provider.GetRequiredService<IImageGenerator>());
           Assert.NotNull(provider.GetRequiredService<ApiKeyService>());
           Assert.NotNull(provider.GetRequiredService<ModelCatalogService>());
           Assert.NotNull(provider.GetRequiredService<SessionService>());
           Assert.NotNull(provider.GetRequiredService<GenerationService>());
           Assert.NotNull(provider.GetRequiredService<AssistantService>());
           Assert.Same(provider.GetRequiredService<GenerationService>(), provider.GetRequiredService<GenerationService>());
       }
   }
   ```

Файлы: `ImageGen/ImageGen.Core/CoreServiceCollectionExtensions.cs`, `ImageGen/ImageGen.Data.Tests/CompositionTests.cs`

### Проверка
```
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj --verify-no-changes
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
scripts/check.sh
```

## Отчёт

## Комментарии
