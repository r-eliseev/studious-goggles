---
id: 020
status: planned
assignee:
claimed:
created: 2026-10-27
---

# Параметры генерации в настройках сессии

## Описание
Соотношение сторон, разрешение, формат в настройках сессии; перевод в параметры каждой модели и показ замен и отброшенного (F4.2, F4.3, таблица «Применение параметров к моделям» в `docs/specs/image-client.md`).

Детализировать (точные файлы, код, тесты, как в задачах v0.1) после задачи 016, когда будет код скелета.

## Критерии готовности
- Замены и отброшенные параметры видны на карточке генерации и сохраняются в записи генерации.

## Анализ

Источники: `docs/specs/image-client.md` (F4.2, F4.3, таблица «Применение параметров к моделям», сущности), `docs/specs/chat-sessions.md` (S2.2, S3, S5), ADR 0004, код `main` (f58be2d). Каталог OpenRouter снят 2026-10-28 без ключа: `GET https://openrouter.ai/api/v1/images/models` (61 модель) и `…/images/models/black-forest-labs/flux.2-klein-4b/endpoints`. Копии лежат в `tmp/` (в git не попадают).

### Что уже есть в коде
- `SessionSettings`: `ImageModelIds`, `RunsPerModel`, `AssistantModelId`. Параметров генерации нет.
- `ImageGenerationRequest(ModelId, Prompt, Seed)`. `OpenRouterClient.GenerateAsync` шлёт `POST images` только с `model`, `prompt`, `n: 1` и `seed` (если не null).
- `ImageModel`: только `SupportsSeed`, `SupportsImageInput`, `MaxImagesPerRequest` (из `n.max`). Полный `supported_parameters` не сохраняется ни в модели, ни в таблице `image_models`. Допустимые значения `aspect_ratio`, `resolution`, `output_format` из кеша сейчас не узнать.
- `Generation` хранит `SeedApplied`, но не применённые и отброшенные остальные параметры. Карточка (`GenerationItemViewModel.DetailsOf`) показывает модель, сид или «seed not supported» и цену.
- `SessionService.UpdateSettingsAsync` и `SessionModelsPageModel.SaveAsync` собирают `SessionSettings` заново из трёх полей. Новое поле настроек при сохранении экрана моделей **потеряется**, если его не учесть.
- `GenerationService.RepeatAsync`, `NewSeedAsync`, `EditAsync` берут модель и сид из генерации-источника. Параметров прогона нигде не хранится.
- Вход на экран моделей: `ChatPageModel` (команда, строка 143, переход `Routes.SessionModelsFor`).

### Каталог OpenRouter (снимок 2026-10-28)
- `supported_parameters` во всех 61 моделях — объект (как в фикстуре `OpenRouterClientTests`). Значения: `{"type":"enum","values":[…]}`, `{"type":"range","min","max"}`, `{"type":"boolean"}`.
- Ключи: `aspect_ratio` (60 моделей, enum; часто есть `auto`), `resolution` (27, enum: `1K`, `1.5K`, `2K`, `4K`, а также `512`, `768`), `output_format` (12, enum: `png`, `jpeg`, `webp`, `svg`), `quality` (9), `background` (10), `output_compression` (8, range 0–100), `size` (1, только `boolean`, допустимых значений нет), `seed` (17, boolean), `n` (56, range), `input_references` (59, range, min 0 или 1, max от 1 до 20).
- `black-forest-labs/flux.2-klein-4b` (модель по умолчанию): `aspect_ratio` [1:1, 4:3, 3:4, 3:2, 2:3, 16:9, 9:16, 21:9, auto], `output_format` [png, jpeg], `seed` да, `resolution` нет, `n` 1. Цена $0.014 за мегапиксель (из `endpoints`).
- Наборы `aspect_ratio` разные: у одних есть `auto`, у других нет; у некоторых нет 1:1 или есть только несколько значений. Пример для теста: `["1:1","3:2","2:3","auto"]`.
- Эндпоинт `…/endpoints` отдаёт те же `supported_parameters` по каждому провайдеру, плюс `allowed_passthrough_parameters` (steps, guidance, safety_tolerance, это F4.6, вне задачи) и цену по единице.
- Расхождение, которое не трогаем: `SupportsImageInput` берётся из `architecture.input_modalities`. У `meta/muse-image` в modalities есть `image`, а `input_references` нет. Для референсов (F4.5, позже) правильнее `input_references.max`.
- **Неизвестно:** форма тела `POST /api/v1/images` для `aspect_ratio`, `resolution`, `output_format` (верхний уровень или вложенный объект; принимает ли `resolution` значения вида `1K`). Проверяется только одним платным запросом.
- **Неизвестно:** что значат `512` и `768` у `resolution` (пиксели стороны или другая единица). От этого зависит, как сравнивать их с `1K`.

