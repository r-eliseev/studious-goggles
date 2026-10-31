---
id: 156
status: todo
assignee:
claimed:
created: 2026-10-31
---

# Выбор модели ассистента только из OpenRouter

## Описание
Найдено при аудите 2026-10-31 (`docs/log/2026-10-31-completed-tasks-audit.md`). Документация и код расходятся, какая сторона права, неизвестно: сначала нужен ответ владельца.

- Документация: ADR 0009, п. 3 и 6 — `openAiCompatible` (картинки и чат) и `anthropicCompatible` (только чат) подключаются через `providers.json`; `docs/architecture.md`, «Внешние системы» — «Другие OpenAI-compatible API (`chat/completions`) и Anthropic Messages API (чат) подключаются через `providers.json`». Правило слоёв (`docs/architecture.md`, «Правила слоёв») — код вне `Providers/` и клиентов провайдеров не знает, какой провайдер отвечает ассистенту.
- Ядро это поддерживает: `ChatModelCatalogService.ListAssistantModelsAsync(providerId, …)`, `AssistantService` берёт клиент по `session.Settings.AssistantModel.ProviderId` (`ProviderRouter.GetChatClient`).
- Интерфейс — нет: `ImageGen/ImageGen.Presentation/PageModels/SessionModelsPageModel.cs:226-230` запрашивает модели ассистента только у `ProviderIds.OpenRouter` и строит `ModelRef(ProviderIds.OpenRouter, …)`. Чат-модель провайдера из `providers.json` выбрать на экране моделей сессии нельзя.
- Together по решению владельца не предлагается как модель ассистента (ADR 0017, `docs/architecture.md`); это ограничение нужно сохранить.

Зависит от: —
Среда: Linux или Windows (логика в `Presentation`, тесты на Linux).

## Критерии готовности
- Есть решение владельца: вкладка «Assistant model» показывает чат-модели всех настроенных провайдеров, поддерживающих чат (кроме image-only, например Together), либо остаётся только OpenRouter.
- Если всех провайдеров: модели сгруппированы и подписаны провайдером, как картинки (S5.5, S5.7 `docs/specs/chat-sessions.md`); сохраняется пара provider id + model id; провайдер без ключа или с ошибкой каталога не ломает список остальных.
- Если только OpenRouter: ADR 0009 и `docs/architecture.md` явно говорят, что чат-провайдеры из конфига в интерфейсе не выбираются (новый ADR или пометка в существующем).
- Тесты `Presentation` на состав списка моделей ассистента; поведение одинаково в MAUI и Avalonia.

## Анализ

## План

## Отчёт

## Комментарии
### 2026-10-31, claude-audit
Вопрос владельцу: показывать ли на вкладке «Assistant model» чат-модели всех настроенных chat-провайдеров (`openAiCompatible`, `anthropicCompatible`) или выбор ассистента намеренно только из OpenRouter? До ответа задача не планируется. Вопрос также записан в `docs/status.md`, «Открытые вопросы».
### 2026-10-31, владелец (в чате, записал claude-audit)
Ответ на вопрос: показывать чат-модели **всех** настроенных chat-провайдеров. Действует первый вариант критериев готовности; вариант «только OpenRouter» снят. Image-only провайдеры (Together) по-прежнему не предлагаются (ADR 0017). Задача остаётся `todo`: нужны «Анализ» и «План».
