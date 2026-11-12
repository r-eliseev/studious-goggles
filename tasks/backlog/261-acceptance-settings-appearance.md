---
id: 261
status: blocked
assignee:
claimed:
created: 2026-11-09
---

# Приёмочные сценарии: настройки и внешний вид

## Описание
Автоматизировать раздел «Настройки и внешний вид» каталога: SET-01 … SET-03. SET-04 (Clear logs) — после задачи 232. Файл `Features/Settings.feature`, тег `@settings`.

Зависит от: 253.
Среда: Linux или Windows (headless).

## Критерии готовности
- SET-01 … SET-03 в `.feature`; каждый зелёный или `@ignore @bug-NNN` с задачей.
- SET-01: в чистой папке данных выбран стиль Quiet и тема «как в системе» (`SettingsPageModel.Styles`, выбранная тема).
- SET-02: смена стиля на Gallery и темы на тёмную применяется сразу — проверка по ресурсам приложения Avalonia (`Application.Current.RequestedThemeVariant` и токен стиля, как в `Appearance/AvaloniaAppearanceApplierTests.cs`) — и сохраняется после `Restart`.
- SET-03: вход в Settings с экрана сессий (`SessionsPageModel.OpenSettingsAsync`) открывает маршрут `Routes.Settings`.
- Статусы SET-* в каталоге обновлены.

## Анализ
- `ImageGen/ImageGen.Presentation/PageModels/SettingsPageModel.cs` (`Styles`, `SelectThemeAsync`, `LoadAsync`), `StyleOptionViewModel.cs`; `ImageGen/ImageGen.Presentation/Appearance/` (сервис и файл настроек).
- Avalonia: `ImageGen/ImageGen.Avalonia.Tests/Appearance/AvaloniaAppearanceApplierTests.cs` — как проверяется применение.
- Потоковые тесты: `Flows/SettingsFlowTests.cs`.
- Спецификация: `docs/specs/chat-sessions.md` S6.1–S6.4; ADR 0014.

## План
По одному сценарию SET-01 → SET-03.

Файлы: `ImageGen/ImageGen.Acceptance.Tests/Features/Settings.feature`, `Steps/SettingsSteps.cs`, `Steps/CommonSteps.cs`, `docs/specs/acceptance-scenarios.md`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj -- --filter-trait "Category=settings"
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

### 2026-11-10, owner (in the chat, recorded by claude-bdd-plan) — plan extended
Task 232 is done, so SET-04 is added to this task: "Clear logs" (`SettingsPageModel.ClearLogsAsync`, confirmation through the dialog fake) deletes the log files of `<data folder>/logs`, and the app keeps writing the log afterwards (a new line appears after the next action). Files: the same as in the plan. Expectation from the catalog row SET-04 and task 232's "Report"; on a discrepancy — `@ignore @bug-NNN` and a task. Plan approved.

### 2026-11-12, codex-gpt6
The planned acceptance scenarios cannot verify the stated behavior with the current harness. `ImageGen/ImageGen.Acceptance.Tests/Support/AppWorld.cs` creates the composition without `configureLogging`, so the app does not write files under the data directory's `logs/`. It also does not start `AvaloniaAppearanceApplier`; only `ImageGen/ImageGen.Avalonia/App.axaml.cs` does that, and the plan forbids product/head startup changes. Therefore SET-02 cannot observe the live theme/style resources after the user's Settings command, and SET-04 cannot clear and then observe the active log file continuing to receive entries.

Proposed plan amendment: add `Support/AppWorld.cs` to `Files:` and allow it to configure the real file logger for the acceptance composition and start the Avalonia appearance applier against `Application.Current` for each started world. Keep the Settings steps as user actions and verify the resulting Avalonia resources and log file after restart. No implementation was made pending planner/owner approval.
