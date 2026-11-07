---
id: 217
status: in-progress
assignee: codex-gpt6
claimed: 2026-11-07T10:13:41Z
created: 2026-11-07
---

# Семантический индекс: OpenRouter отвечает именем провайдера модели

## Описание
Лог владельца 2026-11-07 после задачи 215: `Semantic search indexing failed with provider error Unknown: response model 'parasail-bge-m3' differs from requested 'baai/bge-m3'`. OpenRouter направляет запрос провайдеру (Parasail) и в поле `model` ответа пишет его имя модели, а не запрошенное `baai/bge-m3`. Проверка `ValidateResult` отклоняет каждый ответ.

Просто принять имя из ответа нельзя: `SemanticIndexService` сравнивает сохранённые эмбеддинги с константой `EmbeddingModel` строго (`StringComparison.Ordinal` в отборе устаревших и в `CountCurrent`), поэтому эмбеддинги с `parasail-bge-m3` считались бы устаревшими и пересчитывались бесконечно.

Живая проверка 2026-11-07 (claude-audit, ключ владельца, прямой `POST /api/v1/embeddings` с двумя текстами): HTTP 200 за 3,1 с; `model: "BAAI/bge-m3"`, `provider: "DeepInfra"`; `data` — 2 элемента с `object`, `embedding` (1024 числа `float`), `index`; `usage.cost = 1.6e-07` (USD; `cost_details.upstream_inference_cost` то же). Утром лог владельца показал `parasail-bge-m3` — имя зависит от провайдера, которого выбрал OpenRouter.

Сценарий LiveCheck `--only embeddings` (215) падает `InvalidCastException`: `(OpenRouterClient)router.GetConnection(provider)` — роутер оборачивает клиент в `InMemoryQueuedImageConnection`.

Зависит от: —
Среда: Linux или Windows.

## Критерии готовности
- `ValidateResult` больше не требует совпадения `result.Model` с запрошенной моделью; остальные проверки (число векторов, размерность, конечные значения) — как сейчас.
- Эмбеддинги сохраняются с запрошенным идентификатором модели (`EmbeddingModel`, `baai/bge-m3`), а не с именем из ответа; имя из ответа пишется в журнал на уровне Debug один раз за индексацию.
- Все сравнения модели в `SemanticIndexService` — с `EmbeddingModel` единообразно (одно правило, одна вспомогательная функция).
- То же в `SemanticSearchService` (поиск по смыслу): `ValidateQueryVector` не требует `result.Model == "baai/bge-m3"` (иначе запрос поиска падает так же, как индексация); отбор сохранённых векторов (`embedding.Model == "baai/bge-m3"`) и расход запроса (`ModelId`) — по запрошенной модели; строковые константы `"baai/bge-m3"` в службе заменить общей константой (одно место, например `SemanticSearchModels.Default` в `ImageGen.Core/Search/`).
- Защита от смены модели провайдером — проверка размерности (уже есть: смена размерности очищает индекс).
- `EmbeddingTraceSummary` LiveCheck (215) не считает разницу имени ошибкой, а печатает оба имени и поле `provider`.
- LiveCheck `RunEmbeddingsAsync` берёт клиента через `router.GetTextEmbeddingClient(provider)` вместо приведения `GetConnection` к `OpenRouterClient`; запуск `--only embeddings` с ключом проходит (результат — в «Отчёт»).
- Цена эмбеддингов: `OpenRouterClient.EmbedAsync` возвращает `usage.cost` как `CostUsd` (единица подтверждена живым ответом — USD); комментарий «unit has not been verified» убрать; расход записывается в журнал (как уже делает `SemanticIndexService` при `CostUsd` не `null`). Тест: ответ с `usage.cost` 1.6e-07 → `CostUsd = 0.00000016`.
- Тесты: поиск по смыслу при ответе `model = "BAAI/bge-m3"` возвращает результаты; ответ с `model = "parasail-bge-m3"` и правильными векторами → эмбеддинги сохранены с `baai/bge-m3`, статус `Ready`; повторный запуск индексации ничего не пересчитывает (провайдер не вызывается).
- Факт записан в `docs/knowledge/dev-environment.md`, раздел «OpenRouter embeddings» (215): поле `model` ответа — имя провайдера (`parasail-bge-m3`, лог 2026-11-07), не запрошенное.

## Анализ
`ImageGen/ImageGen.Core/Services/SemanticIndexService.cs`: константа `EmbeddingModel` (строка 21), отбор размерностей (≈317, `OrdinalIgnoreCase`), отбор устаревших (≈334, `Ordinal`), `CountCurrent` (≈511, `Ordinal`), создание `TextEmbedding(... result.Model ...)`, `ValidateResult` (≈567). Тесты — `ImageGen/ImageGen.Core.Tests/Services/SemanticIndexServiceTests.cs`. LiveCheck — `ImageGen/ImageGen.LiveCheck/Scenarios/EmbeddingTraceSummary.cs`.

## План
1. Падающие тесты из «Критериев».
2. Правка `ValidateResult`, сохранения и сравнений; Debug-запись имени из ответа.
3. LiveCheck-сводка; запись в `docs/knowledge/dev-environment.md`.

Файлы: `ImageGen/ImageGen.Core/Services/SemanticSearchService.cs`, `ImageGen/ImageGen.Core.Tests/Search/SemanticSearchServiceTests.cs`, `ImageGen/ImageGen.Core/OpenRouter/OpenRouterClient.cs`, `ImageGen/ImageGen.Core.Tests/OpenRouter/OpenRouterClientTests.cs`, `ImageGen/ImageGen.LiveCheck/Scenarios/LiveCheckRunner.cs`, `ImageGen/ImageGen.Core/Services/SemanticIndexService.cs`, `ImageGen/ImageGen.Core.Tests/Services/SemanticIndexServiceTests.cs`, `ImageGen/ImageGen.LiveCheck/Scenarios/EmbeddingTraceSummary.cs`, `ImageGen/ImageGen.LiveCheck.Tests/EmbeddingTraceSummaryTests.cs`, `docs/knowledge/dev-environment.md`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.LiveCheck.Tests/ImageGen.LiveCheck.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

### Порядок исполнения (для Luna)
- Сначала прочитай только файлы из строки «Файлы» и названные тесты; сигнатуры сверяй с текущим `main`.
- Шаги строго по порядку (ub-mode): падающий тест → запуск, убедиться, что он падает именно по описанной причине → минимальный фикс → тест зелёный.
- Не меняй файлы вне строки «Файлы», не переименовывай существующие члены сверх плана, не чини соседние находки — они в отдельных задачах.
- Если код расходится с планом (нет такого метода, другая сигнатура, тест уже есть), не импровизируй: остановись и запиши вопрос в «Комментарии».
- Команды `dotnet` выполняй по одной, не параллельно. После каждого изменённого проекта — `dotnet format <проект> --verify-no-changes`.
- В «Отчёте»: что изменено, команды и результаты, отличия от плана.

## Отчёт

## Комментарии

### 2026-11-07, claude-audit
Создано по отчёту владельца 2026-11-07 (лог MAUI Android `ce90792`: видео генерируется, звук есть, полноэкранный режим работает). План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
План одобрен ранее данным поручением «заапрувь все таски которые будут».

### 2026-11-07, claude-audit
Добавлены результаты живой проверки эмбеддингов, исправление падения сценария LiveCheck и запись цены эмбеддингов (`usage.cost` в USD).

### 2026-11-07, claude-audit
Добавлен `SemanticSearchService`: та же строгая проверка имени модели в запросе поиска (строка ≈235 на `ce90792`).
