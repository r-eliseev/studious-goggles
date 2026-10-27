---
id: 011
status: done
assignee:
claimed:
created: 2026-10-27
---

# Ядро: регистрация сервисов в DI

## Описание
Наполнить `AddImageGenCore()`: `TimeProvider`, типизированный `HttpClient` для OpenRouter, интерфейсы провайдера, источник сидов и все сервисы как singleton. Тест сборки контейнера из `AddImageGenCore()` + `AddImageGenData()` с проверкой при построении.

Зависит от: 005, 010.
Среда: Linux или Windows (нужен только .NET 10 SDK, MAUI-нагрузки не нужны).

## Критерии готовности
- `AddImageGenCore()` регистрирует всё из плана.
- Тест композиции и все остальные тесты обоих тестовых проектов проходят.
- `dotnet format` для всех четырёх проектов ядра и хранилища проходит.

## Анализ
- Хост (MAUI, позже Web) обязан зарегистрировать `ISecretStore` и `IAppPaths` и вызвать `AddImageGenData()`.
- `OpenRouterClient` реализует три интерфейса: `IModelProvider`, `IImageGenerator` (через `IModelProvider`) и `IOpenRouterChatClient` (задача 010). Регистрируется один типизированный клиент, а интерфейсы отдают его же.
- `GenerationService` — singleton: он держит фоновые генерации и событие для UI.
- Тайм-аут HTTP 3 минуты: генерация картинки у части моделей идёт дольше 100 секунд (значение по умолчанию у `HttpClient`).
- Тест композиции лежит в `ImageGen.Data.Tests`, потому что этот проект видит и ядро, и хранилище.
- Пробная реализация: `poc/v0.1/011-core-di/` (подсказка, не копировать дословно, см. `poc/README.md`).

## План
1. `CoreServiceCollectionExtensions.AddImageGenCore()`:
   - singleton `TimeProvider.System`;
   - `AddHttpClient<OpenRouterClient>`: `BaseAddress = OpenRouterClient.BaseAddress`, `Timeout = 3 минуты`;
   - `IModelProvider`, `IImageGenerator`, `IOpenRouterChatClient` → фабрикой, которая возвращает `OpenRouterClient` из контейнера;
   - singleton: `ISeedSource` → `RandomSeedSource`, `ApiKeyService`, `ModelCatalogService`, `SessionService`, `GenerationService`, `AssistantService`.
   - XML-комментарий: что хост должен зарегистрировать сам (`ISecretStore`, `IAppPaths`, `AddImageGenData`).
2. Тест `ImageGen.Data.Tests/CompositionTests.cs`: контейнер с логированием, подменёнными `IAppPaths` (временный каталог) и `ISecretStore`, `AddImageGenCore()` и `AddImageGenData()`, построение с `ValidateOnBuild = true`. Ожидается:
   - разрешаются `IModelProvider`, `IOpenRouterChatClient`, `IImageGenerator` и все пять сервисов;
   - `GenerationService` — один и тот же экземпляр при двух запросах.
3. Если для теста нужен пакет `Microsoft.Extensions.DependencyInjection` в `ImageGen.Data.Tests` — он уже подключён (задача 003); новые пакеты не добавлять.

Файлы: `ImageGen/ImageGen.Core/CoreServiceCollectionExtensions.cs`, `ImageGen/ImageGen.Data.Tests/CompositionTests.cs`

### Проверка
```
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data/ImageGen.Data.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj --verify-no-changes
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
scripts/check.sh
```

## Отчёт
- `CoreServiceCollectionExtensions.AddImageGenCore()` наполнен по плану: `TimeProvider.System` (singleton); `AddHttpClient<OpenRouterClient>` с `BaseAddress = OpenRouterClient.BaseAddress` и `Timeout = 3 минуты`; `IModelProvider`, `IImageGenerator`, `IOpenRouterChatClient` через фабрики, которые возвращают `OpenRouterClient` из контейнера; singleton: `ISeedSource` → `RandomSeedSource`, `ApiKeyService`, `ModelCatalogService`, `SessionService`, `GenerationService`, `AssistantService`.
- XML-комментарий перечисляет, что хост регистрирует сам: `ISecretStore`, `IAppPaths`, вызов `AddImageGenData()`.
- Отклонение: интерфейсы провайдера зарегистрированы как transient (план не задавал время жизни). Типизированный клиент `OpenRouterClient` регистрируется `AddHttpClient<T>` как transient, и фабрики следуют ему. Замечание: singleton-сервисы (`ApiKeyService`, `ModelCatalogService`, `GenerationService`, `AssistantService`) фиксируют один экземпляр клиента и его `HttpClient`, поэтому ротация обработчиков `HttpClientFactory` до них не доходит. Не исправлял; при необходимости вынести в отдельную задачу.
- Тест `ImageGen.Data.Tests/CompositionTests.cs` (файл из плана): контейнер с `AddLogging()`, подменённые `IAppPaths` (системный временный каталог) и `ISecretStore`, `AddImageGenCore()` + `AddImageGenData()`, построение с `ValidateOnBuild = true` и `ValidateScopes = true`. Проверяется разрешение `IModelProvider`, `IOpenRouterChatClient`, `IImageGenerator` и всех пяти сервисов; отдельный тест проверяет, что `GenerationService`, `AssistantService` и `SessionService` выдаются одним экземпляром. Singleton для `ApiKeyService`, `ModelCatalogService` и `ISeedSource` тестом не проверяется.
- Новые пакеты не добавлялись, `Directory.Packages.props` не менялся. `poc/v0.1/011-core-di/` не использовался.
- Результаты: `dotnet format --verify-no-changes` для `ImageGen.Core`, `ImageGen.Core.Tests`, `ImageGen.Data`, `ImageGen.Data.Tests` — код возврата 0. `ImageGen.Core.Tests`: 94 из 94 пройдены. `ImageGen.Data.Tests`: 26 из 26 пройдены (включая 2 новых теста композиции). `scripts/check.sh` — пройден.
- Документация не менялась (раздел «Состояние кода» в логе не трогал).
- `scripts/release-task.sh 011` без `--publish` очистил `assignee` и `claimed` в локальном файле задачи (так работает скрипт), поэтому в файле они пустые, а не `claude-qnrqyt` и дата. Статус `done` сохранён.

## Комментарии
