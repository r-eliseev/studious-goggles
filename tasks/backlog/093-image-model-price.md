---
id: 093
status: planned
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
Как сейчас: `ImageModel` цену не хранит; `OpenRouterClient.ListImageModelsAsync` читает только `GET images/models`; `FalImageProvider.ListImageModelsAsync` ставит цену «неизвестно» (решение владельца в 025: цена fal — «неизвестно», пока нет официального источника); у ассистента цена — из `pricing.prompt/completion` общего `GET models` (за токен, на экране за 1 млн). Кэш — таблица `image_models` (`ImageModelRepository`); старые строки без новой колонки уже обрабатывались так: `Parameters == null` → кэш перечитывается (`ModelCatalogService.GetImageModelsAsync`).

OpenRouter — проверено без ключа 2026-10-29 (ответы в `tmp/093/`, не в git):
- `GET /api/v1/images/models` (61 модель) **цены не содержит**; у каждой модели поле `endpoints` = путь `/api/v1/images/models/{id}/endpoints`.
- `GET /api/v1/images/models/{id}/endpoints` (200 без ключа) → `{"id", "endpoints":[{"provider_name", "supported_parameters", "pricing":[{"billable","unit","cost_usd","variant"?}]}]}`. Примеры: `black-forest-labs/flux.2-klein-4b` → `[{"billable":"output_image","unit":"megapixel","cost_usd":0.014}]`; `qwen/qwen-image-2.1-turbo` → `input_image`/`image`/0 и два `output_image`/`image`/0.0184 с `variant` `1k` и `2k`; `openai/gpt-image-1` → `input_image`, `input_text`, `output_image` с `unit` `token` (за токен, 4e-05).
- Сводка по 61 модели: `output_image` за `image` — 66 записей (38 с `variant`: разрешение `768`/`1k`/`2k`/`4k`, качество `low_1k`, `high_resolution`, `style_references`), за `token` — 25, за `megapixel` — 4, за `request` — 1 (надбавка `high_thinking_surcharge`); входные `input_image`, `input_text`, `input_reference`, `input_font`. У 56 моделей один эндпоинт, у 4 — два-три с разной ценой (Google: AI Studio и Vertex), у `meta/muse-image` — ни одного.
- Общий `GET /api/v1/models` тоже перечисляет эти модели с `pricing.image_output`, но в пересчёте на токен (у `qwen/qwen-image-2.1-turbo` 0.0000044 «за токен» при $0.0184 за картинку в эндпоинте) — для показа в единицах провайдера не годится.
- Значит, цена = 1 запрос `endpoints` на модель: при «Refresh» и при пустом кэше ≈ 62 запроса вместо одного. С параллельностью 4 и ~0,2 с на запрос — порядка 3 с под индикатором загрузки (оценка, не замер). Ленивый запрос цены по строке отвергнут: критерий требует кэша вместе с каталогом.

fal — проверено 2026-10-29: `GET https://api.fal.ai/v1/models` без ключа отвечает 200, цены в ответе нет (в `metadata`: `display_name`, `category`, `license_type`, `group`, `tags` и др.). `GET https://api.fal.ai/v1/models/pricing?endpoint_id=…` без ключа → 401 «API key authentication required»; форма успешного ответа **неизвестна** (ключа fal в этой сессии нет). Допущение: в 093 цена fal остаётся «price unknown» (как решил владелец в 025); разбор ответа fal проверяется тестом «цены нет → неизвестно». Чтение цены fal с ключом — отдельная задача после ответа владельца (вопрос в «Комментариях»).

Что показывать (допущение, вопрос в «Комментариях»):
- Только записи `billable = output_image` — цена одной полученной картинки; входные картинки и референсы зависят от вложений и в строке не показываются (хранятся для будущей оценки F4.4).
- Единица: `image` → «$0.04 / image», `megapixel` → «$0.014 / MP», `token` → за 1 млн токенов «$30.00 / 1M output tokens», `request` → «$0.02 / request».
- Несколько вариантов и эндпоинтов: берётся одна единица по приоритету image > megapixel > token > request; если цены в ней различаются — диапазон «$0.008–0.036 / image», иначе одно число. Записи с неизвестной единицей пропускаются.
- Нет ни одной `output_image` (пустые `endpoints`, ошибка запроса, fal, модели из конфига) → «price unknown» (`AssistantPriceUnknown`), без чисел.
- Формат чисел — инвариантный, как у цены ассистента (`0.00##`).

Хранение: новая колонка `prices_json TEXT NULL` в `image_models`, миграция `NNNN_image_model_prices.sql`. NULL = «не сохранено» (строка до миграции) → кэш перечитывается один раз, как было с `Parameters`; пустой список = «провайдер цену не дал».

