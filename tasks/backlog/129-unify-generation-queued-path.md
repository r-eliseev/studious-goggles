---
id: 129
status: done
assignee:
claimed:
created: 2026-10-29
---

# Перевести GenerationService на единый queued path

## Описание
После128 все synchronous image providers имеют подготовленный adapter; именно129 включает IQueuedImageProvider на wrapper и единый dispatch атомарно. Удалить normal orchestration branch; synchronous OpenRouter/OpenAI-compatible идут через in-memory handle/poll. Разделить catalog interface и synchronous transport contract; architecture привести к ADR задачи130 (использовать фактический номер нового ADR).

Зависит от: 128, 125.
Среда: Linux или Windows.

## Критерии готовности
- GenerationService имеет один submit→persist handle→poll→terminal path для Fal/OpenRouter/OpenAI-compatible; normal GenerateAndStore removed.
- IModelProvider только catalog/key; old IImageGenerator renamed `ISynchronousImageTransport` and used only inside adapter/wrapper, never orchestration.
- Submit handle persistence failure best-effort cancels adapter/native job через best-effort CancelAsync121 и supervisor113 после retries125.122 не предпосылка: при handle persistence картинки ещё нет, compensation terminal file добавит122 после129.
- New process polling local handle → Failed/Interrupted; Fal persisted handle resumes.
- Per-provider limit solely GenerationService; adapter has no second semaphore.
- Existing provider/catalog/chat/generation tests updated and green; architecture current layout/flow updated without task progress.

## Анализ
Chosen architecture A: common in-memory adapter, not preserving a second normal path. OpenRouter and OpenAI-compatible clients implement IModelProvider, IAssistantChatClient, ISynchronousImageTransport; wrapper exposes queued adapter. Fal implements IModelProvider+IQueuedImageProvider. Unknown image provider terminal ModelUnavailable.

Нужность: завершает переход от двух orchestration branches к одному, сохраняя transport-specific HTTP в clients.125 — обязательная функциональная предпосылка:129 использует strict persistence/retry/supervisor и lifetime semaphore111. Пересечения093 по OpenRouterClient/tests и089 по CreateRunAsync учитывать через rebase; не откатывать цену/событие обновления списка сессий.

## План
Для Haiku/Luna: change interfaces first with compiler-guided tests, then orchestration. Не менять public product behavior кроме lifecycle/status timing.

