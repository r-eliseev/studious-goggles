---
id: 093
status: approved
assignee:
claimed:
created: 2026-10-29
---

# Цена моделей картинок в списке моделей

## Описание
Замечание владельца (2026-10-29): у моделей ассистента в списке видна цена («3.00 in, 6.00 out per 1M tokens»), у моделей картинок нет. Нужно показывать цену и у моделей картинок.

Что известно: `ImageModel` (`ImageGen.Core/Domain/Models/ImageModel.cs`) цену не хранит; у ассистента цена берётся из поля `pricing` каталога OpenRouter (`OpenRouterClient`). В каком виде OpenRouter и fal отдают цену моделей картинок (за картинку, за мегапиксель, за токены вывода), неизвестно: выяснить в «Анализе», при необходимости пробным кодом в `poc/`. Скорее всего нужна новая колонка в кэше каталога (миграция `NNNN_<смысл>.sql`).

Зависит от: 092 (та же строка модели в списке; ядро и данные можно делать раньше).
Среда: Linux или Windows.

## Критерии готовности
- В строке модели картинок видна цена в единицах провайдера (например «$0.014 / image» или «$X / MP»); если провайдер цену не дал, то «price unknown», без выдуманных чисел.
- Цена кэшируется вместе с каталогом и обновляется по «Refresh».
- Формат цены описан в спецификации экрана моделей; тесты разбора каталога OpenRouter и fal на сохранённых ответах.

## Анализ
Как сейчас:
- `ImageModel` цену не хранит.
- `OpenRouterClient.ListImageModelsAsync` читает только `GET images/models`.
- `FalImageProvider` цену не заявляет (решение владельца в 025: цена fal «неизвестно», пока нет официального источника).
- Цена ассистента берётся из `pricing.prompt/completion` общего `GET models`.
- Кэш — таблица `image_models` (`ImageModelRepository`).

OpenRouter, проверено без ключа 2026-10-29:
- `GET /api/v1/images/models` (61 модель) **цены не содержит**.
- Цена есть в `GET /api/v1/images/models/{id}/endpoints` (200 без ключа). Форма ответа: `{"id","endpoints":[{"provider_name",…,"pricing":[{"billable","unit","cost_usd","variant"?}]}]}`. Сохранённые выдержки (используй их в тестах дословно):
  - `black-forest-labs/flux.2-klein-4b`: `"pricing":[{"billable":"output_image","unit":"megapixel","cost_usd":0.014}]`
  - `qwen/qwen-image-2.1-turbo`: `"pricing":[{"billable":"input_image","unit":"image","cost_usd":0},{"billable":"output_image","unit":"image","cost_usd":0.0184,"variant":"1k"},{"billable":"output_image","unit":"image","cost_usd":0.0184,"variant":"2k"}]`
  - `openai/gpt-image-1`: `"pricing":[{"billable":"input_image","unit":"token","cost_usd":0.00001},{"billable":"input_text","unit":"token","cost_usd":0.000005},{"billable":"output_image","unit":"token","cost_usd":0.00004}]`
  - `black-forest-labs/flux-3-image-klein`: `output_image`/`image` с вариантами `768` 0.008, `1k` 0.009, `1.5k` 0.011, `2k` 0.014, `4k` 0.036
  - `krea/k3-edit`: `output_image`/`image` 0.03 и `2k` 0.06, `output_image`/`request` 0.02 `high_thinking_surcharge`, `input_image`/`image` 0.02
  - `meta/muse-image`: `{"id":"meta/muse-image","endpoints":[]}`
- Единицы во всём каталоге: `image`, `megapixel`, `token`, `request`.
- Общий `GET /api/v1/models` даёт цены картинок в пересчёте на токен, для показа не годится.

fal, проверено 2026-10-29: в списке моделей цены нет. `GET https://api.fal.ai/v1/models/pricing?endpoint_id=…` без ключа отвечает 401; форма успешного ответа неизвестна.

