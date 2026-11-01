---
id: 105
status: in-progress
assignee: codex-gpt6
claimed: 2026-11-01T20:04:09Z
created: 2026-10-29
---

# Семантический поиск только через OpenRouter

## Описание
Реализовать ADR 0016: text embeddings через OpenRouter, модель `baai/bge-m3`. Индексируются названия сессий, промпты и текстовые описания картинок. Пиксели, image URLs и файлы embedding provider не отправляются.

Решение владельца 2026-10-29: offline mode не будет. Не добавлять ONNX Runtime, локальные embedding-модели, веса, downloader или fallback без сети. При отсутствии сети/ключа semantic search явно недоступен; существующий локальный fuzzy search 079 продолжает работать.

Зависит от: 079, 081; этап 2 — также 021 (этап A, журнал расходов).
Среда: Linux или Windows; обе UI-оболочки.

Статус todo позволяет исследование и подготовку плана. До planned требуется решение владельца о переиндексации старых данных; готового одобренного плана реализации нет.

## Критерии готовности
- Text embeddings вычисляются `baai/bge-m3` через OpenRouter POST /embeddings; запрос содержит только разрешённые тексты, без image bytes/data URL/file URL и непубличных provider URLs.
- Векторы хранятся в SQLite BLOB с model/dimension/format/source-version; не используются sqlite-vec alpha, ONNX/local models, weights/download/fallback.
- Нет сети/ключа/provider failure → понятное отдельное состояние semantic search; локальный fuzzy search остаётся доступен без платного вызова.
- Indexing и query имеют ограниченные batch/concurrency/retry/объём и не запускают скрытые повторные платные запросы; пользователь видит состояние/объём индекса и фактическую/неизвестную стоимость, если она доступна.
- Изменённый/удалённый текст инвалидируется; старый embedding не прикрепляется к новой версии, повторное сохранение без изменения текста не вызывает повторной индексации.
- Стратегия переиндексации старых данных отдельно решена владельцем до planned. UX активации/согласия на отправку/расходы и масштаб/память поиска зафиксированы в плане до исполнения.
- Privacy, storage, ranking, dimension/NaN validation, cancellation, stale responses и provider errors покрыты tests; качество проверено на небольшой размеченной RU/EN выборке, без выдуманного threshold.

## Анализ
`ISearchTextSource.ListAllAsync` возвращает SearchText(SessionId, Kind, Text) без устойчивого id исходной записи, updated version и пагинации. Для векторного индекса такого контракта недостаточно: заголовок, несколько промптов и описаний надо различать и инвалидировать независимо. Нельзя сделать ключ только (SessionId,Kind), иначе все промпты сессии перетрут друг друга.

`SessionSearchService` сейчас ранжирует fuzzy-совпадения в памяти, одно лучшее на сессию. Semantic score не имеет той же шкалы: их нельзя складывать без определённого алгоритма. До плана зафиксировать отдельный semantic режим или точную политику объединения; fallback fuzzy остаётся локальным и явным.

Замер ADR0016 относится к 512-dimensional векторам на cloud CPU, а не к фактической размерности bge-m3 на телефоне. План должен измерить возвращаемую размерность, bytes per vector и peak memory на выбранном формате/объёме; не обещать 50k на мобильном только по чужому benchmark. API usage.cost есть в схеме, но его единица не подтверждена вызовом; не маркировать неизвестную единицу как USD.

Для Haiku/Luna разделить после решений: embeddings transport/contract tests → versioned storage/indexer → ranking/read path → UI активации/состояний/reindex. В каждой части явно сохранить отсутствие offline mode; новый пакет не нужен для обычного HTTP+SQLite float BLOB.


