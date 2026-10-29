---
id: 129
status: approved
assignee:
claimed:
created: 2026-10-29
---

# Перевести GenerationService на единый queued path

## Описание
После 128 все image providers доступны как IQueuedImageProvider. Удалить normal orchestration branch; synchronous OpenRouter/OpenAI-compatible идут через in-memory handle/poll. Разделить catalog interface и synchronous transport contract; architecture привести к ADR 0019.

Зависит от: 128.
Среда: Linux или Windows.

## Критерии готовности
- GenerationService имеет один submit→persist handle→poll→terminal path для Fal/OpenRouter/OpenAI-compatible; normal GenerateAndStore removed.
- IModelProvider только catalog/key; old IImageGenerator renamed `ISynchronousImageTransport` and used only inside adapter/wrapper, never orchestration.
- Submit handle persistence failure best-effort cancels adapter/native job using mechanisms 121/113/122.
- New process polling local handle → Failed/Interrupted; Fal persisted handle resumes.
- Per-provider limit solely GenerationService; adapter has no second semaphore.
- Existing provider/catalog/chat/generation tests updated and green; architecture current layout/flow updated without task progress.

## Анализ
Chosen architecture A: common in-memory adapter, not preserving a second normal path. OpenRouter and OpenAI-compatible clients implement IModelProvider, IAssistantChatClient, ISynchronousImageTransport; wrapper exposes queued adapter. Fal implements IModelProvider+IQueuedImageProvider. Unknown image provider terminal ModelUnavailable.

## План
Для Haiku/Luna: change interfaces first with compiler-guided tests, then orchestration. Не менять public product behavior кроме lifecycle/status timing.

1. Add `ISynchronousImageTransport` with old GenerateAsync; change OpenRouter/OpenAiCompatible and adapter/wrapper. Remove IImageGenerator file after grep zero references. IModelProvider no longer inherits transport.
2. Wrapper 128 removes temporary GenerateAsync delegation; constructor requires inner catalog/chat/transport capabilities explicitly.
3. Добавить `IQueuedImageProvider GetQueuedImageProvider(string providerId)` в IProviderRouter. ProviderRouter вызывает cached `GetConnection(providerId)`, casts и иначе бросает `ProviderException(ModelUnavailable)`; GenerationService использует метод для fresh/recovery.
4. GenerationService: remove QueuedProviderOf distinction, ExecuteAsync normal branch and GenerateAndStoreAsync. Every generation gets IQueuedImageProvider, slot, submit, persisted JobJson, watch.
5. Handle persistence failure: call CancelAsync best effort before supervisor; no second submit. Apply 121 safe logging and 122 cleanup semantics.
6. Tests all built-in/config kinds, status sequence, no direct transport call outside adapter, Fal recovery, local handle restart Interrupted, cancellation, provider limit. Отдельно unknown/deconfigured provider и Anthropic chat-only через GetQueuedImageProvider → ModelUnavailable; generation terminal Failed/ModelUnavailable. Сохранить SubmitSettled invariant 111 для cancellation до submit.
7. Architecture current Provider interfaces/services and run flow update per ADR0019; do not duplicate detailed recovery/concurrency docs reserved 119/120.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Providers/ISynchronousImageTransport.cs` (новый), `ImageGen/ImageGen.Core/Providers/IImageGenerator.cs` (удалить), `ImageGen/ImageGen.Core/Providers/IModelProvider.cs`, `ImageGen/ImageGen.Core/Providers/IProviderRouter.cs`, `ImageGen/ImageGen.Core/Providers/ProviderRouter.cs`, `ImageGen/ImageGen.Core/Providers/InMemoryQueuedImageProvider.cs`, `ImageGen/ImageGen.Core/Providers/InMemoryQueuedImageConnection.cs`, `ImageGen/ImageGen.Core/OpenRouter/OpenRouterClient.cs`, `ImageGen/ImageGen.Core/OpenAiCompatible/OpenAiCompatibleClient.cs`, `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `ImageGen/ImageGen.Core.Tests/Services/GenerationServiceTests.cs`, `ImageGen/ImageGen.Core.Tests/Providers/ProviderRouterTests.cs`, `ImageGen/ImageGen.Core.Tests/Providers/InMemoryQueuedImageProviderTests.cs`, `ImageGen/ImageGen.Core.Tests/Providers/InMemoryQueuedImageConnectionTests.cs`, `ImageGen/ImageGen.Core.Tests/OpenRouter/OpenRouterClientTests.cs`, `ImageGen/ImageGen.Core.Tests/OpenAiCompatible/OpenAiCompatibleClientTests.cs`, `docs/architecture.md`

## Отчёт

## Комментарии
