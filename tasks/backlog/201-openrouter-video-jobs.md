---
id: 201
status: approved
assignee:
claimed:
created: 2026-11-06
---

# OpenRouter: задания генерации видео (отправка, опрос, скачивание)

## Описание
ADR 0027, пп. 1, 3, 4. Клиент заданий видео OpenRouter: отправка `POST /api/v1/videos`, опрос `GET polling_url`, потоковое скачивание `GET /api/v1/videos/{id}/content?index=0`. Подключение к `GenerationService` — задача 202.

Зависит от: 199, 200.
Среда: Linux или Windows.

## Критерии готовности
- `VideoGenerationRequest` (record): `ModelId`, `Prompt`, `int DurationSeconds`, `string? Resolution`, `string? AspectRatio`, `bool GenerateAudio`, `long? Seed`, `ImageReference? FirstFrame` (байты + тип картинки; найти существующий тип входной картинки у `ImageGenerationRequest`, иначе новый record `(byte[] Content, string MediaType)`).
- `IQueuedVideoProvider` в `ImageGen.Core/Providers/`: `SubmitAsync(VideoGenerationRequest, ct)` → `ProviderJobHandle`; `PollAsync(handle, ct)` → `VideoJobUpdate(JobState State, decimal? CostUsd, ErrorKind? ErrorKind, string? ErrorMessage)`; `OpenContentAsync(handle, ct)` → `ProviderContent` (новый `sealed class : IAsyncDisposable` с `Stream Content`, `string MediaType`, владеющий `HttpResponseMessage`). Лимит размера считает хранилище (`SaveMediaStreamAsync`, 199), а не клиент.
- `OpenRouterClient : IQueuedVideoProvider`:
  - тело `POST videos`: `model`, `prompt`, `duration`, `resolution`/`aspect_ratio` (если заданы), `generate_audio`, `seed` (если задан), `frame_images: [{type:"image_url", image_url:{url:"data:<type>;base64,..."}, frame_type:"first_frame"}]` при `FirstFrame` (точный формат элемента `image_url` сверить с документацией OpenRouter «Video Generation»; при расхождении — по документации, записать в «Отчёт»);
  - ответ → `ProviderJobHandle(ProviderId, RequestId = id, StatusUrl = polling_url, ResponseUrl = "<base>/videos/{id}/content?index=0", CancelUrl = "")`; оба URL проверяются: хост — хост `BaseUrl` провайдера, схема `https` (иначе `ProviderException(ErrorKind.Unknown)`);
  - опрос: `pending` → `Queued`; `in_progress` → `Running`; `completed` → `Succeeded` с `usage.cost` (если число); `failed` → `Failed` (`error.message`, вид ошибки по `error.code`, как в `OpenRouterClient` для картинок — найти общий маппер); `cancelled`/`expired` → `Failed` с `ErrorKind.Unknown` и текстом статуса; неизвестный → `Queued`;
  - скачивание: запрос с авторизацией и `HttpCompletionOption.ResponseHeadersRead`, неуспешный код → `ProviderException` по общему маппингу; `Content` — поток тела ответа без чтения в память; тип — из `Content-Type`, при отсутствии — `video/mp4`. Если `Content-Length` больше `ProviderResponseLimits.Video` — исключение до чтения. Тело ответа не логируется.
- Отмена: метода у интерфейса нет (у OpenRouter нет отмены, ADR 0027 п. 4).
- Тесты с подставным `HttpMessageHandler` на всё перечисленное.

## Анализ
Образцы: `OpenRouterClient` (картинки, `ProviderClientBase`: `CreateAuthorizedRequestAsync`, `SendJsonAsync`, лимиты ответов `ProviderResponseLimits`), `FalImageProvider` (очередь: `SubmitAsync`/`PollAsync`, проверка URL-адресов `FalUrlValidator`). Тесты OpenRouter — `ImageGen/ImageGen.Core.Tests/OpenRouter/OpenRouterClientTests.cs` (как там подменяется сеть). Лимит JSON — `ProviderResponseLimits.Json`; для скачивания — свой потоковый путь без чтения в память (не через `SendJsonAsync`).

Внимание: `ProviderRouter.Create` оборачивает `OpenRouterClient` в `InMemoryQueuedImageConnection` (`Wrap`), поэтому `GetConnection(id) as IQueuedVideoProvider` вернёт `null`. Обёртка уже пробрасывает `ITextEmbeddingClient` (поле `_textEmbeddingClient`) — пробросить `IQueuedVideoProvider` и `IVideoModelProvider` (200) так же, с тестом в `ProviderRouterTests`.

## План
1. Падающие тесты (`OpenRouterVideoJobsTests`, `ProviderRouterTests`: `GetQueuedVideoProvider("openrouter")` не `null`):
   - `SubmitAsync` → тело запроса содержит ровно ожидаемые поля (без `seed`, если не задан; с `frame_images` при первом кадре), handle с `id` и `polling_url`;
   - `polling_url` с чужим хостом → `ProviderException`;
   - `PollAsync`: четыре статуса и неизвестный (см. «Критерии»), `usage.cost` 0.4 → `CostUsd` 0.4;
   - `OpenContentAsync`: тело 2 КБ с `Content-Type: video/mp4` → из `Content` читается 2 КБ, тип `video/mp4`; `Content-Length` больше лимита → исключение; запрос содержит `Authorization`; `DisposeAsync` освобождает ответ.
2. Реализация.

Файлы: `ImageGen/ImageGen.Core/Providers/VideoGenerationRequest.cs`, `ImageGen/ImageGen.Core/Providers/VideoJobUpdate.cs`, `ImageGen/ImageGen.Core/Providers/ProviderContent.cs`, `ImageGen/ImageGen.Core/Providers/IQueuedVideoProvider.cs`, `ImageGen/ImageGen.Core/OpenRouter/OpenRouterClient.cs`, `ImageGen/ImageGen.Core/Providers/ProviderRouter.cs` и `IProviderRouter.cs` (`GetQueuedVideoProvider(providerId)`), `ImageGen/ImageGen.Core/Providers/InMemoryQueuedImageConnection.cs`, `ImageGen/ImageGen.Core.Tests/Providers/ProviderRouterTests.cs`, `ImageGen/ImageGen.Core.Tests/OpenRouter/OpenRouterVideoJobsTests.cs` (новый).

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

### Порядок исполнения (для Luna)
- Сначала прочитай только файлы из строки «Файлы» и названные тесты; сигнатуры сверяй с текущим `main`.
- Шаги строго по порядку (ub-mode): падающий тест → запуск, убедиться, что он падает именно по описанной причине → минимальный фикс → тест зелёный.
- Не меняй файлы вне строки «Файлы», не переименовывай существующие члены сверх плана, не чини соседние находки — они в отдельных задачах.
- Если код расходится с планом (нет такого метода, другая сигнатура, тест уже есть), не импровизируй: остановись и запиши вопрос в «Комментарии».
- Команды `dotnet` выполняй по одной, не параллельно. После каждого изменённого проекта — `dotnet format <проект> --verify-no-changes`.
- В «Отчёте»: что изменено, команды и результаты, отличия от плана.

## Отчёт

## Комментарии

### 2026-11-06, claude-audit
Создано по поручению владельца («распланируй звук и видео»; выбрано: всё через OpenRouter, LibVLC в Avalonia сразу). ADR 0027, исследование — `docs/knowledge/media-generation.md`. План для Luna, ждёт одобрения.

### 2026-11-06, владелец (в чате, записал claude-audit)
План одобрен: «заапрувь все таски которые будут».