Решения плана (допущения из комментариев claude-plan, приняты в плане):
- В строке показываются только записи `output_image`. Входные цены хранятся, но не показываются.
- Единица выбирается по приоритету image > megapixel > token > request. Если цены в этой единице различаются, показывается диапазон «$0.008–0.036 / image», иначе одно число. Токены показываются за 1 млн. Записи с другими единицами пропускаются.
- Нет `output_image` → «price unknown». Это касается пустых `endpoints`, ошибки запроса, fal и моделей из конфига.
- У fal в этой задаче «price unknown». Неизвестная форма цены fal записывается открытым вопросом в `docs/status.md`.
- Цены OpenRouter читаются при загрузке каталога: по одному запросу `endpoints` на модель, не больше 4 одновременно (около 62 запросов на «Refresh» — принято).
- Хранение: колонка `prices_json` в `image_models`. Миграция очищает кэш каталога (`DELETE FROM image_models`): это только кэш, и он перечитается при следующем открытии. Поэтому `ImageModel.Prices` не nullable, по умолчанию пустой.
- Формат чисел инвариантный, `0.00##`, как у цены ассистента.

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов, членов, ключей строк и тестов переноси дословно. Если шаг не получается так, как написано (API другое, компилятор не принимает разметку), не придумывай обход: опиши в «Комментариях», что пробовал, поставь `blocked` и остановись.

Выполнять после: 092 (та же строка модели и `ModelOptionViewModel`). Шаги 1–6 (ядро и данные) с 090–092 не пересекаются и могут идти раньше. Шаг 7 — только после 092 `done`; иначе поставь `blocked` с комментарием «ждёт 092».

Прочитай перед началом:
- «Анализ» выше.
- `ImageGen/ImageGen.Core/OpenRouter/OpenRouterClient.cs` (`ListImageModelsAsync`, `ReadNumber`, `CreateAuthorizedRequestAsync`, `SendJsonAsync`) и `ImageGen/ImageGen.Core/Providers/ProviderClientBase.cs` (какие ошибки становятся `ProviderException`).
- `ImageGen/ImageGen.Core/Domain/Models/ImageModel.cs`, `ImageGen/ImageGen.Core/Serialization/CoreJsonContext.cs`, `ImageGen/ImageGen.Data/ParameterJson.cs`, `ImageGen/ImageGen.Data/Repositories/ImageModelRepository.cs`, `ImageGen/ImageGen.Data/Migrations/0003_generation_parameters.sql`.
- Тесты-образцы: `ImageGen/ImageGen.Core.Tests/OpenRouter/OpenRouterClientTests.cs`, `ImageGen/ImageGen.Core.Tests/Fal/FalImageProviderTests.cs` (заглушка по пути запроса), `ImageGen/ImageGen.Data.Tests/MigrationsTests.cs`, `ImageGen/ImageGen.Data.Tests/Repositories/ImageModelRepositoryTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/AssistantModelOptionViewModelTests.cs` (тест культуры), `ModelOptionViewModelTests.cs`.

1. Типы ядра (тестов не требует, проверка — сборка).
   - Новый файл `ImageGen/ImageGen.Core/Domain/Models/ImagePriceUnit.cs`: `public enum ImagePriceUnit { Image = 0, Megapixel = 1, Token = 2, Request = 3 }`. Номера не менять никогда: они хранятся в JSON.
   - Новый файл `ImageGen/ImageGen.Core/Domain/Models/ImageModelPrice.cs`: `public sealed record ImageModelPrice(string Billable, ImagePriceUnit Unit, decimal CostUsd, string? Variant);`. XML-комментарий: `CostUsd` — за одну единицу провайдера, у `Token` — за один токен.
   - В `ImageModel` — `public IReadOnlyList<ImageModelPrice> Prices { get; set; } = [];` с комментарием «Prices from the provider's catalog; empty when the provider gives none».
   - В `CoreJsonContext` — `[JsonSerializable(typeof(List<ImageModelPrice>))]`.
