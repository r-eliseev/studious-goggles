---
id: 005
status: planned
assignee:
claimed:
created: 2026-10-27
---

# Хранилище: файлы картинок и регистрация в DI

## Описание
Реализовать `IImageFileStore` (картинки в `images/<sessionId>/<generationId>.<ext>` внутри `IAppPaths.DataDirectory`) и метод `AddImageGenData()`, который регистрирует базу, репозитории и файловое хранилище как singleton.

Зависит от: 004.

## Критерии готовности
- Файлы из плана созданы с содержимым как в плане.
- Все тесты `ImageGen.Data.Tests` проходят.
- `dotnet format` для `ImageGen.Data` и `ImageGen.Data.Tests` проходит.

## Анализ
- В БД хранится путь относительно папки данных, поэтому смена папки не ломает записи.
- Расширение файла определяется по `media_type` ответа OpenRouter; неизвестный тип получает `.bin`.
- Код проверен сборкой и тестами 2026-10-27 в пробной копии решения.

## План
1. Создать файлы (содержимое дословно):

   `ImageGen/ImageGen.Data/FileImageStore.cs`
   ```csharp
   using ImageGen.Core.Platform;
   using ImageGen.Core.Storage;

   namespace ImageGen.Data;

   /// <summary>Stores images as files under <c>{DataDirectory}/images/{sessionId}/{generationId}.{ext}</c>.</summary>
   public sealed class FileImageStore(IAppPaths appPaths) : IImageFileStore
   {
       public const string ImagesFolder = "images";

       public async Task<string> SaveAsync(
           Guid sessionId,
           Guid generationId,
           byte[] content,
           string mediaType,
           CancellationToken cancellationToken)
       {
           ArgumentNullException.ThrowIfNull(content);
           ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);

           var relativePath = Path.Combine(
               ImagesFolder,
               sessionId.ToString("N"),
               generationId.ToString("N") + GetExtension(mediaType));
           var fullPath = GetFullPath(relativePath);
           Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
           await File.WriteAllBytesAsync(fullPath, content, cancellationToken);
           return relativePath;
       }

       public string GetFullPath(string relativePath)
       {
           ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
           return Path.Combine(appPaths.DataDirectory, relativePath);
       }

       public Task DeleteSessionFilesAsync(Guid sessionId, CancellationToken cancellationToken)
       {
           var folder = Path.Combine(appPaths.DataDirectory, ImagesFolder, sessionId.ToString("N"));
           if (Directory.Exists(folder))
           {
               Directory.Delete(folder, recursive: true);
           }

           return Task.CompletedTask;
       }

       /// <summary>Maps a media type to a file extension with a leading dot.</summary>
       public static string GetExtension(string mediaType) => mediaType.ToLowerInvariant() switch
       {
           "image/png" => ".png",
           "image/jpeg" => ".jpg",
           "image/jpg" => ".jpg",
           "image/webp" => ".webp",
           "image/gif" => ".gif",
           _ => ".bin",
       };
   }
   ```

   `ImageGen/ImageGen.Data/DataServiceCollectionExtensions.cs`
   ```csharp
   using ImageGen.Core.Storage;
   using ImageGen.Data.Repositories;
   using Microsoft.Extensions.DependencyInjection;

   namespace ImageGen.Data;

   public static class DataServiceCollectionExtensions
   {
       /// <summary>Registers the SQLite database, repositories and the image file store. Requires an <c>IAppPaths</c> registration.</summary>
       public static IServiceCollection AddImageGenData(this IServiceCollection services)
       {
           ArgumentNullException.ThrowIfNull(services);
           services.AddSingleton<SqliteDatabase>();
           services.AddSingleton<ISessionRepository, SessionRepository>();
           services.AddSingleton<IChatMessageRepository, ChatMessageRepository>();
           services.AddSingleton<IRunRepository, RunRepository>();
           services.AddSingleton<IImageModelRepository, ImageModelRepository>();
           services.AddSingleton<IImageFileStore, FileImageStore>();
           return services;
       }
   }
   ```

