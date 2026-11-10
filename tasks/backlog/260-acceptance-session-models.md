---
id: 260
status: approved
assignee:
claimed:
created: 2026-11-09
---

# Приёмочные сценарии: модели и параметры сессии

## Описание
Автоматизировать раздел «Модели и параметры сессии» каталога: MOD-01 … MOD-08. MOD-09 — после задачи 236. Файл `Features/SessionModels.feature`, тег `@models`.

Зависит от: 253.
Среда: Linux или Windows (headless).

## Критерии готовности
- MOD-01 … MOD-08 в `.feature`; каждый зелёный или `@ignore @bug-NNN` с задачей.
- MOD-01/07: экран моделей сессии открывается из чата (`ChatPageModel.OpenModelsAsync`), выбор — галочками в `ImageModelGroups`, число запусков, `SaveAsync` / `CancelAsync`; результат проверяется следующим прогоном (число карточек), а не чтением настроек.
- MOD-02: значения 0 и 11 не принимаются (как именно — по коду: ограничение поля или ошибка; записать в «Отчёт»).
- MOD-04: каталог фейка с моделями `black-forest-labs/…`, `~google/…`, `openai/…` → группы по производителю (правило S5.5, в т. ч. удаление начальных `~`).
- MOD-05: цена только у `output_image`; у модели без цены — «price unknown» (по ресурсам).
- MOD-06: без `RefreshAsync` повторное открытие экрана не делает запросов каталога (журнал фейка); после `RefreshAsync` — делает и показывает новые цены.
- MOD-08: параметры сессии (`SessionParametersPageModel`: соотношение сторон, разрешение, формат) после `SaveAsync` уходят в тело следующего запроса генерации; форма полей — по коду клиента (ADR 0011).
- Статусы MOD-* в каталоге обновлены.

## Анализ
- `ImageGen/ImageGen.Presentation/PageModels/SessionModelsPageModel.cs` (`ImageModelGroups`, `SearchText`, `RefreshAsync`, `SaveAsync`, `CancelAsync`, вкладки `ShowImageModels`/`ShowAssistantModel`/`ShowVideoModels`), `ModelOptionViewModel.cs`, `ModelVendors.cs`, `SessionParametersPageModel.cs`.
- Ядро: `ImageGen/ImageGen.Core/Services/ModelCatalogService.cs`.
- Потоковые тесты: `Flows/SessionModelsFlowTests.cs`, `Flows/SessionParametersFlowTests.cs`.
- Спецификация: `docs/specs/chat-sessions.md` S5.1–S5.7; `docs/specs/image-client.md` F4.2.

## План
По одному сценарию MOD-01 → MOD-08. Каталог фейка задаётся таблицей шага «Дано каталог моделей картинок OpenRouter».

Файлы: `ImageGen/ImageGen.Acceptance.Tests/Features/SessionModels.feature`, `Steps/SessionModelsSteps.cs` (новый), `Steps/CommonSteps.cs`, `Support/FakeOpenRouter.cs` (только новые ответы), `docs/specs/acceptance-scenarios.md`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj -- --filter-trait "Category=models"
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