### Что требует спецификация
- F4.2: общие для всех моделей соотношение сторон, разрешение, формат (количество и seed указаны там же, см. решения ниже).
- F4.3 и таблица «Применение параметров к моделям»: для каждой модели видно, что применено, что заменено ближайшим допустимым значением и что отброшено. Пометка на карточке и сохранение в записи генерации.
- Критерий задачи: замены и отброшенное видны на карточке и сохраняются в записи генерации.

### Решения, которые принимает план
1. **Количество картинок не становится новым параметром.** В F4.2 и таблице это `RunsPerModel`: уже есть в настройках, каждый запуск даёт одну картинку с общим сидом. Вопрос владельцу ниже.
2. **Seed остаётся отдельно** (`SeedApplied` и сид прогона, как сейчас). В `GenerationParameters` его нет.
3. **В объём 020 входят три параметра из описания задачи:** `aspect_ratio`, `resolution`, `output_format`. Качество, фон, сжатие, референсы и пиксельный размер (`size`) — вне задачи.
4. **Параметры живут в настройках сессии**, как и модели: `SessionSettings.Parameters`. Смена модели меняет только план следующих прогонов. Уже созданные прогоны и их план не меняются.
5. **Что отправлено, решает план прогона.** План записывается в каждую генерацию (`Outcomes`), и в API уходит ровно принятая его часть. Так запись и отправка не расходятся (F6.7).
6. **Repeat, New seed, Edit** берут параметры прогона-источника (снимок при создании), а не текущие настройки сессии.
7. **Ядро отдаёт коды** (`ParameterOutcomeKind`, `ParameterDropReason`). Тексты берутся из `.resx` оболочки (`docs/architecture.md`, «Правила слоёв»).

## План

### Типы и их роли (Core)
- `Domain/Models/ModelParameter` (record): `Name` (имя из каталога), `Kind` (`ParameterKind`: `Enum`, `Range`, `Boolean`), `Values` (только `Enum`, порядок каталога), `Min`, `Max` (только `Range`).
- `Domain/Models/ImageModel`: новое поле `IReadOnlyList<ModelParameter>? Parameters`. `null` значит «неизвестно» (строка кеша до миграции 0002 или модель не найдена). Пустой список значит «провайдер не объявил параметров». Поля `SupportsSeed`, `SupportsImageInput`, `MaxImagesPerRequest` сохраняются и выводятся из того же ответа.
- `Domain/Sessions/GenerationParameters` (record, все поля `string?`): `AspectRatio`, `Resolution`, `OutputFormat`. `null` значит «по умолчанию модели», такой параметр в план не попадает. Статическое `Empty`. Проверка при сохранении: `AspectRatio` — `W:H` (числа, допускается дробь) или `auto`; `Resolution` — непустая строка до 16 символов; `OutputFormat` — `png`, `jpeg` или `webp`.
- `Domain/Sessions/SessionSettings`: новое поле `GenerationParameters Parameters { get; set; } = new()`. `CreateDefault()` не меняется (параметры пустые).
- `Domain/Generations/ParameterOutcome` (record): `Name`, `Kind` (`ParameterOutcomeKind`: `Applied`, `Replaced`, `Dropped`), `Requested` (строка), `Sent` (строка или null при `Dropped`), `Reason` (`ParameterDropReason?`: `NotSupported`, `NoAllowedValue`, `CatalogUnknown`).
- `Domain/Generations/Run`: новое поле `GenerationParameters Parameters`, снимок параметров, запрошенных при создании прогона.
- `Domain/Generations/Generation`: новое поле `IReadOnlyList<ParameterOutcome> Outcomes`. Пусто для старых записей.
- `Providers/ImageGenerationRequest`: добавить `string? AspectRatio = null`, `string? Resolution = null`, `string? OutputFormat = null`. Значения уже выбраны планом, провайдер их не решает. `IImageGenerator` не меняется.

