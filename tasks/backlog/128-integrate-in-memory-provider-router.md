---
id: 128
status: approved
assignee:
claimed:
created: 2026-10-29
---

# Подключить in-memory queued adapter в ProviderRouter

## Описание
После 102 adapter существует отдельно. ProviderRouter должен создавать один composite connection на provider id: catalog/chat/key делегируются OpenRouter/OpenAI-compatible client, queued images — adapter. Fal остаётся native queued, Anthropic chat-only. GenerationService пока не переключается; это 129.

Зависит от: 102.
Среда: Linux или Windows.

## Критерии готовности
- OpenRouter и каждый OpenAI-compatible provider connection реализуют IModelProvider, IAssistantChatClient и IQueuedImageProvider.
- Один cached connection/adapter на provider id; Submit/Poll/Cancel видят одинаковый job dictionary.
- Catalog/chat/key behavior и текущий temporary GenerateAsync delegation не меняются до 129.
- Fal/Anthropic types unchanged.
- Router tests cover two configured compatible ids, stable instances and interface matrix.

## Анализ
ProviderRouter уже caches IProviderConnection. Нужен `InMemoryQueuedImageConnection` wrapper вокруг client, который делегирует current interfaces и owns adapter. До 129 IModelProvider наследует IImageGenerator, поэтому wrapper временно делегирует GenerateAsync; это удаляется 129.

## План
Для Haiku/Luna: только wrapper/router/tests; GenerationService/interfaces не менять.

1. Создать composite connection с inner object implementing IModelProvider+IAssistantChatClient и `InMemoryQueuedImageProvider` wrapping its IImageGenerator.
2. Делегировать CheckKey, image catalog, chat catalog/completion, temporary GenerateAsync; queued methods adapter.
3. ProviderRouter Create: wrap OpenRouter/OpenAiCompatible; leave Fal direct and Anthropic direct. Existing ConcurrentDictionary remains one instance/id.
4. Tests interface matrix by kind; repeated GetConnection ReferenceEquals; two ids isolated job stores; queued result delegates correct underlying transport; chat/catalog regressions.
5. Ensure ProviderId in handles equals config id and secrets still resolved inner client config.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Providers/InMemoryQueuedImageConnection.cs` (новый), `ImageGen/ImageGen.Core/Providers/ProviderRouter.cs`, `ImageGen/ImageGen.Core.Tests/Providers/ProviderRouterTests.cs`, `ImageGen/ImageGen.Core.Tests/Providers/InMemoryQueuedImageConnectionTests.cs` (новый)

## Отчёт

## Комментарии
