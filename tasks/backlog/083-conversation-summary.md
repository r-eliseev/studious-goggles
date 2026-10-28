---
id: 083
status: approved
assignee:
claimed:
created: 2026-10-28
---

# Контекст ассистента: сводка старой части истории

## Описание
Вторая часть 027 (C3): когда история не помещается, модель ассистента в фоне после ответа сжимает старую часть в сводку; следующий запрос идёт со сводкой и свежими сообщениями. Одна сводка на сессию, таблица `conversation_summaries` (ADR 0015).

Спецификация: `docs/specs/assistant-context.md` (разделы C1–C4 — источник правды, числа оттуда), ADR 0015. Решения владельца — задача 027, «Комментарии».

Зависит от: 082.
Среда: Linux или Windows (ядро, хранилище и их тесты).

## Критерии готовности
- Миграция и репозиторий сводки; удаление сессии удаляет сводку.
- Сводка запускается в фоне по условию C3.2, содержание — C3.3; ошибка — C3.5; ответ ассистента её не ждёт.
- Запрос со сводкой — по C1.3 и C3.6.
- Тест-кейсы из «Плана» проходят.

## Анализ
- Миграции: `ImageGen/ImageGen.Data/Migrations/` (последняя — `0005_image_descriptions.sql`; номер берётся следующий свободный в момент реализации, `docs/tasks.md`). Список миграций — проверить, как подключается (`ImageGen.Data/Migrations.cs` или ресурс).
- Образец репозитория: `ImageDescriptionRepository` (019) — та же форма: строки с `session_id`, `cost_usd` как TEXT.
- Фоновая работа: образец `GenerationService` (`_running`, `WhenIdleAsync` для тестов) — у сводки нужен такой же `WhenIdleAsync`, иначе тесты удаляют базу, пока идёт запись (`docs/conventions.md`, «Тесты»).
- Вызов модели — тот же `IProviderRouter.GetChatClient(...)` и `CompleteChatAsync`, без инструмента.

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов и строк переноси дословно. Если шаг не получается так, как написано, не придумывай обход: опиши в «Комментариях», что пробовал, поставь `blocked` и остановись.

1. Core: `Domain/Chat/ConversationSummary.cs` (`SessionId`, `Text`, `CoversUpToMessageId`, `ModelRef Model`, `decimal? CostUsd`, `DateTimeOffset CreatedAt`), `Storage/IConversationSummaryRepository.cs` (`GetAsync(sessionId)`, `UpsertAsync(summary)`).
2. Data: миграция `NNNN_conversation_summaries.sql` — таблица по ADR 0015; `Repositories/ConversationSummaryRepository.cs` (`INSERT … ON CONFLICT(session_id) DO UPDATE`, только параметры); регистрация. Тесты `ConversationSummaryRepositoryTests`: `Upsert_Then_Get_ReturnsSame`, `Upsert_Twice_KeepsLast`, `DeleteSession_DeletesSummary`, `Migration_OnExistingDatabase_CreatesEmptyTable`.
3. Core `Context/SummaryPlanner.cs` (static, чистая): `Plan(IReadOnlyList<ChatMessage> messages, ConversationSummary? current, int historyBudget, double factor)` → `SummaryPlan?` (`record SummaryPlan(IReadOnlyList<ChatMessage> ToSummarize, Guid CoversUpToMessageId)`): условие C3.2 (80 % бюджета, минимум 10 сообщений кроме последних 20), иначе null. Тесты: `Plan_ShortHistory_Null`, `Plan_OverEightyPercent_SummarizesAllButLast20`, `Plan_FewerThan10Old_Null`, `Plan_ExistingSummary_StartsAfterCoverage`, `Plan_SkipsErrors`.
4. Core `Services/ConversationSummaryService.cs`: `RequestAsync(Guid sessionId)` — запускает задачу в фоне (не больше одной на сессию; повторный вызов во время работы ничего не делает), внутри: загрузка сессии, сообщений, текущей сводки, бюджета (как в 082), `SummaryPlanner.Plan`; если план есть — запрос к модели ассистента: системное сообщение `AssistantPrompts.SummaryInstruction` (текст по C3.3, на английском: «Summarize the conversation so far for an assistant that writes image prompts. Keep the user's goals, chosen style, rejected ideas and the latest prompt. At most 300 words. Write in the language of the conversation.»), затем прежняя сводка (если есть) и сообщения плана; результат — `UpsertAsync`. Ошибка — `LogWarning`, без повтора (C3.5). `WhenIdleAsync()` для тестов.
5. `AssistantService`: в `BuildRequestAsync` — если есть сводка: системное сообщение `"Summary of the earlier conversation: " + text` после текущего промпта; `SummaryTokens` в бюджете; в `ContextBuilder.Select` передаются только сообщения после `CoversUpToMessageId`. После сохранения ответа ассистента — `summaries.RequestAsync(sessionId)` (без `await` результата сводки; ошибка запуска не ломает ответ).
6. Тесты `AssistantServiceTests` и `ConversationSummaryServiceTests`: `Reply_LongHistory_RequestsSummaryInBackground` (после `WhenIdleAsync` сводка сохранена, ответ пришёл раньше — заглушка сводки отвечает с задержкой), `Request_WithSummary_SendsSummaryBeforeHistory`, `Request_WithSummary_SkipsCoveredMessages`, `SummaryFails_LogsWarning_ReplyNotAffected`, `SummaryRunning_SecondRequest_DoesNotStartAnother`.
7. `docs/architecture.md` — сводка и таблица; спецификация — статус «в работе» (084 доделает «реализовано»).

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data/ImageGen.Data.csproj --verify-no-changes
scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/{Domain/Chat/ConversationSummary.cs,Storage/IConversationSummaryRepository.cs,Context/SummaryPlanner.cs,Services/ConversationSummaryService.cs,Services/AssistantService.cs,Services/AssistantPrompts.cs,CoreServiceCollectionExtensions.cs}`, `ImageGen/ImageGen.Data/{Migrations/NNNN_conversation_summaries.sql,Repositories/ConversationSummaryRepository.cs,DataServiceCollectionExtensions.cs}` (и список миграций, если он отдельно), тесты, `docs/architecture.md`, `docs/specs/assistant-context.md`

## Отчёт

## Комментарии