### Планирование (Core, чистая функция)
`Services/GenerationParameterPlanner`: без ввода-вывода и без времени.
`Plan(GenerationParameters requested, ImageModel? model) → IReadOnlyList<ParameterOutcome>`

```
для name в [AspectRatio, Resolution, OutputFormat], если requested.name != null:
  если model == null или model.Parameters == null          → Dropped(CatalogUnknown)
  p = model.Parameters с Name == name (без учёта регистра имени)
  p == null                                                 → Dropped(NotSupported)
  p.Kind != Enum                                            → Dropped(NotSupported)   (правило на будущее)
  requested ∈ p.Values (без учёта регистра)                 → Applied(Sent = значение из каталога)
  иначе ближайшее из p.Values (правила ниже)                → Replaced(Sent = ближайшее)
  ближайшего нет                                            → Dropped(NoAllowedValue)
```

- **AspectRatio:** расстояние `|ln(W/H) − ln(targetW/targetH)|`. Значение `auto` в сравнение не входит. Если запрошено `auto`, а его в списке нет, результат `Dropped(NoAllowedValue)`. Если в списке только `auto`, результат `Replaced` на `auto`. При равенстве побеждает значение, которое раньше в каталоге.
- **Resolution:** число из `NK` = N·1000, из записи без `K` — само число (единица не подтверждена, см. вопросы). Расстояние по модулю. Если число не разобрать, `Dropped(NoAllowedValue)`. При равенстве побеждает значение раньше в каталоге.
- **OutputFormat:** только точное совпадение, замены нет (в спецификации «формат по умолчанию модели»). Иначе `Dropped(NoAllowedValue)`. Фактический формат пишется из ответа провайдера (`MediaType`, как сейчас).

### Хранение (Data)
- Миграция `ImageGen.Data/Migrations/0002_generation_parameters.sql`, добавляется в `Migrations.All`. `0001` не менять.
  - `ALTER TABLE image_models ADD COLUMN supported_parameters_json TEXT NULL;`
  - `ALTER TABLE runs ADD COLUMN parameters_json TEXT NULL;`
  - `ALTER TABLE generations ADD COLUMN outcomes_json TEXT NULL;`
- JSON только через `CoreJsonContext` (source generation). В контекст добавить `GenerationParameters`, `List<ModelParameter>`, `List<ParameterOutcome>`. Для `Parameters` NULL читается как `GenerationParameters.Empty`. Для `Outcomes` NULL читается как пустой список.
- `ImageModelRepository`: читает и пишет `supported_parameters_json` (`null` сохраняется как NULL).
- `RunRepository`: пишет `parameters_json` в `AddRunAsync`, читает в `GetRunAsync` и в списках. `outcomes_json` пишется в `AddRunAsync` и `UpdateGenerationAsync`, читается в обеих выборках генераций.
- `SqliteDatabase` применяет миграции по `PRAGMA user_version`, поэтому 0002 встаёт поверх существующей БД без потери данных.

### Каталог (Core, OpenRouter)
- `OpenRouterClient.ListImageModelsAsync`: из `supported_parameters` строится `ModelParameter`-список (`enum` → `Values`, `range` → `Min`/`Max`, `boolean` → `Boolean`). Неизвестный тип пропускается. `SupportsSeed` остаётся как есть (ключ `seed` есть).
- `OpenRouterClient.GenerateAsync`: непустые `AspectRatio`, `Resolution`, `OutputFormat` добавляются в тело под именами каталога. При `null` поля в теле нет. Положение поля в теле не подтверждено (см. вопросы).
- `ModelCatalogService.GetImageModelsAsync`: если в кеше есть строка с `Parameters == null`, кеш считается устаревшим и каталог перезагружается. Если загрузка падает, возвращается кеш, и тогда параметры неизвестны (`CatalogUnknown`).

