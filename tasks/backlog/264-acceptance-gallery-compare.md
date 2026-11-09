---
id: 264
status: approved
assignee:
claimed:
created: 2026-11-09
---

# Приёмочные сценарии: галерея, просмотрщик и сравнение

## Описание
Автоматизировать раздел «Галерея, просмотр и сравнение» каталога: GAL-01 … GAL-05. Файл `Features/Gallery.feature`, тег `@gallery`.

Зависит от: 253, 257 (шаги прогона).
Среда: Linux или Windows (headless).

## Критерии готовности
- GAL-01 … GAL-05 в `.feature`; каждый зелёный или `@ignore @bug-NNN` с задачей.
- GAL-01: две сессии, в каждой готовая и ошибочная карточка → в галерее (`SessionsPageModel.OpenGalleryAsync`, `GalleryPageModel.Items`) только две готовые, новые сверху.
- GAL-02: фильтр модели (`Models`, `SelectedModel`) и периода (`SelectedPeriod`, `ApplyFiltersAsync`); «старые» картинки — сдвигом `FakeTimeProvider` между прогонами.
- GAL-03: `GalleryPageModel.OpenAsync` открывает исходную сессию (маршрут чата с её id).
- GAL-04: просмотрщик — через настоящий `IImageViewerService` Avalonia или фейк, смотря что проще проверить в headless (сверить с `Services/OverlayImageViewerServiceTests.cs`); листание только внутри прогона (`ImageViewerNavigationState`).
- GAL-05: сравнение (`CompareAsync` у карточки, `ComparisonPageModel`: `Mode`, `Zoom`, `SetZoom`, `SetOffset`) — переключение режима и общий масштаб для обеих картинок.
- Статусы GAL-* в каталоге обновлены.

## Анализ
- `ImageGen/ImageGen.Presentation/PageModels/GalleryPageModel.cs`, `GalleryItemViewModel.cs`, `GalleryModelFilter.cs`, `ComparisonPageModel.cs`, `ComparisonMode.cs`; `ImageGen/ImageGen.Presentation/Services/ImageViewerNavigationState.cs`, `IImageViewerService.cs`, `IImageComparisonService.cs`.
- Avalonia: `ImageGen/ImageGen.Avalonia.Tests/Services/OverlayImageViewerServiceTests.cs`, `OverlayImageComparisonServiceTests.cs`, `Views/GalleryViewTests.cs`.
- Спецификация: `docs/specs/image-client.md` F6.2, F6.3, F7.3, F7.4.

## План
По одному сценарию GAL-01 → GAL-05.

Файлы: `ImageGen/ImageGen.Acceptance.Tests/Features/Gallery.feature`, `Steps/GallerySteps.cs` (новый), `Steps/CommonSteps.cs`, `Support/FakePlatform.cs` (если просмотрщик подменяется), `docs/specs/acceptance-scenarios.md`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj -- --filter-trait "Category=gallery"
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
