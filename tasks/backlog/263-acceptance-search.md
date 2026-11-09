---
id: 263
status: planned
assignee:
claimed:
created: 2026-11-09
---

# Приёмочные сценарии: поиск сессий (Words и Meaning)

## Описание
Автоматизировать раздел «Поиск» каталога: SRCH-01 … SRCH-04. SRCH-05 — после задач 226–228. Файл `Features/Search.feature`, тег `@search`.

Зависит от: 253, 257 (шаги прогона). Если на момент claim задача 217 (имя модели эмбеддингов) не смерджена — сверить маршрут эмбеддингов фейка с `main`.
Среда: Linux или Windows (headless).

## Критерии готовности
- SRCH-01 … SRCH-04 в `.feature`; каждый зелёный или `@ignore @bug-NNN` с задачей.
- SRCH-01/02: `SessionsPageModel.SearchText`, `SearchResults`, `HasNoResults`, `ClearSearch`; поиск по названию и по промпту генерации.
- SRCH-03: фейк отвечает на `POST embeddings` (`OpenRouterClient`, около строки 559) векторами из таблицы шага: тексту ставится в соответствие вектор, близость задаётся сценарием. Размерность — та, что ждёт код (проверить по коду; фактическая размерность `baai/bge-m3` неизвестна, `docs/status.md`). Включение — `SettingsPageModel.ToggleSemanticSearchAsync`; дождаться окончания индексации по `SemanticIndexStatus`.
- SRCH-04: `SelectSemanticSearch` / `SelectWordSearch` — результаты режимов не смешиваются.
- Статусы SRCH-* в каталоге обновлены.

## Анализ
- `ImageGen/ImageGen.Presentation/PageModels/SessionsPageModel.cs` (`SearchText`, `SearchResults`, `IsSemanticSearchMode`, `SemanticIndexStatus`), `SettingsPageModel.cs` (`ToggleSemanticSearchAsync`, `StopSemanticIndexAsync`).
- Ядро: `ImageGen/ImageGen.Core/Search/SessionSearchService.cs`, `ImageGen/ImageGen.Core/Services/SemanticIndexService.cs`, `SemanticSearchService.cs`.
- Спецификация: `docs/specs/chat-sessions.md` S1.4; ADR 0016.

## План
По одному сценарию SRCH-01 → SRCH-04.

Файлы: `ImageGen/ImageGen.Acceptance.Tests/Features/Search.feature`, `Steps/SearchSteps.cs` (новый), `Steps/CommonSteps.cs`, `Support/FakeOpenRouter.cs` (маршрут эмбеддингов), `docs/specs/acceptance-scenarios.md`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj -- --filter-trait "Category=search"
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