### Настройки сессии (Core)
- `SessionService.UpdateGenerationParametersAsync(sessionId, GenerationParameters parameters)`: проверка по правилам выше (`ArgumentException`), свежее чтение сессии, замена только `Settings.Parameters`, `UpdatedAt`, сохранение.
- `SessionService.UpdateSettingsAsync` (модели, прогоны, ассистент): **не меняет** `Parameters`. Берёт их из свежей записи и игнорирует переданное значение. Это закрывает потерю параметров при сохранении экрана моделей.

### Прогон (Core)
- `GenerationService.StartRunAsync`: `requested = session.Settings.Parameters`.
- `RepeatAsync`, `NewSeedAsync`, `EditAsync`: `requested = run.Parameters` прогона-источника (`IRunRepository.GetRunAsync(source.RunId)`). Модель и сид — как сейчас.
- `CreateRunAsync(sessionId, prompt, origin, pairs, requested)`: модель ищется один раз на модель (как `seedSupport` сейчас). Для каждой генерации `Outcomes = GenerationParameterPlanner.Plan(requested, model)`, `SeedApplied` как сейчас, `Run.Parameters = requested`.
- `Launch` и `GenerateAndStoreAsync`: `ImageGenerationRequest` строится из `Outcomes`. В запрос уходят значения `Applied` и `Replaced`, `Dropped` не уходит.
- Ошибки, модерация и состояние «отфильтровано» не зависят от параметров.

### Экран параметров (MAUI)
- `PageModels/SessionParametersPageModel` (`IQueryAttributable`, параметр `sessionId`). Зависимости: `SessionService`, `ModelCatalogService`, `INavigationService`, `IDialogService`.
  ```
  Load: session = GetAsync(sessionId); models = catalog.GetImageModelsAsync(forceRefresh: false)
        по выбранным моделям сессии собрать объединённые enum-значения aspect_ratio и resolution;
        сохранённое значение добавить в список, даже если его нет ни в одной модели
        output_format: png, jpeg, webp
  Save: UpdateGenerationParametersAsync → GoBackAsync; ошибка → AlertAsync
  ```
- Пикеры: первый пункт «по умолчанию модели» (значение `null`). У каждого пикера `SemanticProperties.Description`.
- `Pages/SessionParametersPage.xaml(.cs)`. `Routes`: `SessionParameters = "parameters"` и `SessionParametersFor(Guid)`.
- `PageModels/ChatPageModel`: команда перехода на экран параметров рядом с командой моделей. `Pages/ChatPage.xaml`: кнопка рядом с кнопкой моделей.
- `MauiProgram.cs`: `AddTransientWithShellRoute<SessionParametersPage, SessionParametersPageModel>(Routes.SessionParameters)`.
- `PageModels/GenerationItemViewModel`: `DetailsOf` добавляет строки по `Outcomes`. Например: «соотношение сторон 3:2 → 4:3», «формат webp: не поддерживается». `SummaryText` включает их. Тексты — `AppResources` (ключи для исходов, причин и имён параметров).

### Крайние случаи
- Параметр задан, модель его не поддерживает: прогон идёт, в плане `Dropped`, на карточке видна причина.
- Смена модели в сессии: параметры сессии не меняются, следующий прогон строит новый план. Старые прогоны и их `Outcomes` не меняются.
- Модель нет в каталоге, или кеш без параметров (после миграции), и сети нет: все заданные параметры `Dropped(CatalogUnknown)`, прогон идёт.
- Старый прогон без `parameters_json` и старая генерация без `outcomes_json`: параметров нет, на карточке нет строк о параметрах.
- Repeat, New seed, Edit после смены настроек сессии: используются параметры прогона-источника.
- Сохранение экрана моделей не трогает параметры.
- `supported_parameters` пустой (`{}`): все заданные параметры `Dropped(NotSupported)`.