Пересечения: строка модели (`ModelOptionViewModel`, разметка экрана) — после 092. Ядро и данные (`ImageModel`, `OpenRouterClient`, `FalImageProvider`, `ModelCatalogService`, `ImageModelRepository`, миграция) с 090–092 не пересекаются и могут идти раньше. Если параллельно появится другая миграция, номер берётся следующим свободным в момент реализации. Экран «Other model…» (`ModelPickerPageModel`) тоже показывает модели картинок — цену туда не добавляем (вне критериев).

## План
Ядро (`ImageGen.Core`):
- `Domain/Models/ImagePriceUnit` — перечисление `Image`, `Megapixel`, `Token`, `Request`.
- `Domain/Models/ImageModelPrice` — запись: `Billable` (строка провайдера, например `output_image`), `Unit`, `CostUsd` (decimal, за единицу провайдера, у токена — за 1 токен), `Variant` (строка или null).
- `ImageModel.Prices: IReadOnlyList<ImageModelPrice>?` — null = не сохранено, пусто = цены нет.
- `OpenRouterClient.ListImageModelsAsync`: после списка для каждой модели `GET images/models/{id}/endpoints` (id с `/` как есть, без экранирования), не больше 4 запросов одновременно (константа); разбор `endpoints[*].pricing[*]`, неизвестные `unit` пропускаются; ошибка запроса одной модели (`ProviderException`, плохой JSON) → `Prices = []` и предупреждение в лог, остальные модели не страдают; отмена пробрасывается.
- `FalImageProvider` и `OpenAiCompatibleClient`: `Prices = []`.
- `ModelCatalogService.GetImageModelsAsync`: кэш годен, только если у всех строк `Parameters` и `Prices` не null.
- `Serialization/CoreJsonContext`: `List<ImageModelPrice>`.

Данные (`ImageGen.Data`): миграция `Migrations/NNNN_image_model_prices.sql` (`ALTER TABLE image_models ADD COLUMN prices_json TEXT NULL`); `ImageModelRepository` пишет и читает `prices_json` параметром; JSON — через `CoreJsonContext`, рядом с `ParameterJson` (тот же приём «NULL → null»).

Представление (`ImageGen.Presentation`):
- `PageModels/ImagePriceText` (статический): `Format(IReadOnlyList<ImageModelPrice>?)` → строка по правилам из «Анализа».
- `ModelOptionViewModel`: `PriceText`; `DetailsText` = «id · seed · цена».
- `AppResources`: `ImagePricePerImageFormat` («${0} / image»), `ImagePricePerMegapixelFormat` («${0} / MP»), `ImagePricePerMillionTokensFormat` («${0} / 1M output tokens»), `ImagePricePerRequestFormat` («${0} / request»); «price unknown» — существующий `AssistantPriceUnknown`.
- Разметка обеих оболочек: если цена входит в `DetailsText`, правок нет; иначе — строка `PriceText` под названием в шаблоне строки (`Controls/SessionModelsView.xaml`, `SessionModelsView.axaml`).

Шаги (TDD; ответы в тестах — выдержки из сохранённых 2026-10-29 ответов, внутри теста, как в существующих `OpenRouterClientTests`):
1. `OpenRouterClientTests` (сначала падающие):
   - список из двух моделей + эндпоинты `flux.2-klein-4b` и `qwen-image-2.1-turbo` → у первой одна цена `output_image`/`Megapixel`/0.014, у второй `input_image` 0 и два `output_image`/`Image`/0.0184 с вариантами `1k`, `2k`;
   - адрес запроса цены — `https://openrouter.ai/api/v1/images/models/black-forest-labs/flux.2-klein-4b/endpoints`;
   - эндпоинты с `unit: token` (`openai/gpt-image-1`) → `Token`, 0.00004;
   - `{"endpoints":[]}` (`meta/muse-image`) → пустой список;
   - эндпоинты одной модели отвечают 500 → у неё пусто, у другой цены есть, список возвращён;
   - запись с `unit: "second"` → пропущена, остальные на месте;
   - одновременно не больше 4 запросов `endpoints` (заглушка считает);
   - отмена во время запросов цены → `OperationCanceledException`.