1. Добавить `ISynchronousImageTransport` с прежним `Task<ImageGenerationResult> GenerateAsync(ImageGenerationRequest, CancellationToken)`; IModelProvider больше не наследует IImageGenerator, содержит только IProviderConnection+catalog. OpenRouterClient/OpenAiCompatibleClient реализуют ISynchronousImageTransport явно в списке interfaces; методы HTTP не переписывать. Переименовать transport в adapter102. У Fal удалить synchronous GenerateAsync и его unused polling constants: native Submit/Poll/Cancel остаются. Перенести два FalImageProviderTests GenerateAsync_* сценария на SubmitAsync→PollAsync и проверки terminal JobUpdate, не удалять покрытие изображения/модерации.
2. `InMemoryQueuedImageConnection` начинает реализовывать IQueuedImageProvider и делегирует методы существующему QueuedImages. Удалить temporary GenerateAsync. Constructor теперь принимает отдельные `IModelProvider imageProvider`, `IAssistantChatClient chatClient`, `ISynchronousImageTransport transport`, `string providerId`, `TimeProvider`; router передаёт один inner client для трёх capability arguments. Сам wrapper не реализует ISynchronousImageTransport: synchronous transport наружу не нужен.
3. Добавить `IQueuedImageProvider GetQueuedImageProvider(string providerId)` в IProviderRouter. Реализация использует cached GetConnection, cast либо ProviderException(ModelUnavailable). Неизвестный id или chat-only Anthropic дают Failed/ModelUnavailable для generation; ловить именно provider lookup failure до submit, сохранить CompletedAt/event/persistence. Catalog GetImageProvider остаётся прежним и не требует GenerateAsync.
4. GenerationService: удалить QueuedProviderOf, normal Execute branch и GenerateAndStoreAsync. Все jobs проходят общий provider slot111 и Submit→PublishHandle121→persist JobJson125→Watch→terminal. Recovery использует тот же GetQueuedImageProvider, registration/lease108, сохранённый handle без submit. Успешный JobUpdate.Result сохраняет Content/MediaType И CostUsd: старый queued путь стоимость не переносил, теперь это сломало бы OpenRouter usage.cost. Сохранить parameter outcomes/references/seed и очередь событий/статусов. SubmitSettled завершается при любом исходе, в том числе cancelled slot wait.
5. Если JobJson UPDATE не сохранился после retries125, использовать опубликованный in-memory handle для best-effort CancelAsync с отдельным token, safe logging121 и без повторного submit. Затем исходный persistence exception →supervisor113; false terminal event не публиковать. Чтобы Delete и эта cleanup не послали cancel дважды, добавить private RunningJob `CancelOnceAsync` с cached cancellation Task под его lock; Stop121 и persistence-failure path используют один helper. Новый file ещё не получен,122 здесь не вызывать/не предвосхищать.
6. Выбрать интервалы через реальный wrapper type `InMemoryQueuedImageConnection`: min50ms/max250ms; native fal min1s/max5s по ADR130. Не распознавать local по строке provider id/URL и не увеличивать задержку всего OpenRouter на1s. Первый Poll идёт сразу; network retry fal не менять. Recovery local handle после restart →adapter Failed/Interrupted без paid submit; fal handle снова poll. XML-doc/count RecoverInterrupted сохраняют106 semantics.
7. Обновить doubles/compiler failures системно: rg всех IImageGenerator/GenerateAsync/IModelProvider references в ImageGen перед удалением старого файла. Core GenerationServiceTests и AssistantServiceTests теперь настраивают GetQueuedImageProvider и управляемые queued snapshots/adapter, catalog tests настраивают IModelProvider только для catalog. В Presentation.Tests/TestDoubles/ProviderDoubles.Router добавить optional IQueuedImageProvider queued и настройку нового router member; page model tests, запускающие генерацию, передают готовый queued fake. Не оставлять default NSubstitute null/task без результата. Остальные tests с catalog-only substitutes не расширять ненужным GenerateAsync.
8. Regression tests: все built-in/config kinds; native/local generation result+CostUsd; no direct transport invocation вне adapter; статус/terminal event ровно один; Fal recovery; новый local adapter со старым handle →Interrupted; missing/chat-only provider; cancel и per-provider limits; handle persistence failure cancel once; совместная гонка Delete+persistence failure cancel once; old normal-path methods/reference count ноль. Дождаться всех Task через TCS/WhenIdle, без sleeps.
9. Обновить `docs/architecture.md` interfaces/current flow/Первый запуск и S3.8/описание параллельного прогона в `docs/specs/chat-sessions.md` согласованно с106/111: один queued path для всех image providers, память local handles, fal resume, no repeated paid submit, фактические per-provider limits.119/120 отменены и не являются владельцами этих абзацев. Старый IImageGenerator.cs удалить только после rg zero references; сборкой проверить ВСЕ четыре test projects и Avalonia, не только Core.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Providers/ISynchronousImageTransport.cs` (новый), `ImageGen/ImageGen.Core/Providers/IImageGenerator.cs` (удалить), `ImageGen/ImageGen.Core/Providers/IModelProvider.cs`, `ImageGen/ImageGen.Core/Providers/IProviderRouter.cs`, `ImageGen/ImageGen.Core/Providers/ProviderRouter.cs`, `ImageGen/ImageGen.Core/Providers/InMemoryQueuedImageProvider.cs`, `ImageGen/ImageGen.Core/Providers/InMemoryQueuedImageConnection.cs`, `ImageGen/ImageGen.Core/OpenRouter/OpenRouterClient.cs`, `ImageGen/ImageGen.Core/OpenAiCompatible/OpenAiCompatibleClient.cs`, `ImageGen/ImageGen.Core/Fal/FalImageProvider.cs`, `ImageGen/ImageGen.Core.Tests/Fal/FalImageProviderTests.cs`, `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `ImageGen/ImageGen.Core.Tests/Services/GenerationServiceTests.cs`, `ImageGen/ImageGen.Core.Tests/Providers/ProviderRouterTests.cs`, `ImageGen/ImageGen.Core.Tests/Providers/InMemoryQueuedImageProviderTests.cs`, `ImageGen/ImageGen.Core.Tests/Providers/InMemoryQueuedImageConnectionTests.cs`, `ImageGen/ImageGen.Core.Tests/OpenRouter/OpenRouterClientTests.cs`, `ImageGen/ImageGen.Core.Tests/OpenAiCompatible/OpenAiCompatibleClientTests.cs`, `ImageGen/ImageGen.Core.Tests/Services/AssistantServiceTests.cs`, `ImageGen/ImageGen.Core.Tests/Services/ModelCatalogServiceTests.cs`, `ImageGen/ImageGen.Presentation.Tests/TestDoubles/ProviderDoubles.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/ChatPageModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/ModelPickerPageModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/SessionModelsPageModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/SessionParametersPageModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/Services/AppInitializerTests.cs`, `ImageGen/ImageGen.Data.Tests/CompositionTests.cs`, `docs/architecture.md`, `docs/specs/chat-sessions.md`. После rg включить дополнительные реальные call sites в отчёт; перечень не разрешает оставить compiler errors в неназванном файле.

После проверок заполнить «Отчёт» (изменения, проверки, непроверенное), выполнить `scripts/release-task.sh 129`, затем `bash scripts/check.sh`; отправить PR по `docs/tasks.md`.

## Отчёт

- Переведены Fal, OpenRouter и OpenAI-compatible generations на один queued dispatch в `GenerationService`. Вынесен `ISynchronousImageTransport`, `IModelProvider` оставлен для каталога/ключа; OpenRouter и OpenAI-compatible клиенты адаптируются в queued providers, Fal оставлен native queued provider.
- Удалён старый `IImageGenerator` и synchronous generation path Fal; обновлены router, Data composition, Core/Presentation doubles и тесты. Для сохранённого handle добавлена компенсационная best-effort отмена после ошибки записи. Успешный результат сохраняет стоимость. Локальные handles при восстановлении не запускают повторную генерацию; Fal продолжает polling.
- Обновлены `docs/architecture.md` и `docs/specs/chat-sessions.md` про общий lifecycle, память локального handle и восстановление.
- Отмена локального job ждёт терминальное состояние адаптера перед удалением сессии; повторные queued/running polling updates не отправляются как дубли.
- Проверки: Core 564/564; Data 95/95; Presentation 366/366; Avalonia 141 passed, 2 skipped из-за отсутствующей Windows DPAPI и D-Bus Secret Service; Avalonia build без предупреждений. `dotnet format --verify-no-changes --no-restore` прошёл для Core, Core.Tests, Data.Tests и Presentation.Tests.
- Добавлены тесты: после исчерпания retries записанный handle отменяется без polling; гонка этой отмены с удалением сессии отправляет один remote cancel.
- Вне Linux-проверки: живые ответы провайдеров и платформенные Secret Service/DPAPI проверки.

## Комментарии
