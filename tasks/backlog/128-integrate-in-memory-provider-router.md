---
id: 128
status: approved
assignee:
claimed:
created: 2026-10-29
---

# Подключить in-memory queued adapter в ProviderRouter

## Описание
После 102 adapter существует отдельно. ProviderRouter должен создавать один composite connection на provider id: catalog/chat/key делегируются OpenRouter/OpenAI-compatible client, queued images подготавливаются отдельным свойством adapter. Fal остаётся native queued, Anthropic chat-only. GenerationService пока не переключается; это 129.

Зависит от: 102.
Среда: Linux или Windows.

## Критерии готовности
- OpenRouter и каждый OpenAI-compatible connection реализуют только текущие IModelProvider/IAssistantChatClient; queued adapter доступен как internal `QueuedImages: IQueuedImageProvider`. Wrapper в128 НЕ реализует IQueuedImageProvider: иначе текущий GenerationService немедленно переключит OpenRouter на queued path до129.
- Один cached connection/adapter на provider id; тестовые Submit/Poll/Cancel через QueuedImages видят одинаковый job dictionary. Production-dispatch до129 по-прежнему GenerateAsync.
- Catalog/chat/key behavior и текущий temporary GenerateAsync delegation не меняются до 129.
- Fal/Anthropic types unchanged.
- Router tests cover two configured compatible ids, stable instances and interface matrix.

## Анализ
ProviderRouter уже caches IProviderConnection. Нужен `InMemoryQueuedImageConnection` wrapper вокруг client, который делегирует current interfaces и owns adapter. До 129 IModelProvider наследует IImageGenerator, поэтому wrapper временно делегирует GenerateAsync; это удаляется 129.

Нужность: отдельный маленький шаг wiring проверяет caching/catalog/chat и подготовку adapter без одновременного удаления orchestration branch. Контракт staging явный: queued capability включается ТОЛЬКО в129. Пересечение093 по OpenRouter transport tests учитывается rebase; transport HTTP эта задача не меняет.

## План
Для Haiku/Luna: только wrapper/router/tests; GenerationService/interfaces не менять.

1. Создать public sealed `InMemoryQueuedImageConnection : IModelProvider, IAssistantChatClient`. Constructor принимает `IModelProvider imageProvider`, `IAssistantChatClient chatClient`, `string providerId`, `TimeProvider timeProvider`; router передаёт один и тот же concrete inner client для первых двух. `internal IQueuedImageProvider QueuedImages { get; }` создаётся один раз через InMemoryQueuedImageProvider(imageProvider,providerId,timeProvider). Нельзя реализовывать IQueuedImageProvider, не добавлять явные queued методы wrapper в128.
2. Делегировать CheckKeyAsync и image catalog в imageProvider; chat catalog/completion в chatClient; временный GenerateAsync в imageProvider. Queued methods тестировать напрямую через internal QueuedImages (Core.Tests имеет InternalsVisibleTo), production GenerationService не менять.
3. ProviderRouter Create: wrap OpenRouter/OpenAiCompatible; leave Fal direct and Anthropic direct. Existing ConcurrentDictionary remains one instance/id.
4. Tests interface matrix: OpenRouter/OpenAi-compatible →wrapper без queued interface; Fal→native queued; Anthropic→chat only. Повторный и concurrent GetConnection одного id возвращают один cached instance. Два configured compatible id изолируют local handle dictionaries; wrong provider handle отклоняется. Проверить queued result через QueuedImages и temporary GenerateAsync delegation, catalog/chat/key regressions. Existing Core ProviderRouterTests с IsType<OpenRouterClient/OpenAiCompatibleClient> заменить на wrapper contract; Data.Tests/CompositionTests тоже содержит два IsType<OpenRouterClient>, обновить их на wrapper+Assert.Same image/chat.
5. ProviderId handle равен config id и secrets по-прежнему разрешает inner client со своим id. Regression GenerationService с real router и OpenRouter test handler до129 проходит normal GenerateAsync путь (GetConnection не IQueuedImageProvider), не создаёт JobJson; queued поведение128 не протекает в application.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Providers/InMemoryQueuedImageConnection.cs` (новый), `ImageGen/ImageGen.Core/Providers/ProviderRouter.cs`, `ImageGen/ImageGen.Core.Tests/Providers/ProviderRouterTests.cs`, `ImageGen/ImageGen.Core.Tests/Providers/InMemoryQueuedImageConnectionTests.cs` (новый), `ImageGen/ImageGen.Core.Tests/Services/GenerationServiceTests.cs`, `ImageGen/ImageGen.Data.Tests/CompositionTests.cs`

После проверок заполнить «Отчёт» (изменения, проверки, непроверенное), выполнить `scripts/release-task.sh 128`, затем `bash scripts/check.sh`; отправить PR по `docs/tasks.md`.

## Отчёт

## Комментарии
