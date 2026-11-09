---
id: 257
status: approved
assignee:
claimed:
created: 2026-11-09
---

# Приёмочные сценарии: прогон и генерации

## Описание
Автоматизировать раздел «Прогон и генерации» каталога: GEN-01 … GEN-11. Файл `Features/Runs.feature`, тег `@runs`.

Зависит от: 253; шаги чата — из 256 (если 256 ещё не смерджена, общие шаги отправки сообщения взять из `CommonSteps`, а не дублировать).
Среда: Linux или Windows (headless).

## Критерии готовности
- GEN-01 … GEN-11 в `.feature`; каждый зелёный или `@ignore @bug-NNN` с задачей.
- GEN-01: сиды задаются шагом («Дано генератор сидов выдаёт 11, 22»); обе модели получили 11 и 22 (тела запросов), четыре карточки.
- GEN-02: модель без seed по каталогу (`supported_parameters` без seed) — в теле запроса нет поля seed; на карточке текст «seed not supported» (по ресурсам).
- GEN-03/04: разные исходы двух моделей одного прогона независимы; состояния — по `GenerationItemViewModel.StatusKind` и `StatusText`; отфильтрованная — `IsFiltered`, не ошибка.
- GEN-05: ответ генерации «держится» фейком → карточка `IsActive`; шаг «провайдер отвечает» → `IsDone` с картинкой.
- GEN-06: файл картинки существует в папке данных до того, как шаг проверяет карточку; после `Restart` та же карточка показывает тот же файл.
- GEN-07: цена `usage.cost` из ответа фейка видна на карточке; формат — по `ImagePriceText`, культура инвариантная.
- GEN-08: сводка прогона обновляется по мере завершения (две генерации, одна удержана: сначала «1 из 2», затем «2 из 2» — формулировка по ресурсам).
- GEN-09: PNG 120×80 из фейка → на карточке `120×80 px · 3:2`.
- GEN-10: смена моделей сессии во время удержанного прогона не меняет число карточек этого прогона; следующий прогон — по новым моделям.
- GEN-11: оценка следующего прогона (`ChatPageModel.NextRunEstimateText`) — сумма известных цен выбранных моделей (цены из каталога фейка).
- Статусы GEN-* в каталоге обновлены.

## Анализ
- `ImageGen/ImageGen.Presentation/PageModels/RunItemViewModel.cs`, `GenerationItemViewModel.cs`, `SummaryMarkerViewModel.cs`, `ImagePriceText.cs`, `CardImageLayout.cs`, `ChatPageModel.cs`.
- Ядро: `ImageGen/ImageGen.Core/Services/GenerationService.cs` (`WhenIdleAsync`), `GenerationParameterPlanner.cs`, `CostEstimator.cs`, `ISeedSource.cs`.
- Потоковые тесты: `Flows/ChatFlowTests.cs`, `Views/GenerationCardMetadataTests.cs`, `Views/SummaryMarkerTests.cs`.
- Спецификация: `docs/specs/chat-sessions.md` S3.1–S3.10; `docs/specs/image-client.md` F4.4, F5.2, F6.5, F7.2, F9.2.

## План
По одному сценарию GEN-01 → GEN-11. Таблицы моделей и исходов — `DataTable` (`| модель | исход |`). Шаг «Дано в сессии выбраны модели: …» выбирает модели через экран моделей сессии (как пользователь), а не записью настроек.

Файлы: `ImageGen/ImageGen.Acceptance.Tests/Features/Runs.feature`, `Steps/RunsSteps.cs` (новый), `Steps/CommonSteps.cs`, `Support/FakeOpenRouter.cs` (только новые ответы), `docs/specs/acceptance-scenarios.md`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj -- --filter-trait "Category=runs"
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