2. Потокобезопасные тестовые заглушки. В `ImageGen/ImageGen.Core.Tests/TestDoubles/StubHttpMessageHandler.cs` и `ImageGen/ImageGen.Avalonia.Tests/TestDoubles/StubHttpMessageHandler.cs`, в `SendAsync`: сначала прочитать тело запроса, затем под `lock` по приватному объекту добавить запрос в `Requests` и тело в `RequestBodies`. Причина: запросы цены идут параллельно. Все тесты зелёные.
3. Разбор цены OpenRouter (сначала тесты в `OpenRouterClientTests`). Заглушка — `new StubHttpMessageHandler(req => …)`: путь, оканчивающийся на `/endpoints`, получает выдержку нужной модели (выбор по `AbsolutePath`), путь `/images/models` — список.
   - `ListImageModelsAsync_Endpoints_ParsesPrices` — список из `black-forest-labs/flux.2-klein-4b` и `qwen/qwen-image-2.1-turbo` с выдержками из «Анализа». У первой одна цена (`output_image`, `Megapixel`, 0.014, `null`); у второй три: `input_image`/`Image`/0/`null`, `output_image`/`Image`/0.0184/`1k`, `output_image`/`Image`/0.0184/`2k`.
   - `ListImageModelsAsync_EndpointsAddress_KeepsSlashInId` — среди запросов есть `https://openrouter.ai/api/v1/images/models/black-forest-labs/flux.2-klein-4b/endpoints`; первый запрос — `…/images/models`.
   - `ListImageModelsAsync_TokenUnit_ParsesPerToken` — выдержка `openai/gpt-image-1` → есть `output_image`/`Token`/0.00004.
   - `ListImageModelsAsync_NoEndpoints_EmptyPrices` — выдержка `meta/muse-image` → `Prices` пуст.
   - `ListImageModelsAsync_EndpointsFail_OtherModelsKeepPrices` — эндпоинты первой модели отвечают 500, второй — выдержкой → у первой `Prices` пуст, у второй цены есть, исключения нет.
   - `ListImageModelsAsync_UnknownUnit_SkipsEntry` — в `pricing` добавлена запись `{"billable":"output_image","unit":"second","cost_usd":1}` → её нет, остальные на месте.
   - `ListImageModelsAsync_Cancelled_Throws` — токен отменён до вызова → `Assert.ThrowsAnyAsync<OperationCanceledException>`.
   - Реализация в `OpenRouterClient`:
     - новый приватный метод `private async Task<IReadOnlyList<ImageModelPrice>> ReadPricesAsync(string modelId, CancellationToken cancellationToken)`. Запрос `GET` на относительный путь `"images/models/" + modelId + "/endpoints"` (id не экранировать) через `CreateAuthorizedRequestAsync` и `SendJsonAsync`. Обходит все `endpoints[*].pricing[*]`. Единица — `unit`: `image`, `megapixel`, `token`, `request`, иначе запись пропускается. Цена — `cost_usd`, читается сразу как `decimal` (`GetValue<decimal>()`); если поле не число, запись пропускается. `variant` — строка или `null`. `ProviderException` перехватывается, и метод возвращает `[]`; отмена пробрасывается.
     - в `ListImageModelsAsync`, после сборки списка: `Parallel.ForEachAsync` по списку с `MaxDegreeOfParallelism = 4` (приватная константа `PriceRequestParallelism = 4`) и токеном отмены; каждой модели присваивается `Prices = await ReadPricesAsync(...)`.
   - Существующие тесты `ListImageModelsAsync_*`, которые отвечают одним JSON на любой запрос, должны остаться зелёными: у их моделей `Prices` пуст. Если какой-то из них проверяет число запросов, поменяй ожидание на «1 + число моделей» и запиши это в «Отчёт».
