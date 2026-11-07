---
id: 227
status: approved
assignee:
claimed:
created: 2026-11-07
---

# Семантический поиск: выбор модели эмбеддингов и переиндексация (ядро)

## Описание
Решение владельца 2026-11-07: модель эмбеддингов выбирается из всех доступных у OpenRouter (как модели картинок и видео), потому что модели со временем устаревают; при смене модели история переиндексируется новой моделью. Сейчас модель жёстко задана: `baai/bge-m3` в `SemanticIndexService`, `SemanticSearchService` (после 217 — общая константа) и `OpenRouterClient.EmbedAsync`.

Каталог (публичный, без ключа, проверено 2026-11-07): `GET https://openrouter.ai/api/v1/embeddings/models` — 40 моделей; у модели `id`, `name`, `description`, `context_length`, `pricing.prompt` (USD за токен, например `0.00000001` у `baai/bge-m3`), `architecture`. Размерности вектора в каталоге нет — она известна после первого ответа.

Зависит от: 217, 226.
Среда: Linux или Windows.

## Критерии готовности
- Каталог: `EmbeddingModel` (record: `ProviderId`, `Id`, `Name`, `Description`, `int? ContextLength`, `decimal? PricePerTokenUsd`, `FetchedAt`), `IEmbeddingModelProvider.ListEmbeddingModelsAsync` в `OpenRouterClient` (`GET embeddings/models`), кэш — по образцу видео-каталога 200 (таблица `embedding_models`, миграция `NNNN_embedding_models.sql`, `EmbeddingModelCatalogService`), проброс через `InMemoryQueuedImageConnection` и `ProviderRouter`.
- Настройка модели: `ISemanticSearchSettingsStore.GetModelAsync`/`SetModelAsync`; `SearchSettingsFile` (`search.json`) хранит `model` рядом с `enabled`; нет поля или неверное значение → `baai/bge-m3` (константа 217) и Warning, как для `enabled`.
- `ITextEmbeddingClient.EmbedAsync(string modelId, IReadOnlyList<string> texts, CancellationToken)`: модель передаётся параметром; `OpenRouterClient` шлёт её в теле.
- `SemanticIndexService` и `SemanticSearchService` берут модель из настройки (читают при каждом прогоне индексации/запросе; кэш векторов запросов поиска — с ключом «модель + текст»). Эмбеддинги хранятся с id выбранной модели; при смене модели все эмбеддинги прежней считаются устаревшими (уже так по сравнению модели) и пересчитываются; поиск, пока идёт переиндексация, использует только векторы текущей модели (частичные результаты лучше, чем смешанные).
- `ISemanticIndexController.ChangeModelAsync(string modelId, ct)`: проверка по каталогу (неизвестная → `ArgumentException`); останавливает идущую индексацию (как `StopAsync`), `DeleteAllAsync` эмбеддингов, сохраняет модель, запускает индексацию; если поиск выключен — только сохраняет модель.
- `EstimateReindexAsync(string modelId, ct)` → `ReindexEstimate(int Sources, long ApproxTokens, decimal? CostUsd)`: токены ≈ сумма длин нормализованных текстов (до `MaximumIndexedSources`) / 4 (округление вверх); цена = токены × `PricePerTokenUsd` (нет цены → `null`).
- Устаревшая модель: если эмбеддинг-запрос отвечает «модель недоступна» (`ErrorKind.ModelUnavailable` — как это классифицирует общий маппер ошибок `OpenRouterClient`; проверить на ответе 404/400 с текстом про модель в тесте) — статус `Unavailable` с `ErrorCode = "model_unavailable"` и `ErrorDetail` = имя модели; поиск по смыслу недоступен, словесный поиск работает. При обновлении каталога, если выбранной модели в нём нет, — тот же статус без запроса.
- Тесты: каталог из фикстуры (3 модели) с ценами; `search.json` без `model` → `baai/bge-m3`; `ChangeModelAsync` удаляет эмбеддинги и следующий прогон шлёт новую модель; поиск во время переиндексации использует только векторы новой модели; оценка 10 источников по 400 символов при цене 1e-8 → 1000 токенов, $0.00001; ответ «модель недоступна» → `model_unavailable`; выбранной модели нет в каталоге → `model_unavailable` без запроса.

## Анализ
`ImageGen/ImageGen.Core/Services/SemanticIndexService.cs`, `SemanticSearchService.cs`, `ImageGen/ImageGen.Core/Search/` (`ISemanticSearchSettingsStore`, `ISemanticIndexController`, `SemanticIndexStatus`), `ImageGen/ImageGen.Presentation/Search/SearchSettingsFile.cs`, `ImageGen/ImageGen.Core/OpenRouter/OpenRouterClient.cs` (`EmbedAsync`), `ImageGen/ImageGen.Core/Providers/ITextEmbeddingClient.cs`; все реализации и вызовы `EmbedAsync` (`rg -n "EmbedAsync" ImageGen`) — включая LiveCheck и тестовые двойники. Образец каталога и кэша — `VideoModelCatalogService`, `IVideoModelRepository`, `VideoModelRepository`.

## План
1. Падающие тесты из «Критериев» (Core, Data, Presentation для `SearchSettingsFile`).
2. Модель параметром в `EmbedAsync`; правка вызовов.
3. Настройка модели; чтение модели в обеих службах.
4. Каталог: тип, провайдер, миграция, репозиторий, служба.
5. `ChangeModelAsync`, `EstimateReindexAsync`, статус `model_unavailable`.

Файлы: `ImageGen/ImageGen.Core/Domain/Models/EmbeddingModel.cs`, `ImageGen/ImageGen.Core/Providers/IEmbeddingModelProvider.cs`, `ImageGen/ImageGen.Core/Providers/ITextEmbeddingClient.cs`, `ImageGen/ImageGen.Core/OpenRouter/OpenRouterClient.cs`, `ImageGen/ImageGen.Core/Providers/InMemoryQueuedImageConnection.cs`, `ImageGen/ImageGen.Core/Providers/ProviderRouter.cs`, `ImageGen/ImageGen.Core/Providers/IProviderRouter.cs`, `ImageGen/ImageGen.Core/Services/EmbeddingModelCatalogService.cs`, `ImageGen/ImageGen.Core/Storage/IEmbeddingModelRepository.cs`, `ImageGen/ImageGen.Core/Services/SemanticIndexService.cs`, `ImageGen/ImageGen.Core/Services/SemanticSearchService.cs`, `ImageGen/ImageGen.Core/Search/ISemanticSearchSettingsStore.cs`, `ImageGen/ImageGen.Core/Search/ISemanticIndexController.cs`, `ImageGen/ImageGen.Core/Search/ReindexEstimate.cs`, `ImageGen/ImageGen.Core/CoreServiceCollectionExtensions.cs`, `ImageGen/ImageGen.Data/Migrations/NNNN_embedding_models.sql`, `ImageGen/ImageGen.Data/Repositories/EmbeddingModelRepository.cs`, `ImageGen/ImageGen.Data/DataServiceCollectionExtensions.cs`, `ImageGen/ImageGen.Presentation/Search/SearchSettingsFile.cs`, `ImageGen/ImageGen.LiveCheck/Scenarios/LiveCheckRunner.cs`, тесты Core, Data, Presentation, LiveCheck.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.LiveCheck.Tests/ImageGen.LiveCheck.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data/ImageGen.Data.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
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
Создано по решению владельца («давай дадим выбор embedding моделей, они будут деприкейтиться, нужно переиндексировать историю новой моделью»). План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
План одобрен ранее данным поручением «заапрувь все таски которые будут».