2. Создать тесты (содержимое дословно):

   `ImageGen/ImageGen.Data.Tests/FileImageStoreTests.cs`
   ```csharp
   using ImageGen.Core.Platform;

   namespace ImageGen.Data.Tests;

   public sealed class FileImageStoreTests : IDisposable
   {
       private readonly string _directory = Path.Combine(Path.GetTempPath(), "imagegen-tests", Guid.NewGuid().ToString("N"));
       private readonly FileImageStore _store;

       public FileImageStoreTests()
       {
           var paths = Substitute.For<IAppPaths>();
           paths.DataDirectory.Returns(_directory);
           _store = new FileImageStore(paths);
       }

       public void Dispose()
       {
           if (Directory.Exists(_directory))
           {
               Directory.Delete(_directory, recursive: true);
           }
       }

       [Fact]
       public async Task SaveAsync_Png_WritesFileAndReturnsRelativePath()
       {
           var ct = TestContext.Current.CancellationToken;
           var sessionId = Guid.NewGuid();
           var generationId = Guid.NewGuid();
           byte[] content = [1, 2, 3];

           var relative = await _store.SaveAsync(sessionId, generationId, content, "image/png", ct);

           Assert.Equal(Path.Combine("images", sessionId.ToString("N"), generationId.ToString("N") + ".png"), relative);
           Assert.Equal(content, await File.ReadAllBytesAsync(_store.GetFullPath(relative), ct));
       }

       [Theory]
       [InlineData("image/png", ".png")]
       [InlineData("image/jpeg", ".jpg")]
       [InlineData("IMAGE/WEBP", ".webp")]
       [InlineData("application/octet-stream", ".bin")]
       public void GetExtension_MediaType_ReturnsExtension(string mediaType, string expected)
       {
           Assert.Equal(expected, FileImageStore.GetExtension(mediaType));
       }

       [Fact]
       public async Task DeleteSessionFilesAsync_ExistingFiles_DeletesFolder()
       {
           var ct = TestContext.Current.CancellationToken;
           var sessionId = Guid.NewGuid();
           var relative = await _store.SaveAsync(sessionId, Guid.NewGuid(), [1], "image/png", ct);

           await _store.DeleteSessionFilesAsync(sessionId, ct);

           Assert.False(File.Exists(_store.GetFullPath(relative)));
       }

       [Fact]
       public async Task DeleteSessionFilesAsync_NoFiles_DoesNotThrow()
       {
           await _store.DeleteSessionFilesAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);
       }
   }
   ```

   `ImageGen/ImageGen.Data.Tests/DataServiceCollectionExtensionsTests.cs`
   ```csharp
   using ImageGen.Core.Platform;
   using ImageGen.Core.Storage;
   using Microsoft.Extensions.DependencyInjection;

   namespace ImageGen.Data.Tests;

   public class DataServiceCollectionExtensionsTests
   {
       [Fact]
       public void AddImageGenData_WithAppPaths_ResolvesAllStorageServices()
       {
           var paths = Substitute.For<IAppPaths>();
           paths.DataDirectory.Returns(Path.GetTempPath());
           var services = new ServiceCollection();
           services.AddSingleton(paths);

           services.AddImageGenData();
           using var provider = services.BuildServiceProvider();

           Assert.NotNull(provider.GetRequiredService<SqliteDatabase>());
           Assert.NotNull(provider.GetRequiredService<ISessionRepository>());
           Assert.NotNull(provider.GetRequiredService<IChatMessageRepository>());
           Assert.NotNull(provider.GetRequiredService<IRunRepository>());
           Assert.NotNull(provider.GetRequiredService<IImageModelRepository>());
           Assert.NotNull(provider.GetRequiredService<IImageFileStore>());
       }
   }
   ```

Файлы: `ImageGen/ImageGen.Data/FileImageStore.cs`, `ImageGen/ImageGen.Data/DataServiceCollectionExtensions.cs`, `ImageGen/ImageGen.Data.Tests/FileImageStoreTests.cs`, `ImageGen/ImageGen.Data.Tests/DataServiceCollectionExtensionsTests.cs`

### Проверка
```
dotnet format ImageGen/ImageGen.Data/ImageGen.Data.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj --verify-no-changes
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
scripts/check.sh
```

## Отчёт

## Комментарии