Уточнение 2026-10-31 (claude-audit). Решения владельца 2026-10-31: при включении **вся существующая история индексируется сразу** (одним проходом с прогрессом, после подтверждения объёма и платности); поиск по смыслу — **отдельный режим** рядом с поиском по словам, результаты не смешиваются. Решение того же дня по 021: embeddings — платный вызов, его цена идёт в журнал расходов.
- Источник текстов `SearchTextSource` отдаёт `(session_id, kind, text)` без id записи: промпты берутся `DISTINCT` из `runs` и `generations`, описания — из `image_descriptions`. Для индекса нужен устойчивый ключ: название → id сессии, промпт → id прогона (у генерации без собственного промпта — промпт прогона; правка промпта (Edit) создаёт новый прогон), описание → id описания.
- `bge-m3` по описанию модели даёт 1024-мерные векторы (4 КБ на float32) — проверить первым вызовом, не закладывать в код. 10 000 текстов ≈ 40 МБ в памяти при полном переборе; это верхняя поддерживаемая граница первой версии, сверх неё — сообщение в журнал и поиск по первым 10 000 последних.

## План
Сначала падающий тест (ub-mode). Четыре этапа, отдельные PR, по порядку. Этап 2 записывает цену в журнал 021 — его этап A должен быть в main; без него цена только в журнал приложения, задача не закрывается.

**1. Клиент эмбеддингов.** `Providers/ITextEmbeddingClient.EmbedAsync(IReadOnlyList<string> texts, ct)` → `EmbeddingResult(Vectors float[][], Model, Dimensions, CostUsd?)`; реализация в `OpenRouterClient`: `POST embeddings`, `model = baai/bge-m3`, `input` — массив строк, ответ через ограниченный reader (`ProviderResponseLimits.Json`). Проверки ответа: число и порядок векторов по `index`, одинаковая размерность, нет NaN/Infinity, иначе `ProviderException(Unknown)`. Тесты на заглушке HTTP: тело запроса содержит только переданные тексты (никаких URL, data-URL, байтов картинок); перепутанный порядок `index`; пустой/неполный ответ; NaN; 401/429/5xx → коды ошибок; отмена.

**2. Хранение и индексатор.**
- Core `Search/SearchSource(SourceKey, SessionId, Kind, Text)`, `ISearchTextSource.ListSourcesAsync` (ключи `title:<sessionId>`, `prompt:<runId>`, `desc:<descriptionId>`). Старый `ListAllAsync` остаётся для поиска по словам.
- Data миграция `NNNN_text_embeddings.sql`: `text_embeddings(source_key TEXT PK, session_id TEXT NOT NULL REFERENCES sessions(id) ON DELETE CASCADE, kind INT, content_hash TEXT, model TEXT, dimensions INT, vector BLOB, created_at TEXT)`; вектор — float32 little-endian (явно, тест на roundtrip). Удаление сессии удаляет её векторы (приватность); журнал расходов это не трогает.
- `Services/SemanticIndexService`: `GetStatusAsync` → `{Enabled, Indexed, Pending, State (Off|Indexing|Ready|Failed|Unavailable)}`; `EnableAsync` (после подтверждения) — пишет флаг в `search.json` в папке данных и запускает полный проход; `DisableAsync` — флаг и удаление векторов; `StopAsync` — остановить проход (уже сохранённые батчи остаются, следующий запуск продолжает). Проход: источники без вектора или с другим `content_hash` (SHA-256 нормализованного текста) → батчи по 64, один запрос одновременно, повтор только для сетевых ошибок/429/5xx — до 2 раз с паузой 2 и 8 с, затем `Failed` с кодом; каждый успешный батч сохраняется в транзакции (точка продолжения). Цена батча — запись в журнал расходов (`ExpenseKind.Embedding`, operation id = id батча).
- Новые и изменённые тексты при включённом режиме: подписка на `SessionChangeNotifier`, пересчёт источников одной сессии с задержкой 2 с; удалённые источники удаляются из индекса.
- Тесты: неизменённый текст не отправляется повторно; изменённое название — один новый вызов, старый вектор заменён; удаление сессии во время запроса — ответ не записывается; остановка и продолжение; повтор только для временных ошибок; цена в журнале; перебор 10 000.

