---
id: 257
status: review
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

Файлы: `ImageGen/ImageGen.Acceptance.Tests/Features/Runs.feature`, `Steps/RunsSteps.cs` (новый), `Steps/CommonSteps.cs`, `Support/FakeOpenRouter.cs` (только новые ответы), `docs/specs/acceptance-scenarios.md`, `Support/FakeOpenRouter.cs` and `Support/AppWorld.cs` (additive test-harness changes only: new configurable fake responses, catalog fields, world helpers; no change of existing behavior used by other features, no product code).

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

- GEN-01 is implemented. The scenario selects two image models and two runs per model through the session models page, configures seeds 11 and 22, then checks four cards and both request seeds per model.
- The fake returns an empty video catalog so the session models screen completes loading, and a tool-capable default assistant model so its save command can preserve the session assistant setting.
- Verification: `dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj -- --filter-trait "Category=runs"` passed (1/1); `dotnet format ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj --verify-no-changes` passed.
- GEN-02 is implemented with a fake model whose catalog omits `seed`; two requests are checked for no `seed` property and both cards show `seed not supported`. The filtered runs acceptance test passed (2/2); formatting passed.
- GEN-03 is implemented; both failed cards for one model show the failed state/reason while both cards for the other model succeed with images. The filtered runs acceptance test passed (3/3); formatting passed.
- GEN-04 is implemented; filtered generations keep `Filtered`/`IsFiltered` and never set `IsFailed`, while the second model succeeds. The filtered runs acceptance test passed (4/4); formatting passed.
- GEN-05 is implemented; a held fake provider response keeps the model card active, and releasing it completes the card with an image. The filtered runs acceptance test passed (5/5); formatting passed.
- GEN-06 is implemented; the scenario verifies the image file exists and is readable from storage before restart, then reopens the session and verifies the same card and file path are restored. The filtered runs acceptance test passed (6/6).
- GEN-07 is implemented; a `usage.cost` response is displayed as `$0.125` while the current culture is `fr-FR`. The filtered runs acceptance test passed (7/7).
- GEN-08 is implemented; with one completed and one held generation the run header shows `1 of 2 done`, then `2 of 2 done` after releasing the held response. The filtered runs acceptance test passed (8/8).
- GEN-09 is implemented; the generated 120×80 PNG displays `120×80 px · 3:2`. The filtered runs acceptance test passed (9/9).
- GEN-10 is implemented; changing selected models during a held run leaves its original two cards intact, and the next run uses only the newly selected model. The filtered runs acceptance test passed (10/10).
- GEN-11 is implemented; image catalog per-image prices are summed in the next run estimate. The filtered runs acceptance test passed (11/11).
- Full verification is incomplete: `dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj` hangs at the existing `Support/AppWorldTests.RestartAsync_ReusesDataFolderAndSecretStore`, including when run alone. The filtered `Category=runs` acceptance test passes (11/11), formatting passes, but the plan requires the full acceptance suite and the executor is not authorized to change `Support/AppWorldTests.cs`.
- Final verification on 2026-11-13 after task 277: the full acceptance suite passed (51 total, 50 passed, 1 skipped for SES-04 `@bug-276`); `Category=runs` passed (11/11); formatting and `bash scripts/check.sh` passed. The previous AppWorld restart hang no longer reproduces. GEN-01–GEN-11 are already marked `done` in `docs/specs/acceptance-scenarios.md`.

## Комментарии

### 2026-11-09, claude-bdd-plan
Создано по просьбе владельца распланировать тест-сценарии приложения для исполнения GPT Luna. Сценарии и ожидания — в `docs/specs/acceptance-scenarios.md`. План ждёт одобрения.

### 2026-11-09, owner (in the chat, recorded by claude-bdd-plan)
Plan approved, status `approved`. Changes of the same day apply: the Gherkin scenarios, step texts, bindings, comments and the "Report" are in English (`docs/conventions.md`, "Language"; ADR 0031 is accepted); a "Structure of a scenario" in this plan is a `Scenario Outline`. Scenario expectations and wording are in the English catalog `docs/specs/acceptance-scenarios.md`. The acceptance project runs in CI (task 252). The flow tests in `ImageGen.Avalonia.Tests/Flows` that a scenario of this task covers are deleted by task 267, not here.

### 2026-11-11, codex-gpt6
GEN-01–GEN-11 are implemented and pass the filtered runs suite (11/11), and formatting passes. The full acceptance command hangs at `Support/AppWorldTests.RestartAsync_ReusesDataFolderAndSecretStore` (line 11), including when that test is run in isolation; the test synchronously disposes `AppWorld` inside the Avalonia UI dispatch callback (`using var world`), while `AppWorld.Dispose()` blocks on `DisposeAsync()`. The accepted `Files:` list does not include `Support/AppWorldTests.cs`, and the task requires the full acceptance suite to pass. Proposed plan change: add `ImageGen/ImageGen.Acceptance.Tests/Support/AppWorldTests.cs` to `Files:` and make its async test disposal nonblocking (`await using`/await `DisposeAsync`), then rerun the full acceptance suite, formatting and `scripts/check.sh`. Task 257 is blocked pending approval of this scope change.

### 2026-11-13, claude-planner — plan amendment
Answers the mismatch of 2026-11-11: the hang of `AppWorldTests.RestartAsync_ReusesDataFolderAndSecretStore` was fixed by task 277 (done 2026-11-13, merged in PR #101), so no change of that file is needed here. GEN-01–GEN-11 are on `main`. Remaining work: rerun the full acceptance suite, formatting and `scripts/check.sh`, update the catalog statuses if not yet done, fill the Report and submit. The harness allowance of the other acceptance tasks applies here too.
Plan amendment approved (standing instruction of 2026-11-07: "approve all tasks that will come").
