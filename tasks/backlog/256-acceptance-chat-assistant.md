---
id: 256
status: blocked
assignee:
claimed:
created: 2026-11-09
---

# Приёмочные сценарии: чат и ассистент

## Описание
Автоматизировать раздел «Чат и ассистент» каталога: CHAT-01 … CHAT-08. Файл `Features/ChatAssistant.feature`, тег `@chat`.

Зависит от: 253.
Среда: Linux или Windows (headless).

## Критерии готовности
- CHAT-01 … CHAT-08 в `.feature`; каждый зелёный или `@ignore @bug-NNN` с задачей.
- CHAT-02 (F9.4, без скрытой подмены): промпт из аргументов `generate_images` побайтно совпадает с промптом в блоке прогона, на карточке и в теле запроса генерации (журнал фейка). Модель в запросе — модель сессии.
- CHAT-03: во втором сообщении сессии тело chat completion содержит системный промпт (`AssistantPrompts`) и промпт последнего прогона. Проверять наличие, а не весь текст промпта.
- CHAT-04 — `Структура сценария` с примерами 500, 429, 401: в ленте отдельное сообщение об ошибке с причиной (тексты по `ErrorKindText`), сообщение пользователя осталось в ленте.
- CHAT-05: при пустом или пробельном вводе команда отправки недоступна (`SendCommand.CanExecute` или эквивалент по коду).
- CHAT-08: вложение через фейк `IFilePickerService` (PNG из ресурсов теста); в теле запроса генерации есть reference. Если согласие F10.6 требует диалога — ответ «да» через фейк диалогов. Форма поля reference — по коду клиента OpenRouter.
- Статусы CHAT-* в каталоге обновлены.

## Анализ
- `ImageGen/ImageGen.Presentation/PageModels/ChatPageModel.cs` (`SendAsync`, `Items`, `AttachImageAsync`, `PendingAttachments`), `MessageItemViewModel.cs`, `RunItemViewModel.cs` (`PromptText`).
- Ядро: `ImageGen/ImageGen.Core/Services/AssistantService.cs` (после `generate_images` ход заканчивается, второго ответа ассистента нет — уточнено в задаче 243), `AssistantPrompts.cs`, `AttachmentService.cs`.
- Потоковые тесты: `Flows/ChatFlowTests.cs`, `Flows/AssistantRetryFlowTests.cs`, `Flows/ChatKeyboardFlowTests.cs`.
- Спецификация: `docs/specs/chat-sessions.md` S2.1–S2.5; `docs/specs/image-client.md` F4.5, F9.4, F10.6.

## План
По одному сценарию CHAT-01 → CHAT-08. Для CHAT-04 — одна `Структура сценария`. Проверки тела запроса — через шаг `Тогда запрос <вид> к OpenRouter содержит …` в `CommonSteps` (поиск по JSON-пути, а не по подстроке).