### Тест-кейсы (условие → результат)
Планировщик (`ImageGen.Core.Tests/Services/GenerationParameterPlannerTests.cs`):
1. Запрошено `3:2`, в enum есть `3:2` → `Applied`, `Sent` = `3:2`.
2. Запрошено `3:2`, enum `[1:1, 4:3, 3:4, 16:9, 9:16, auto]` → `Replaced`, `Sent` = `4:3` (расстояние до 4:3 меньше, чем до 1:1).
3. Запрошено `4:3`, enum `[1:1, 2:3, 3:2, auto]` → `Replaced`, `Sent` = `3:2`.
4. Запрошено `4:3`, enum `[auto]` → `Replaced`, `Sent` = `auto`.
5. Запрошено `auto`, enum без `auto` → `Dropped(NoAllowedValue)`.
6. Запрошено `1:1`, enum `[1:2, 2:1]` (равные расстояния) → `Replaced`, `Sent` = `1:2` (раньше в каталоге).
7. Запрошено `2K`, enum `[1K, 1.5K, 4K]` → `Replaced`, `Sent` = `1.5K`.
8. Запрошено `4K`, enum `[1K, 2K]` → `Replaced`, `Sent` = `2K`.
9. Запрошено `webp`, enum `[png, jpeg]` → `Dropped(NoAllowedValue)`.
10. Запрошено `PNG`, enum `[png, jpeg]` → `Applied`, `Sent` = `png` (значение из каталога).
11. Параметр не объявлен в модели → `Dropped(NotSupported)`.
12. Модель `null` или `Parameters == null` → каждому заданному параметру `Dropped(CatalogUnknown)`.
13. Параметр `null` в настройках → записи в `Outcomes` нет, в запросе поля нет.
14. `Parameters` пустой список → каждому заданному параметру `Dropped(NotSupported)`.

Каталог (`ImageGen.Core.Tests/OpenRouter/OpenRouterClientTests.cs`):
15. Ответ с enum `aspect_ratio`, `resolution`, `output_format` → `Parameters` содержит три элемента с нужными значениями.
16. Ответ с `n` range 1–4 → `Parameters` содержит `Min` = 1, `Max` = 4; `MaxImagesPerRequest` = 4, как сейчас.
17. Неизвестный тип параметра в ответе → параметр пропущен, остальные разобраны.
18. `GenerateAsync` с `AspectRatio = "3:2"` и `OutputFormat = "png"` → в теле есть эти поля с нужными значениями. Без них полей нет.

Настройки (`SessionServiceTests`, `SessionSettingsTests`):
19. `UpdateGenerationParametersAsync` с `3:2` → параметры сохранены, модели и `RunsPerModel` не меняются.
20. `UpdateGenerationParametersAsync` с `gif` → `ArgumentException`, сессия не меняется.
21. `UpdateSettingsAsync` с новым набором моделей, при сохранённых параметрах → параметры остаются.
22. JSON сессии без поля `parameters` → `Parameters` равен `GenerationParameters.Empty`.

Прогон (`GenerationServiceTests`):
23. Сессия с `AspectRatio = 3:2`, модель его не поддерживает → в `Outcomes` исход `Dropped`, в `GenerateAsync` поле не уходит.
24. `RepeatAsync` после смены настроек сессии → план строится по параметрам прогона-источника.
25. `StartRunAsync` с тремя моделями → у каждой генерации свой `Outcomes`, сид у всех одинаковый (как сейчас).

Данные (`ImageGen.Data.Tests`):
26. БД версии 1 с сессией и генерацией → после миграции 0002 данные на месте, `outcomes_json` = NULL, `user_version` = 2.
27. `ImageModelRepository` сохраняет и возвращает `Parameters`; `null` возвращается как `null`.
28. `RunRepository` сохраняет `Run.Parameters` и `Generation.Outcomes` и читает их обратно.

MAUI (`ImageGen.Core.Tests/MauiLinked/`):
29. `GenerationItemViewModel` с исходом `Replaced` → текст карточки содержит замену. С `Dropped` → причину. Без исходов → текст как раньше.
30. `SessionParametersPageModel`: сохранение передаёт выбранные значения; «по умолчанию» → `null`.

