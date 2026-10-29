---
id: 107
status: approved
assignee:
claimed:
created: 2026-10-29
---

# Применить timeout провайдера ко всем запросам fal

## Описание
Найдено ревью 2026-10-29. Общий `HttpClient.Timeout` бесконечный. Base JSON path применяет `Config.Timeout`, но fal catalog/status/result/download/cancel вызывают HTTP напрямую без deadline. Зависший endpoint оставляет generation незавершённой.

Зависит от: 106.
Среда: Linux или Windows.

## Критерии готовности
- Один `Config.Timeout` ограничивает submit, catalog, status, result, image download и cancel, включая чтение body.
- Внутренний deadline → `ProviderException(ErrorKind.Timeout)`; отменённый caller token остаётся `OperationCanceledException`.
- Нет второго timeout value в `FalImageProvider`.
- Все hanging-handler tests выполняются с коротким timeout, без ожидания 180 секунд.

## Анализ
`ProviderClientBase.CreateTimeout` private. `SendFalJsonAsync`, `DownloadAsync`, `CancelAsync` обходят его. `SendFalJsonAsync` обслуживает также catalog. Timeout ProviderException из polling получает terminal state только после 106.

Пересечения: 109 и 112 меняют те же send sites. Выполнять 107 до них.

## План
Для Haiku/Luna: переиспользовать один base helper, не задавать `HttpClient.Timeout`. Один linked CTS должен жить до конца send+body read и dispose после операции.

Прочитай: `ProviderClientBase`, все HTTP methods `FalImageProvider`, `FalImageProviderTests`, existing timeout test `OpenAiCompatibleClientTests`.

1. Сделать `ProviderClientBase.CreateTimeout` protected; не делать public. Сохранить filter `!callerToken.IsCancellationRequested`.
2. Добавить overload test helper `CreateProvider(handler, timeout)` с обязательным timeout argument для timeout tests и cancellation-aware handler (`Task.Delay(Timeout.Infinite, token)`).
3. Падающие timeout tests: submit; catalog; status; result после COMPLETED; image download; cancel. Для result/download зависать на точной фазе. Каждый даёт `ProviderException(Timeout)`.
4. External-cancellation tests минимум для direct JSON path и body-read path: заранее/во время операции отменён caller token → `OperationCanceledException`.
5. Применить base helper в `SendFalJsonAsync`, `DownloadAsync`, `CancelAsync`; timeout охватывает `SendAsync` и чтение content. Submit оставить на существующем base `SendJsonAsync`.
6. `LoggingHttpHandler` не менять; его Debug/debug-trace regression tests остаются задачам 112/124.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Providers/ProviderClientBase.cs`, `ImageGen/ImageGen.Core/Fal/FalImageProvider.cs`, `ImageGen/ImageGen.Core.Tests/Fal/FalImageProviderTests.cs`

## Отчёт

## Комментарии
