---
id: 265
status: approved
assignee:
claimed:
created: 2026-11-09
---

# Приёмочные сценарии: видео

## Описание
Автоматизировать раздел «Видео» каталога: VID-01 … VID-04. VID-05 (аудио) — после 221–223. Файл `Features/Video.feature`, тег `@video`.

Зависит от: 253, 257. Если на момент claim задачи 218–220 (плеер и карточка видео MAUI) меняют `GenerationItemViewModel`, сверить члены с `main`.
Среда: Linux или Windows (headless).

## Критерии готовности
- Шаг 1: маршруты видео в `FakeOpenRouter` по коду `OpenRouterClient` (`GET videos/models`, `POST videos`, опрос задания, `GET videos/{id}/content`). Формы ответов — только из кода и `docs/knowledge/media-generation.md`; чего там нет — вопрос в «Комментарии».
- VID-01 … VID-04 в `.feature`; каждый зелёный или `@ignore @bug-NNN` с задачей.
- VID-01: без выбранных видео-моделей в теле chat completion нет инструмента `generate_video`; с выбранной — есть.
- VID-02: вызов `generate_video` → карточка `IsVideo`, после опросов (время — `FakeTimeProvider`, ожидание — `IAsyncDelay`, как решено в 258) — «готово», видеофайл в папке данных.
- VID-03: ошибка при скачивании содержимого → «ошибка», файла нет (S «Ошибка скачивания или сохранения …» в `media-generation.md`).
- VID-04: отмена видео → «отменено» и предупреждение, что провайдер может продолжить (по ресурсам).
- Плеер в headless не проверяется (`UnsupportedMediaPlayerService`).
- Статусы VID-* в каталоге обновлены.

## Анализ
- `ImageGen/ImageGen.Core/OpenRouter/OpenRouterClient.cs` (строки около 82, 161, 171), `ImageGen/ImageGen.Core/Services/VideoModelCatalogService.cs`, `VideoPriceCalculator.cs`; `SessionModelsPageModel.ShowVideoModels`, `VideoModels`; `GenerationItemViewModel.IsVideo`, `VideoFilePath`.
- Спецификация: `docs/specs/media-generation.md`, «Требования»; ADR 0027.

## План
1. Маршруты видео в фейке (самопроверки в `FakeNetworkTests` сначала).
2. По одному сценарию VID-01 → VID-04.

Файлы: `ImageGen/ImageGen.Acceptance.Tests/Features/Video.feature`, `Steps/VideoSteps.cs` (новый), `Steps/CommonSteps.cs`, `Support/FakeOpenRouter.cs`, `Support/FakeNetworkTests.cs`, `docs/specs/acceptance-scenarios.md`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj -- --filter-trait "Category=video"
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