Файлы: `ImageGen/ImageGen.Acceptance.Tests/Features/ChatAssistant.feature`, `Steps/ChatSteps.cs` (новый), `Steps/CommonSteps.cs`, `Support/FakeOpenRouter.cs` (только новые ответы), ресурс PNG в `ImageGen/ImageGen.Acceptance.Tests/Assets/` (новый), `docs/specs/acceptance-scenarios.md`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj -- --filter-trait "Category=chat"
dotnet format ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj --verify-no-changes
bash scripts/check.sh
```
(Фильтр по тегу — если Reqnroll передаёт теги как категории xUnit; как именно — записано в `docs/knowledge/acceptance-tests.md` задачей 252.)

### Порядок исполнения (для Luna)
- Сначала прочитай только файлы из строки «Файлы», названные тесты и разделы `docs/specs/acceptance-scenarios.md`; сигнатуры сверяй с текущим `main`.
- Шаги строго по порядку (ub-mode): сначала сценарий в `.feature`, запуск — он падает, потому что шаг не привязан или ожидание не выполнено; затем минимальная привязка; сценарий зелёный. Один сценарий — один коммит (id сценария в сообщении).
- Ожидание сценария берётся из спецификации, не из кода. Если код ведёт себя иначе: не подгоняй сценарий и не чини код — тег `@ignore @bug-NNN`, новая задача `scripts/new-task.sh --publish backlog <имя>` с шагами воспроизведения, статус «расхождение (NNN)» в каталоге.
- Шаги действуют как пользователь: команды моделей страниц, выбор пунктов, навигация. Не вызывай репозитории, сервисы ядра и SQL напрямую, кроме шагов «Дано», которые готовят мир, и проверок «файл существует».
- Новый общий шаг — в `Steps/CommonSteps.cs`; сначала поищи, нет ли уже такого шага с другой формулировкой.
- Не меняй файлы вне строки «Файлы», не переименовывай существующие члены сверх плана, не чини соседние находки — они в отдельных задачах.
- Если код расходится с планом (нет такого метода, другая сигнатура), не импровизируй: остановись и запиши вопрос в «Комментарии».
- Команды `dotnet` выполняй по одной, не параллельно. После каждого изменённого проекта — `dotnet format <проект> --verify-no-changes`.
- В «Отчёте»: какие сценарии зелёные, какие `@ignore` и с какими задачами, команды и результаты, отличия от плана. Статусы сценариев в `docs/specs/acceptance-scenarios.md` обнови.

## Отчёт

- CHAT-01 is implemented in `Features/ChatAssistant.feature`; the step bindings assert the assistant text appears in the feed and no run item is present.
- Verification: `dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj -- --filter-trait "Category=chat"` passed (1/1); `dotnet format ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj --verify-no-changes` passed.
- CHAT-02 is implemented; the scenario checks exact prompt equality in the run, generation card, and `/api/v1/images` request, and checks that the request model matches the card model. The filtered chat acceptance run passed (2/2); formatting passed.
- CHAT-03 is implemented; the second chat request is checked for both `AssistantPrompts.System` and the latest run's `CurrentPrompt` system message. The filtered chat acceptance run passed (3/3); formatting passed.
- CHAT-04 is implemented as an outline for HTTP 500, 429, and 401. Each example verifies the user message remains and an error message matches its `ErrorKindText`. The filtered chat acceptance run passed (6/6); formatting passed.
- CHAT-05 is implemented; whitespace-only input disables `SendCommand`. The filtered chat acceptance run passed (7/7); formatting passed.
- CHAT-06 is implemented; after an assistant error, retrying its message consumes the queued tool response and starts a run with the exact prompt. The filtered chat acceptance run passed (8/8); formatting passed.
- CHAT-07 is implemented and reuses the existing model-label assertion also used by SES-06. The filtered chat acceptance run passed (9/9); formatting passed.
- CHAT-08 is not implemented: the approved plan limits `FakeOpenRouter.cs` changes to new assistant responses, but a reference-capable model is needed to exercise this criterion.

## Комментарии

### 2026-11-09, claude-bdd-plan
Создано по просьбе владельца распланировать тест-сценарии приложения для исполнения GPT Luna. Сценарии и ожидания — в `docs/specs/acceptance-scenarios.md`. План ждёт одобрения.

### 2026-11-09, owner (in the chat, recorded by claude-bdd-plan)
Plan approved, status `approved`. Changes of the same day apply: the Gherkin scenarios, step texts, bindings, comments and the "Report" are in English (`docs/conventions.md`, "Language"; ADR 0031 is accepted); a "Structure of a scenario" in this plan is a `Scenario Outline`. Scenario expectations and wording are in the English catalog `docs/specs/acceptance-scenarios.md`. The acceptance project runs in CI (task 252). The flow tests in `ImageGen.Avalonia.Tests/Flows` that a scenario of this task covers are deleted by task 267, not here.

### 2026-11-11, codex-gpt6
Plan mismatch for CHAT-08: `ReferenceImageLimit.Of` only permits a generation to send references when the selected catalog model declares an `input_references` range. `ImageGen/ImageGen.Acceptance.Tests/Support/FakeOpenRouter.cs` currently serializes only `seed` in fake image model parameters, so the configured models cannot take references. The approved plan permits changes to this fake only for additional assistant responses. Proposed plan change: allow a configurable `input_references` maximum in the fake image-model catalog response, and configure a reference-capable model in CHAT-08 before attaching an image. Keep product code unchanged. The task is blocked until this file-scope change is approved.
