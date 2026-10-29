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
- Один источник значения — `Config.Timeout` — ограничивает каждый отдельный HTTP запрос: submit, probe, catalog (каждую страницу), status, result, image download и cancel, включая чтение body. Это deadline на один send+read, а не общий бюджет всего job или цепочки status → result → download.
- Внутренний deadline → `ProviderException(ErrorKind.Timeout)`; отменённый caller token остаётся `OperationCanceledException`.
- Нет второго timeout value в `FalImageProvider`.
- Все hanging-handler tests выполняются с коротким timeout, без ожидания 180 секунд.

## Анализ
`ProviderClientBase.CreateTimeout` private. `SendFalJsonAsync`, `DownloadAsync`, `CancelAsync` обходят его. `SendFalJsonAsync` обслуживает также catalog. Timeout ProviderException из polling получает terminal state только после 106.

Нужность: без deadline fal может навсегда удержать job и блокировать остановку сессии.

Пересечения: 109 и 112 меняют те же send sites. Выполнять 107 до них. С 108 можно работать параллельно: она меняет lifecycle GenerationService, а не HTTP client; после rebase её owned token должен пройти через новые linked CTS.

## План
Для Haiku/Luna: переиспользовать один base helper, не задавать `HttpClient.Timeout`. Один linked CTS должен жить до конца send+body read и dispose после операции.

Прочитай: `ProviderClientBase`, все HTTP methods `FalImageProvider`, `FalImageProviderTests`, existing timeout test `OpenAiCompatibleClientTests`.

1. Сделать `ProviderClientBase.CreateTimeout` protected; не делать public. Сохранить filter `!callerToken.IsCancellationRequested`.
2. Проверить существующий `ProbeKeyAsync`: timeout уже есть; сохранить его контракт `ApiKeyStatus.NetworkError` при внутреннем deadline и проброс caller cancellation. Добавить regression tests для probe; не менять контракт проверки ключа на ProviderException. Добавить overload test helper `CreateProvider(handler, timeout)` с обязательным timeout argument для timeout tests и cancellation-aware handler (`Task.Delay(Timeout.Infinite, token)`).
3. Падающие timeout tests: submit; catalog; status; result после COMPLETED; image download; cancel. Для result/download зависать на точной фазе. Каждый даёт `ProviderException(Timeout)`.
4. External-cancellation tests для direct JSON, download и cancel: заранее/во время операции отменён caller token → `OperationCanceledException`. Отдельный body-read тест: handler сразу возвращает headers, а custom `HttpContent` блокирует чтение с переданным токеном; после deadline → Timeout, при caller cancellation → OCE. В тестах использовать `TaskCompletionSource` для достижения нужной фазы и короткий Config.Timeout; watchdog теста только предотвращает зависание, не является проверяемым таймаутом.
5. Применить base helper в `SendFalJsonAsync`, `DownloadAsync`, `CancelAsync`; timeout охватывает `SendAsync` и чтение content. Весь send+read в try с фильтром `!cancellationToken.IsCancellationRequested`; `HttpRequestException` по-прежнему Network. Submit оставить на существующем base `SendJsonAsync`. Для JSON и download dispose response/request при success и ошибках; cancel также освобождает response. Не вводить общий CTS на всё время PollAsync, иначе один deadline ошибочно суммирует status, result и download.
6. `LoggingHttpHandler` не менять; его Debug/debug-trace regression tests остаются задачам 112/124.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Providers/ProviderClientBase.cs`, `ImageGen/ImageGen.Core/Fal/FalImageProvider.cs`, `ImageGen/ImageGen.Core.Tests/Fal/FalImageProviderTests.cs`

После проверок заполнить «Отчёт» (изменения, проверки, непроверенное), выполнить `scripts/release-task.sh 107`, затем `bash scripts/check.sh`; код отправить PR по `docs/tasks.md`.

## Отчёт

## Комментарии