2. `FalImageProviderTests`: сохранённый ответ списка fal (выдержка с `metadata`) → `Prices` пустой, не null.
3. `ModelCatalogServiceTests`: кэш со строками `Prices == null` → перечитывается у провайдера; с `Prices = []` → берётся из кэша.
4. Data: `MigrationsTests`/`ProviderMigrationTests` — БД до миграции со строкой каталога → после миграции колонка есть, строка читается с `Prices == null`; тест репозитория — круг записи и чтения: null → null, [] → [], цены с вариантом и без.
5. `ImagePriceTextTests` (Presentation): одна цена за картинку → «$0.04 / image»; варианты 0.008…0.036 → «$0.008–0.036 / image»; одинаковые варианты → одно число; мегапиксель → «$0.014 / MP»; токен 0.00003 → «$30.00 / 1M output tokens»; картинка и надбавка за запрос → показана картинка; только `input_*` → «price unknown»; null и пусто → «price unknown»; под культурой `ru-RU` разделитель — точка.
6. `ModelOptionViewModel`: `DetailsText` содержит цену или «price unknown».
7. Avalonia: `OpenRouterFakes` отвечает на `…/endpoints` (одна модель с ценой, одна без); headless-тест: в строке модели на экране моделей видны «$0.014 / MP» и «price unknown».
8. Документация: `docs/specs/chat-sessions.md` S5 — формат цены моделей картинок, источник (эндпоинты OpenRouter), fal — «price unknown»; `docs/architecture.md` — `Domain/Models`: `ImageModelPrice`, `ImagePriceUnit`; `docs/specs/image-client.md` таблица провайдеров: у OpenRouter цена в `images/models/{id}/endpoints`, а не в списке (факт с датой); `docs/status.md` — открытый вопрос о форме цены fal. Ручные пункты в 016 и 046 (цена видна после «Refresh»).

Проверка: `dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj`, `dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj`, `dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj`, `dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj`, `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android`, `dotnet format … --verify-no-changes` по изменённым проектам, `scripts/check.sh`. Живая проверка цены у OpenRouter — без ключа доступна; у fal — нет.

Файлы: `ImageGen/ImageGen.Core/Domain/Models/ImageModel.cs`, `ImageGen/ImageGen.Core/Domain/Models/ImageModelPrice.cs`, `ImageGen/ImageGen.Core/Domain/Models/ImagePriceUnit.cs` (новые), `ImageGen/ImageGen.Core/OpenRouter/OpenRouterClient.cs`, `ImageGen/ImageGen.Core/Fal/FalImageProvider.cs`, `ImageGen/ImageGen.Core/OpenAiCompatible/OpenAiCompatibleClient.cs`, `ImageGen/ImageGen.Core/Services/ModelCatalogService.cs`, `ImageGen/ImageGen.Core/Serialization/CoreJsonContext.cs`, `ImageGen/ImageGen.Data/Migrations/NNNN_image_model_prices.sql` (новый), `ImageGen/ImageGen.Data/Repositories/ImageModelRepository.cs`, `ImageGen/ImageGen.Data/ParameterJson.cs` (или новый `PriceJson.cs`), `ImageGen/ImageGen.Presentation/PageModels/ImagePriceText.cs` (новый), `ImageGen/ImageGen.Presentation/PageModels/ModelOptionViewModel.cs`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx` (и `.cs`), при необходимости `ImageGen/ImageGen.Maui/Controls/SessionModelsView.xaml` и `ImageGen/ImageGen.Avalonia/Views/SessionModelsView.axaml`; тесты: `ImageGen/ImageGen.Core.Tests/OpenRouter/OpenRouterClientTests.cs`, `ImageGen/ImageGen.Core.Tests/Fal/FalImageProviderTests.cs`, `ImageGen/ImageGen.Core.Tests/Services/ModelCatalogServiceTests.cs`, `ImageGen/ImageGen.Data.Tests/MigrationsTests.cs`, `ImageGen/ImageGen.Data.Tests/Repositories/ImageModelRepositoryTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/ImagePriceTextTests.cs` (новый), `ImageGen/ImageGen.Avalonia.Tests/TestDoubles/OpenRouterFakes.cs`, `ImageGen/ImageGen.Avalonia.Tests/Flows/SessionModelsFlowTests.cs`; `docs/specs/chat-sessions.md`, `docs/specs/image-client.md`, `docs/architecture.md`, `docs/status.md`, `tasks/v0.1/016-*.md`, `tasks/backlog/046-*.md`.

## Отчёт

## Комментарии
### 2026-10-29, claude-plan
Вопросы владельцу по плану (до ответа план идёт по допущениям):
1. У многих моделей несколько цен вывода (разрешение `1k`/`2k`/`4k`, качество, несколько эндпоинтов у Google). Допущение: показывать диапазон в одной единице («$0.008–0.036 / image»). Вариант: только минимальную («from $0.008 / image») или цену варианта, совпадающего с параметрами сессии.
2. Цены входных картинок и референсов в строке не показываются (зависят от вложений), только хранятся. Подходит?
3. fal: в списке моделей цены нет, отдельный `GET /v1/models/pricing` требует ключ (401 без ключа), форма ответа не проверена. Допущение: в 093 у fal «price unknown» (как решено в 025). Нужна ли отдельная задача «цена fal по ключу» с живой проверкой?
4. Обновление каталога OpenRouter станет ≈62 запроса вместо одного (цена только в `images/models/{id}/endpoints`), порядка нескольких секунд при «Refresh». Допустимо?