4. fal и модели из конфига. Тест в `FalImageProviderTests`: `ListImageModels_NoPriceInCatalog_EmptyPrices` — существующий ответ списка (`fal-ai/a`, `fal-ai/b`) → `Prices` пуст у обеих. Тест в `OpenAiCompatibleClientTests`: `ListImageModelsAsync_ConfiguredModels_EmptyPrices`. Код не меняется: значение по умолчанию уже `[]`. Если тест падает — поставь `Prices = []` явно.
5. Данные (сначала тесты).
   - Новая миграция `ImageGen/ImageGen.Data/Migrations/NNNN_image_model_prices.sql`, где `NNNN` — следующий свободный номер в папке на момент работы. Содержимое:
     - комментарий «Task 093: image model prices. The catalog is a cache: old rows are dropped and reloaded on the next open.»;
     - `ALTER TABLE image_models ADD COLUMN prices_json TEXT NULL;`
     - `DELETE FROM image_models;`
   - Тест в `MigrationsTests`: `All_ImageModelPricesMigration_AddsColumnAndClearsCache` — в `Migrations.All` есть миграция, чьё имя оканчивается на `_image_model_prices.sql`; её `Script` содержит `ADD COLUMN prices_json` и `DELETE FROM image_models`.
   - В `ImageGen/ImageGen.Data/ParameterJson.cs`:
     - `public static string Prices(IReadOnlyList<ImageModelPrice> prices)` — сериализация через `CoreJsonContext.Default.ListImageModelPrice`;
     - `public static IReadOnlyList<ImageModelPrice> ReadPrices(string? json)` — `null` → `[]`.
   - `ImageModelRepository`: в `INSERT` добавить колонку и параметр `@prices_json`; в `SELECT` — колонку `prices_json`, в объект — `Prices = ParameterJson.ReadPrices(...)`.
   - Тесты в `ImageModelRepositoryTests`:
     - `ReplaceAllAsync_Prices_RoundTrip` — модель с ценами `output_image`/`Image`/0.0184/`1k` и `input_image`/`Token`/0.00001/`null` читается обратно с теми же значениями;
     - `ReplaceAllAsync_NoPrices_ReadsEmpty` — `Prices` пуст.
6. Формат цены (сначала тесты). Строки (в `AppResources.resx` и свойства в `AppResources.cs`):
   - `ImagePricePerImageFormat` = «${0} / image»;
   - `ImagePricePerMegapixelFormat` = «${0} / MP»;
   - `ImagePricePerMillionTokensFormat` = «${0} / 1M output tokens»;
   - `ImagePricePerRequestFormat` = «${0} / request»;
   - «price unknown» — существующий `AssistantPriceUnknown`.
   - Новый файл `ImageGen/ImageGen.Presentation/PageModels/ImagePriceText.cs`: `public static class ImagePriceText` с методом `public static string Format(IReadOnlyList<ImageModelPrice> prices)`:
     - берутся записи с `Billable == "output_image"` (`StringComparison.Ordinal`); нет таких → `AppResources.AssistantPriceUnknown`;
     - единица — первая из `Image`, `Megapixel`, `Token`, `Request`, у которой есть запись;
     - значения — `CostUsd` записей этой единицы, у `Token` умноженные на 1 000 000;
     - число — `ToString("0.00##", CultureInfo.InvariantCulture)`; при `min != max` — `"{min}–{max}"` (между ними символ U+2013), иначе одно число;
     - результат — `string.Format(CultureInfo.InvariantCulture, <формат единицы>, число)`.
   - Тесты в новом файле `ImageGen/ImageGen.Presentation.Tests/PageModels/ImagePriceTextTests.cs`:
     - `Format_OnePerImage_ShowsPrice` — 0.04 `Image` → «$0.04 / image»;
     - `Format_Variants_ShowsRange` — цены flux-3-image-klein из «Анализа» → «$0.008–0.036 / image»;
     - `Format_EqualVariants_ShowsOneNumber` — qwen-image-2.1-turbo → «$0.0184 / image»;
     - `Format_Megapixel_ShowsPerMp` — klein 4b → «$0.014 / MP»;
     - `Format_Token_ShowsPerMillion` — gpt-image-1 → «$40.00 / 1M output tokens»;
     - `Format_ImageAndRequest_PrefersImage` — k3-edit → «$0.03–0.06 / image»;
     - `Format_OnlyInputs_Unknown` — только `input_image` → «price unknown»;
     - `Format_Empty_Unknown`;
     - `Format_PolishCulture_UsesPoint` — под культурой `pl-PL` (как в `AssistantModelOptionViewModelTests`) 0.014 `Megapixel` → «$0.014 / MP».