### Команды проверки
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data/ImageGen.Data.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes          # Windows
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-windows10.0.19041.0  # Windows
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android              # Windows
scripts/check.sh
```
Ручная проверка на Windows: сессия с `aspect_ratio` 3:2 и `output_format` webp, модель без webp. Карточка показывает замену (если есть) и отброшенное, файл сохраняется, формат файла — тот, что вернул провайдер.

Файлы: ImageGen/ImageGen.Core/Domain/Models/ModelParameter.cs, ImageGen/ImageGen.Core/Domain/Models/ImageModel.cs, ImageGen/ImageGen.Core/Domain/Sessions/GenerationParameters.cs, ImageGen/ImageGen.Core/Domain/Sessions/SessionSettings.cs, ImageGen/ImageGen.Core/Domain/Generations/ParameterOutcome.cs, ImageGen/ImageGen.Core/Domain/Generations/Run.cs, ImageGen/ImageGen.Core/Domain/Generations/Generation.cs, ImageGen/ImageGen.Core/Providers/ImageGenerationRequest.cs, ImageGen/ImageGen.Core/Services/GenerationParameterPlanner.cs, ImageGen/ImageGen.Core/Services/SessionService.cs, ImageGen/ImageGen.Core/Services/GenerationService.cs, ImageGen/ImageGen.Core/Services/ModelCatalogService.cs, ImageGen/ImageGen.Core/OpenRouter/OpenRouterClient.cs, ImageGen/ImageGen.Core/Serialization/CoreJsonContext.cs, ImageGen/ImageGen.Data/Migrations/0002_generation_parameters.sql, ImageGen/ImageGen.Data/Migrations.cs, ImageGen/ImageGen.Data/Repositories/ImageModelRepository.cs, ImageGen/ImageGen.Data/Repositories/RunRepository.cs, ImageGen/ImageGen.Maui/Pages/SessionParametersPage.xaml, ImageGen/ImageGen.Maui/Pages/SessionParametersPage.xaml.cs, ImageGen/ImageGen.Maui/PageModels/SessionParametersPageModel.cs, ImageGen/ImageGen.Maui/PageModels/ChatPageModel.cs, ImageGen/ImageGen.Maui/Pages/ChatPage.xaml, ImageGen/ImageGen.Maui/PageModels/GenerationItemViewModel.cs, ImageGen/ImageGen.Maui/Routes.cs, ImageGen/ImageGen.Maui/MauiProgram.cs, ImageGen/ImageGen.Maui/Resources/Strings/AppResources.resx

## Отчёт

## Комментарии

### 2026-10-28, claude-qnrqyt
Вопросы владельцу (план написан, статус `planned`; код не начат):
1. **Количество картинок.** F4.2 и таблица «Количество картинок» описывают число картинок на модель. План считает это `RunsPerModel` (уже в настройках), а не новым параметром. Подтвердить.
2. **Качество (`quality`).** F4.2 его упоминает, описание задачи нет. Сейчас в план не входит. Добавить в 020 или отдельной задачей?
3. **Форма запроса `POST /api/v1/images`.** Каталог даёт имена `aspect_ratio`, `resolution`, `output_format`, но неизвестно, где они лежат в теле (верхний уровень или вложенный объект) и принимает ли `resolution` значения вида `1K`. Нужен один платный запрос на `black-forest-labs/flux.2-klein-4b` (около $0.014 за мегапиксель). Разрешить такой шаг (пробный код в `poc/v0.1/020-generation-parameters/`) до реализации?
4. **Значения `512` и `768` у `resolution`.** Что это за единица? От ответа зависит, как их сравнивать с `1K` при замене.
5. **Проверка перед запуском.** Спецификация (раздел «Применение параметров к моделям») говорит показывать замены до запуска. S2.2 в `chat-sessions.md` говорит запускать сразу, без подтверждения. План показывает замены на карточке после запуска, как требует критерий. Предварительная проверка в 020 не входит. Подтвердить.
6. **Строка «Среда».** В описании её нет. По `docs/tasks.md` это «только Windows». Core и Data проверяются на Linux, MAUI только на Windows. Указать «Linux или Windows» для Core и Data? Описание я не менял.
7. **Зависимость от 016.** В описании сказано «детализировать после задачи 016». Задача 016 пока `planned`. План написан по коду `main` (f58be2d). Ждать 016 или одобрять сейчас?
8. **Повтор и редактирование.** Repeat, New seed и Edit используют параметры прогона-источника, а не текущие настройки сессии. Подтвердить.
9. **Пример в спецификации.** В `docs/specs/image-client.md` строка «1:1 → 1:1 (замена)» противоречива: 1:1 не заменяется. Исправить спецификацию в рамках 020 или отдельно? Не блокирует план.
