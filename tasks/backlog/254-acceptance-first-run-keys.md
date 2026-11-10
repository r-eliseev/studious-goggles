---
id: 254
status: in-progress
assignee: codex-gpt6
claimed: 2026-11-10T17:45:16Z
created: 2026-11-09
---

# Приёмочные сценарии: первый запуск и ключи провайдеров

## Описание
Автоматизировать раздел «Первый запуск и ключи» каталога `docs/specs/acceptance-scenarios.md`: KEY-02 … KEY-09 (KEY-01 и KEY-10 сделаны в 252–253). Файл `Features/FirstRunAndKeys.feature`, тег фичи `@keys`.

Зависит от: 253.
Среда: Linux или Windows (headless).

## Критерии готовности
- KEY-02 … KEY-09 в `.feature` с тегами id и требований; каждый зелёный или `@ignore @bug-NNN` с задачей.
- Тексты сообщений проверяются по ресурсам (`AppResources.ProviderKeyInvalidFormat`, `ProviderKeyNetworkErrorFormat` и т. п. — точные имена найти в `ImageGen/ImageGen.Presentation/Resources/Strings/`), а не строковыми литералами в привязках.
- KEY-05: после неверного ключа ни текст статуса, ни файлы журнала в папке данных (`logs/`), ни журнал фейка сети не содержат введённого ключа. Ключ — фиктивный (`sk-or-acceptance-invalid`).
- KEY-06: ключ, сохранённый в первом запуске, виден после `Restart` мира: экран ключа не открывается.
- KEY-07 … KEY-09 — через экран Settings (`SettingsPageModel.ProviderKeys`, `ProviderKeyItemViewModel`): маска ключа — последние четыре символа; «Not connected» у fal и Together.
- Статусы KEY-* в каталоге обновлены.

## Анализ
- Экран ключа: `ImageGen/ImageGen.Presentation/PageModels/ApiKeyPageModel.cs` (`SaveAsync`, `StatusText`, `HasError`, `IsChoosingAssistantModel`, `UseSelectedAssistantModelAsync`), `SessionsPageModel.OnAppearingAsync` (переход на экран ключа без ключа).
- Settings: `SettingsPageModel.cs`, `ProviderKeyItemViewModel.cs`.
- Проверка ключа: `ImageGen/ImageGen.Core/Services/ApiKeyService.cs`, маршрут `/key` у `OpenRouterFakes`.
- Существующие потоковые тесты: `ImageGen/ImageGen.Avalonia.Tests/Flows/StartFlowTests.cs`, `SettingsFlowTests.cs` — сверить ожидания, не удалять.
- Спецификация: `docs/specs/image-client.md` F1.1–F1.3, F10.3; `docs/specs/chat-sessions.md` S1.3, S6.4.

## План
По одному сценарию в порядке KEY-02 → KEY-09. Для каждого: сценарий в `.feature` → прогон (падает: шаг не привязан) → привязка → зелёный → коммит `254-acceptance-first-run-keys: KEY-0N …`. Новые шаги «Дано»: «провайдер OpenRouter отвечает на проверку ключа кодом N» и «нет связи с OpenRouter» (исключение `HttpRequestException` из фейка) — в `CommonSteps`, если их нет.

Файлы: `ImageGen/ImageGen.Acceptance.Tests/Features/FirstRunAndKeys.feature`, `Steps/FirstRunSteps.cs`, `Steps/SettingsSteps.cs` (новый), `Steps/CommonSteps.cs`, `Support/FakeOpenRouter.cs` (только новые ответы), `docs/specs/acceptance-scenarios.md`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj -- --filter-trait "Category=keys"
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
