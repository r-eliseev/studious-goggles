---
id: 011
status: planned
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

## Комментарии