7. Цена в строке модели (после 092). `ModelOptionViewModel.DetailsText` = `model.Id + " · " + <seed-текст> + " · " + ImagePriceText.Format(model.Prices)`; новое свойство `public string PriceText { get; }` = `ImagePriceText.Format(model.Prices)`.
   - В `ModelOptionViewModelTests` обнови ожидания: «vendor/alpha · seed · price unknown» и «vendor/beta · no seed · price unknown».
   - Новый тест `DetailsText_WithPrice_ShowsPrice` — модель с ценой klein 4b → «… · $0.014 / MP».
   - Разметка оболочек не меняется: `DetailsText` уже выводится в строке модели в `ImageGen/ImageGen.Maui/Controls/SessionModelsView.xaml` и `ImageGen/ImageGen.Avalonia/Views/SessionModelsView.axaml`.
8. Avalonia, сквозная проверка. В `OpenRouterFakes.Create` добавь ответ на путь, оканчивающийся на `/endpoints`: для `black-forest-labs/flux.2-klein-4b` — выдержка из «Анализа», для остальных — `{"endpoints":[]}`. Ветку поставь до ветки `/images/models`. Тест в `SessionModelsFlowTests`: `ImageModelRow_ShowsPrice` — на экране моделей есть `TextBlock` с текстом, содержащим «$0.014 / MP», и `TextBlock` с текстом, содержащим «price unknown» (строка «Beta Pro»; группа «vendor» перед проверкой раскрыта).
9. Документация.
   - `docs/specs/chat-sessions.md`, раздел S5: пункт S5.6 — формат цены моделей картинок (правила из «Анализа», примеры «$0.014 / MP», «$0.008–0.036 / image», «$40.00 / 1M output tokens», «price unknown»). Источник — `images/models/{id}/endpoints` OpenRouter. У fal — «price unknown». Цена обновляется по «Refresh».
   - `docs/specs/image-client.md`, таблица провайдеров, строка «Каталог моделей», колонка OpenRouter: допиши «цены — в `GET /api/v1/images/models/{id}/endpoints` (поле `pricing`: billable, unit, cost_usd, variant), в самом списке цен нет (проверено 2026-10-29)».
   - `docs/architecture.md`, строка `Domain/Models/`: добавь `ImageModelPrice`, `ImagePriceUnit` (цена из каталога).
   - `docs/status.md`, «Открытые вопросы»: «Цена моделей fal (задача 093): в списке моделей цены нет, `GET https://api.fal.ai/v1/models/pricing` без ключа отвечает 401, форма ответа неизвестна; у fal показывается „price unknown“. Проверить с ключом fal».
10. Ручные пункты (см. «Ручная проверка») допиши в задачи 016 и 046.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data/ImageGen.Data.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj --verify-no-changes
bash scripts/check.sh
```
Живая проверка без ключа (необязательна): `curl -s https://openrouter.ai/api/v1/images/models/black-forest-labs/flux.2-klein-4b/endpoints` — в ответе есть `"pricing"`. Если формы ответа больше нет, поставь `blocked` и опиши в «Комментариях».