**3. Поиск по смыслу.** `SemanticSearchService.SearchAsync(query, ct)`: запрос < 3 символов — пусто; задержка ввода 400 мс (в PageModel); вектор запроса — один вызов, кэш LRU на 50 запросов по (модель, текст); косинусная близость по всем векторам (загружаются в память при первом поиске, кэш сбрасывается при изменении индекса), нулевая норма — пропуск; лучшее совпадение на сессию, порядок по убыванию, при равенстве — по дате сессии; первые 20. Цена запроса — в журнал. Сниппет — из локального текста источника. Тесты: ранжирование на фикстурных векторах, tie-break, отмена устаревшего запроса, недоступность (нет ключа/сети) → `Unavailable` без падения.

**4. Интерфейс.** Settings: раздел «Semantic search» — переключатель; при включении диалог: «N texts (titles, prompts, image descriptions) will be sent to OpenRouter (`baai/bge-m3`), paid» → подтверждение; прогресс «Indexed X of N», кнопка Stop, состояние ошибки. Список сессий: переключатель режима «Words / Meaning» у поля поиска; «Meaning» недоступен с подсказкой, если режим выключен, индексация не завершена или нет ключа OpenRouter; поиск по словам работает всегда. Обе оболочки; тесты Presentation и Avalonia headless.

Качество: перед завершением владелец с ключом прогоняет 20 запросов RU/EN (список в отчёте) и оценивает выдачу; в автотестах — только фикстурные векторы, без живых вызовов. Размерность, байты на вектор, пиковая память и фактическая единица `usage.cost` записываются в отчёт и `docs/decisions/0016-semantic-search.md` (раздел «Последствия»).

Проверки: Core.Tests, Data.Tests, Presentation.Tests, Avalonia.Tests, сборка MAUI Android, `dotnet format`, `scripts/check.sh`. Документы: `docs/specs/chat-sessions.md` (S1.4 — режим по смыслу), `docs/architecture.md`.

Файлы: `ImageGen/ImageGen.Core/Providers/ITextEmbeddingClient.cs`, `EmbeddingResult.cs` (новые), `ImageGen/ImageGen.Core/OpenRouter/OpenRouterClient.cs`, `ImageGen/ImageGen.Core/Search/SearchSource.cs`, `ISearchTextSource.cs`, `SemanticIndexService.cs`, `SemanticSearchService.cs`, `SemanticIndexStatus.cs`, `Storage/ITextEmbeddingRepository.cs` (новые), `CoreServiceCollectionExtensions.cs`; `ImageGen/ImageGen.Data/Migrations/NNNN_text_embeddings.sql`, `Repositories/TextEmbeddingRepository.cs` (новые), `Repositories/SearchTextSource.cs`, `DataServiceCollectionExtensions.cs`; `ImageGen/ImageGen.Presentation/PageModels/SettingsPageModel.cs`, `SessionsPageModel.cs`, `Search/SearchSettingsFile.cs` (новый), `AppResources.resx/.cs`; `ImageGen/ImageGen.Avalonia/Views/SettingsView.axaml`, `SessionsListView.axaml`; `ImageGen/ImageGen.Maui/Pages/SettingsPage.xaml`, `Controls/SessionsListView.xaml`; тесты Core/Data/Presentation/Avalonia; `docs/decisions/0016-semantic-search.md`.

## Отчёт

## Комментарии

### 2026-10-29, владелец (в чате, записал kiro)
Офлайн-режима semantic search не будет.

### 2026-10-29, codex-backlog-review
Подтверждено принятое решение без offline mode. Добавлены реальные неизвестности/границы и обязательные проверки; до ответа о переиндексации и UX/расходах задача остаётся todo для анализа. Исторические комментарии сохранены.

### 2026-10-31, claude-audit
План готов, статус planned. Допущения: верхняя граница 10 000 текстов в индексе; батчи по 64, один запрос одновременно, до 2 повторов временных ошибок; задержка ввода 400 мс, первые 20 результатов; выключение режима удаляет векторы. Зависимость: этап 2 ждёт этапа A задачи 021 (журнал расходов).

### 2026-10-31, владелец (в чате, записал claude-audit)
План одобрен вместе с допущениями из комментария выше.

### 2026-10-31, владелец (в чате, записал claude-audit)
Разрешено дописать в «Зависит от» зависимость этапа 2 от 021 (этап A).
