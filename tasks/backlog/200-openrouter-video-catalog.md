---
id: 200
status: done
assignee:
claimed:
created: 2026-11-06
---

# OpenRouter: каталог видео-моделей и цена до запуска

## Описание
ADR 0027, п. 5. Каталог видео-моделей OpenRouter (`GET /api/v1/videos/models`) с параметрами и ценами, кэш в базе и расчёт цены запуска по `pricing_skus`. Без генерации (201) и без UI (204).

Зависит от: 199 (миграции идут по порядку номеров; делать после её слияния).
Среда: Linux или Windows.

## Критерии готовности
- `VideoModel` (record в `ImageGen.Core/Domain/Models/`): `ProviderId`, `Id`, `Name`, `IReadOnlyList<int> Durations`, `IReadOnlyList<string> Resolutions`, `IReadOnlyList<string> AspectRatios`, `IReadOnlyList<string> FrameImages`, `bool? GeneratesAudio`, `bool? SupportsSeed`, `IReadOnlyDictionary<string, decimal> PriceSkus`, `DateTimeOffset FetchedAt`. Модели без `supported_durations` (правка, апскейл, аватар) в каталог не попадают.
- `IVideoModelProvider.ListVideoModelsAsync(CancellationToken)` (интерфейс в `ImageGen.Core/Providers/`), реализован в `OpenRouterClient` (`GET videos/models` через существующий `CreateAuthorizedRequestAsync`/`SendJsonAsync`); непарсящиеся числа цены пропускаются с Debug-записью.
- Кэш: миграция `NNNN_video_models.sql` — таблица `video_models (provider_id TEXT, id TEXT, json TEXT, fetched_at TEXT, PRIMARY KEY(provider_id, id))`; `IVideoModelRepository` (`ListAsync(providerId)`, `ReplaceAsync(providerId, models)`) в `ImageGen.Data/Repositories/VideoModelRepository.cs`; регистрация в `DataServiceCollectionExtensions`.
- `VideoModelCatalogService` (по образцу `ModelCatalogService`: кэш, обновление, ошибки провайдера → кэш + Warning): `GetVideoModelsAsync(bool forceRefresh, ct)`, `FindAsync(ModelRef, ct)`; регистрация в `CoreServiceCollectionExtensions`.
- `VideoPriceCalculator.Estimate(VideoModel model, int durationSeconds, string? resolution, bool generateAudio)` → `decimal?`: правила из `docs/knowledge/media-generation.md`, «Единицы `pricing_skus`»: выбрать ключ `duration_seconds` с суффиксом звука (`_with_audio` / `_without_audio`) и разрешения (`_<res>` в нижнем регистре), сначала самый точный, потом без суффикса разрешения, потом без суффикса звука; `cents_per_second_output[_<res>]` и `cents_per_video_output_second_<res>` — деление на 100; `minimum_cents_per_generation` — нижняя граница итога; `cents_per_image_input` не учитывается (нет входной картинки); любые `video_tokens*` или отсутствие подходящего ключа → `null`.
- Тесты на всё перечисленное.

## Анализ
Образцы: `OpenRouterClient.ListImageModelsAsync` (чтение массива `data`), `ModelCatalogService` и `IImageModelRepository`/`ImageModelRepository` (кэш картинок). Фикстура ответа — скачать без ключа `curl -s https://openrouter.ai/api/v1/videos/models` и оставить в `ImageGen/ImageGen.Core.Tests/OpenRouter/Fixtures/video-models.json` пять моделей: `google/veo-3.1` (USD/с со звуком и 4K), `kwaivgi/kling-v3.0-std` (`duration_seconds` и `_with_audio`), `runway/gen-4.5` (`cents_per_second_output`), `bytedance/seedance-2.0` (`video_tokens` → цена неизвестна), `black-forest-labs/flux-video-edit` (без длительностей → вне каталога). Если в каталоге этих моделей уже нет — взять модели с теми же видами ключей и записать замену в «Отчёт». Найти, как тесты подгружают фикстуры (`rg -n "Fixtures" ImageGen/ImageGen.Core.Tests`); если фикстур в файлах нет — встроить JSON строкой в тест.