### Ручная проверка
- В задачу 016 (MAUI под Windows): после «Refresh» у моделей OpenRouter в строке видна цена («$0.014 / MP» у FLUX.2 Klein 4B), у моделей fal — «price unknown»; повторное открытие экрана берёт цены из кэша без задержки.
- В задачу 046 (Avalonia): то же.
- Android — в «Отчёт» этой задачи: «не проверено, проверить владельцу: время „Refresh“ на телефоне».

Файлы: `ImageGen/ImageGen.Core/Domain/Models/ImagePriceUnit.cs`, `ImageGen/ImageGen.Core/Domain/Models/ImageModelPrice.cs` (новые), `ImageGen/ImageGen.Core/Domain/Models/ImageModel.cs`, `ImageGen/ImageGen.Core/Serialization/CoreJsonContext.cs`, `ImageGen/ImageGen.Core/OpenRouter/OpenRouterClient.cs`, `ImageGen/ImageGen.Data/Migrations/NNNN_image_model_prices.sql` (новый), `ImageGen/ImageGen.Data/ParameterJson.cs`, `ImageGen/ImageGen.Data/Repositories/ImageModelRepository.cs`, `ImageGen/ImageGen.Presentation/PageModels/ImagePriceText.cs` (новый), `ImageGen/ImageGen.Presentation/PageModels/ModelOptionViewModel.cs`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.cs`; тесты: `ImageGen/ImageGen.Core.Tests/TestDoubles/StubHttpMessageHandler.cs`, `ImageGen/ImageGen.Core.Tests/OpenRouter/OpenRouterClientTests.cs`, `ImageGen/ImageGen.Core.Tests/Fal/FalImageProviderTests.cs`, `ImageGen/ImageGen.Core.Tests/OpenAiCompatible/OpenAiCompatibleClientTests.cs`, `ImageGen/ImageGen.Data.Tests/MigrationsTests.cs`, `ImageGen/ImageGen.Data.Tests/Repositories/ImageModelRepositoryTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/ImagePriceTextTests.cs` (новый), `ImageGen/ImageGen.Presentation.Tests/PageModels/ModelOptionViewModelTests.cs`, `ImageGen/ImageGen.Avalonia.Tests/TestDoubles/StubHttpMessageHandler.cs`, `ImageGen/ImageGen.Avalonia.Tests/TestDoubles/OpenRouterFakes.cs`, `ImageGen/ImageGen.Avalonia.Tests/Flows/SessionModelsFlowTests.cs`; `docs/specs/chat-sessions.md`, `docs/specs/image-client.md`, `docs/architecture.md`, `docs/status.md`, `tasks/v0.1/016-*.md`, `tasks/backlog/046-*.md`.

## Отчёт

## Комментарии
### 2026-10-29, claude-plan
Вопросы владельцу по плану (до ответа план идёт по допущениям):
1. У многих моделей несколько цен вывода (разрешение `1k`/`2k`/`4k`, качество, несколько эндпоинтов у Google). Допущение: показывать диапазон в одной единице («$0.008–0.036 / image»). Вариант: только минимальную («from $0.008 / image») или цену варианта, совпадающего с параметрами сессии.
2. Цены входных картинок и референсов в строке не показываются (зависят от вложений), только хранятся. Подходит?
3. fal: в списке моделей цены нет, отдельный `GET /v1/models/pricing` требует ключ (401 без ключа), форма ответа не проверена. Допущение: в 093 у fal «price unknown» (как решено в 025). Нужна ли отдельная задача «цена fal по ключу» с живой проверкой?
4. Обновление каталога OpenRouter станет ≈62 запроса вместо одного (цена только в `images/models/{id}/endpoints`), порядка нескольких секунд при «Refresh». Допустимо?

### 2026-10-29, владелец (в чате, записал claude-plan)
План одобрен; допущения из комментариев claude-plan приняты как решения. План переписан под небольшую модель.
