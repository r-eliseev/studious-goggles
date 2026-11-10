---
id: 255
status: in-progress
assignee: codex-gpt6
claimed: 2026-11-10T17:52:38Z
created: 2026-11-09
---

# Приёмочные сценарии: список сессий, название, удаление, настройки по умолчанию

## Описание
Автоматизировать раздел «Сессии» каталога `docs/specs/acceptance-scenarios.md`: SES-01 … SES-10. SES-11 (удаление всей истории) — после задачи 229, здесь не делать. Файл `Features/Sessions.feature`, тег `@sessions`.

Зависит от: 253.
Среда: Linux или Windows (headless).

## Критерии готовности
- SES-01 … SES-10 в `.feature`; каждый зелёный или `@ignore @bug-NNN` с задачей.
- SES-02: сообщение из 60 символов → название ровно первые 40 (сравнение строк, без обрезки пробелов, если спецификация её не требует — уточнить по S1.2 и коду; при неясности — вопрос в «Комментарии»).
- SES-04: переименование в пустую строку и в 41 символ не меняет название; что видит пользователь (ошибка или неактивная кнопка) — по коду, записать в «Отчёт».
- SES-05: отказ в диалоге подтверждения (фейк `IDialogService` отвечает «нет») — сессия остаётся; согласие — сессии нет и после `Restart`.
- SES-09: «Today» и «Earlier» считаются по `FakeTimeProvider`; сессия, созданная «вчера» (время сдвинуто шагом «Дано сейчас <дата время>»), — в «Earlier». Шаг времени — в `CommonSteps`.
- SES-07: смена модели ассистента по умолчанию в Settings не меняет модель уже созданной сессии, а новая сессия получает новую.
- Статусы SES-* в каталоге обновлены.

## Анализ
- `ImageGen/ImageGen.Presentation/PageModels/SessionsPageModel.cs` (`Sessions`, `Today`, `Earlier`, `NewSessionAsync`, `StartExampleAsync`, `Examples`), `SessionItemViewModel.cs` (переименование и удаление — найти команды), `ChatPageModel.cs` (`Title`).
- Ядро: `ImageGen/ImageGen.Core/Services/SessionService.cs`, правила названия — там же или в `HistoryService.cs`.
- Настройка новых сессий: `SettingsPageModel.SaveDefaultAssistantModelAsync`, `defaults.json` (`docs/specs/chat-sessions.md`, S1.3).
- Потоковые тесты: `Flows/EmptyChatExampleFlowTests.cs` (SES-10), `Views/SessionsViewTests.cs`.

## План
По одному сценарию SES-01 → SES-10, TDD-цикл из «Порядка исполнения». Число прогонов (SES-08) получить настоящими прогонами через ассистента (`Дано ассистент отвечает вызовом generate_images …`), а не записью в БД.

Файлы: `ImageGen/ImageGen.Acceptance.Tests/Features/Sessions.feature`, `Steps/SessionsSteps.cs` (новый), `Steps/CommonSteps.cs`, `docs/specs/acceptance-scenarios.md`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj -- --filter-trait "Category=sessions"
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

- Implemented and verified SES-01 (new empty session), SES-06 (default assistant model), and SES-09 (Today/Earlier grouping using `FakeTimeProvider`).
- Not complete: SES-02 stalls while `ChatPageModel.SendCommand` is awaited from a Reqnroll headless step; the command does not return before timeout. SES-05 also stalls in the delete command path. A direct data-layer workaround would violate the approved plan.
- SES-03/04 need configurable `IDialogService.PromptAsync` results for rename, but the approved task file list excludes `Support/FakePlatform.cs`, whose current prompt always returns `null`. SES-07/08/10 have not been implemented.
- The task remains in-progress; no task 255 changes have been copied to PR #93. Needs a plan amendment for fake prompt support and diagnosis of the command stalls.
- Verification: Acceptance suite 16 passed, 1 expected skip (`@ignore @bug-272`); formatting and `bash scripts/check.sh` passed.

## Комментарии

### 2026-11-09, claude-bdd-plan
Создано по просьбе владельца распланировать тест-сценарии приложения для исполнения GPT Luna. Сценарии и ожидания — в `docs/specs/acceptance-scenarios.md`. План ждёт одобрения.

### 2026-11-09, owner (in the chat, recorded by claude-bdd-plan)
Plan approved, status `approved`. Changes of the same day apply: the Gherkin scenarios, step texts, bindings, comments and the "Report" are in English (`docs/conventions.md`, "Language"; ADR 0031 is accepted); a "Structure of a scenario" in this plan is a `Scenario Outline`. Scenario expectations and wording are in the English catalog `docs/specs/acceptance-scenarios.md`. The acceptance project runs in CI (task 252). The flow tests in `ImageGen.Avalonia.Tests/Flows` that a scenario of this task covers are deleted by task 267, not here.

### 2026-11-10, codex-gpt6
The approved plan omits fake prompt result control needed by SES-03/04. Initial Reqnroll attempts at SES-02 send and SES-05 delete did not complete within the headless scenario timeout; no product code was changed. Request plan review before extending the file list or marking these product flows as defects.
