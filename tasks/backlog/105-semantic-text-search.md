---
id: 105
status: todo
assignee:
claimed:
created: 2026-10-29
---

# Семантический поиск только через OpenRouter

## Описание
Реализовать ADR 0016: text embeddings через OpenRouter, модель `baai/bge-m3`. Индексируются названия сессий, промпты и текстовые описания картинок. Пиксели, image URLs и файлы embedding provider не отправляются.

Решение владельца 2026-10-29: offline mode не будет. Не добавлять ONNX Runtime, локальные embedding-модели, веса, downloader или fallback без сети. При отсутствии сети/ключа semantic search явно недоступен; существующий локальный fuzzy search 079 продолжает работать.

Зависит от: 079, 081.
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

## План
Этап подготовки плана:
1. Получить ответ владельца: старые записи индексировать сразу при включении или постепенно в фоне? Как пользователь подтверждает передачу старой истории/платные embeddings и может остановить/ограничить индексацию? Не возвращать вопрос о local/offline — он уже решён отрицательно.
2. Утвердить UX: отдельный semantic mode или объединение с fuzzy, точка включения/выключения и индикатор недоступности. Выбрать стартовый supported размер индекса, max resident vectors/cache и поведение сверх него; это измерять, не угадывать.
3. Спроектировать `ITextEmbeddingClient`/OpenRouter implementation, нейтральные request/result с model/dimensions и nullable usage/cost unit. Fixture-тесты response index ordering, empty/malformed vectors, mixed dimensions, NaN/Infinity, provider errors/timeouts/cancel; не отправлять image content.
4. Спроектировать versioned source key (kind + устойчивый source record id), content hash, embedding model/dimension/format и lifecycle: insert/update/delete, title rename, description edit, повтор save, deleted-while-request-in-flight. В Data новая next-free migration и roundtrip float encoding с явной endianness; никаких каскадов в ledger расходов без договора с021.
5. Зафиксировать bounded queue/batch/retry и checkpoint/restart, dedupe неизменившегося текста, query debounce/cancellation и cache с model/hash key. API rate-limit/retry не должен заново тарифицировать весь уже завершённый batch.
6. Описать cosine ranking/zero-norm поведение, одно лучшее совпадение на сессию и устойчивый tie-break; snippets из локального исходного текста, privacy-тесты по записанным HTTP bodies. Добавить размеченные RU/EN примеры для оценки качества без внешних live calls в обычных tests.
7. Подготовить точные части/Файлы/тесты Core.Tests, Data.Tests, Presentation.Tests, Avalonia.Tests и обеих оболочек. До ответов на п.1–2 не ставить planned; после одобрения планов выполнять TDD, format, docs/ADR при изменённом решении, `bash scripts/check.sh`, отчёт/release-task по docs/tasks.md.

Файлы для анализа: `docs/decisions/0016-semantic-search.md`, `poc/backlog/081-semantic-search-research/README.md`, `ImageGen/ImageGen.Core/Search/ISearchTextSource.cs`, `ImageGen/ImageGen.Core/Search/SearchText.cs`, `ImageGen/ImageGen.Core/Search/SessionSearchService.cs`, `ImageGen/ImageGen.Data/Repositories/SearchTextSource.cs`, `ImageGen/ImageGen.Presentation/PageModels/SessionsPageModel.cs`, `ImageGen/ImageGen.Core/Providers/ProviderClientBase.cs`. Новые интерфейсы/файлы/точки indexing hooks назвать в окончательных планах.

## Отчёт

## Комментарии

### 2026-10-29, владелец (в чате, записал kiro)
Офлайн-режима semantic search не будет.

### 2026-10-29, codex-backlog-review
Подтверждено принятое решение без offline mode. Добавлены реальные неизвестности/границы и обязательные проверки; до ответа о переиндексации и UX/расходах задача остаётся todo для анализа. Исторические комментарии сохранены.
