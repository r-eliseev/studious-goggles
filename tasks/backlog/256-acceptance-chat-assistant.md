---
id: 256
status: approved
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

## Комментарии

### 2026-11-09, claude-bdd-plan
Создано по просьбе владельца распланировать тест-сценарии приложения для исполнения GPT Luna. Сценарии и ожидания — в `docs/specs/acceptance-scenarios.md`. План ждёт одобрения.

### 2026-11-09, owner (in the chat, recorded by claude-bdd-plan)
Plan approved, status `approved`. Changes of the same day apply: the Gherkin scenarios, step texts, bindings, comments and the "Report" are in English (`docs/conventions.md`, "Language"; ADR 0031 is accepted); a "Structure of a scenario" in this plan is a `Scenario Outline`. Scenario expectations and wording are in the English catalog `docs/specs/acceptance-scenarios.md`. The acceptance project runs in CI (task 252). The flow tests in `ImageGen.Avalonia.Tests/Flows` that a scenario of this task covers are deleted by task 267, not here.