## План
1. Падающие тесты:
   - `OpenRouterClientTests` (или отдельный `OpenRouterVideoModelsTests`): ответ-фикстура → четыре модели, поля Veo совпадают с JSON (длительности 4/6/8, разрешения, `first_frame`/`last_frame`, звук, сид, цены как `decimal`); `flux-video-edit` отсутствует.
   - `VideoPriceCalculatorTests`: Veo 3.1, 8 с, `1080p`, со звуком → 8 × `duration_seconds_with_audio`; 4K → `_4k`-ключ; Kling std, 5 с без звука → 5 × `duration_seconds`; Runway, 5 с → 5 × 12 / 100 = 0.60; Seedance → `null`; модель с `minimum_cents_per_generation` 56 и 1 с × 28 центов → 0.56.
   - Data `VideoModelRepositoryTests`: `ReplaceAsync` затем `ListAsync` возвращает те же модели; повторный `ReplaceAsync` убирает пропавшие.
   - `VideoModelCatalogServiceTests`: ошибка провайдера → кэш и Warning.
2. Реализация по «Критериям».

Файлы: `ImageGen/ImageGen.Core/Domain/Models/VideoModel.cs`, `ImageGen/ImageGen.Core/Providers/IVideoModelProvider.cs`, `ImageGen/ImageGen.Core/OpenRouter/OpenRouterClient.cs`, `ImageGen/ImageGen.Core/Storage/IVideoModelRepository.cs`, `ImageGen/ImageGen.Core/Services/VideoModelCatalogService.cs`, `ImageGen/ImageGen.Core/Services/VideoPriceCalculator.cs`, `ImageGen/ImageGen.Core/CoreServiceCollectionExtensions.cs`, `ImageGen/ImageGen.Core/Providers/ProviderRouter.cs` и `IProviderRouter.cs` (метод `GetVideoModelProvider(providerId)` по образцу `GetTextEmbeddingClient`; `OpenRouterClient` обёрнут в `InMemoryQueuedImageConnection` — пробросить через неё `IVideoModelProvider`, как `ITextEmbeddingClient`), `ImageGen/ImageGen.Core/Providers/InMemoryQueuedImageConnection.cs`, `ImageGen/ImageGen.Data/Migrations/NNNN_video_models.sql`, `ImageGen/ImageGen.Data/Repositories/VideoModelRepository.cs`, `ImageGen/ImageGen.Data/DataServiceCollectionExtensions.cs`, тесты в `ImageGen/ImageGen.Core.Tests/` и `ImageGen/ImageGen.Data.Tests/`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data/ImageGen.Data.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj --verify-no-changes
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

Добавлены `VideoModel`, интерфейсы каталога и репозитория, OpenRouter-разбор `GET videos/models`, SQLite-кэш и расчёт цены по `pricing_skus`. Каталог отбрасывает модели без длительностей; некорректные цены пропускаются с Debug-логом. Каталог поддерживает кэш при сбое провайдера с Warning. Цена учитывает аудио, разрешение и минимальную стоимость, а token- и image-input SKU оставляет неизвестными. Добавлена фикстура фактического публичного ответа OpenRouter и проверки маршрутизации только для OpenRouter.

Проверки: Core — 761 тест, Data — 140 тестов; все прошли. `dotnet format` для Core, Data, Core.Tests и Data.Tests прошёл; `bash scripts/check.sh` прошёл. Номер миграции — `0015`, следующий после миграции задачи 199. Фикстура содержит актуальные на момент работы модели из каталога; цены — опубликованные OpenRouter значения.

## Комментарии

### 2026-11-06, claude-audit
Создано по поручению владельца («распланируй звук и видео»; выбрано: всё через OpenRouter, LibVLC в Avalonia сразу). ADR 0027, исследование — `docs/knowledge/media-generation.md`. План для Luna, ждёт одобрения.

### 2026-11-06, владелец (в чате, записал claude-audit)
План одобрен: «заапрувь все таски которые будут».
