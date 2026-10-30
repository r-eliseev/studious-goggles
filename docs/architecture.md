# Архитектура

Основной сценарий — сессии с ассистентом (`docs/specs/chat-sessions.md`, ADR 0004). Первый релиз v0.1 — ходячий скелет: только OpenRouter, только MAUI. Поверх скелета вынесен общий слой представления и добавлена оболочка Avalonia (ADR 0006): они входят в раздел «Текущая раскладка». Целевая раскладка с fal, Together AI и Web строится поверх этого (раздел «Целевая раскладка»).

Документ описывает задуманное устройство, а не ход работ: что уже сделано, видно по статусам задач. Правится, только когда код разошёлся с описанием (`AGENTS.md`, «Когда что обновлять»).

## Текущая раскладка
Скелет v0.1 и вынесенные после него `ImageGen.Presentation` и `ImageGen.Avalonia` (ADR 0006). Источник правды для задач: имена проектов, каталогов и типов в задачах совпадают с этим разделом. Где какой проект собирается: `docs/knowledge/dev-environment.md`.

```
.editorconfig                      форматирование
global.json                        режим dotnet test: Microsoft.Testing.Platform
ImageGen/
  ImageGen.slnx
  Directory.Build.props            Nullable, ImplicitUsings, TreatWarningsAsErrors
  Directory.Packages.props         все версии пакетов
  ImageGen.Core/                   net10.0, без UI и без SQLite
    Domain/Sessions/               Session, SessionSettings, SessionTimeline, TimelineEntry
    Domain/Chat/                   ChatMessage, ChatRole
    Domain/Generations/            Run, RunOrigin, Generation, GenerationStatus, ErrorKind
    Domain/Keys/                   ApiKeyStatus, SecretNames (ключ по провайдеру)
    Domain/Models/                 ImageModel, ModelRef (ссылка «провайдер:модель»)
    Domain/Descriptions/           описания картинок (ADR 0013)
    Platform/                      ISecretStore, IAppPaths
    Storage/                       ISessionRepository, IChatMessageRepository, IRunRepository, IConversationSummaryRepository,
                                   IImageModelRepository (кэш по провайдеру), IImageFileStore, IChatAttachmentRepository,
                                   IImageDescriptionRepository
    Providers/                     ProviderConfig, ProviderKind, ConfiguredImageModel, ProviderConfigLoader (встроенный
                                   builtin-providers.json и providers.json в DataDirectory), ProviderRegistry (проверка),
                                   IProviderRouter / ProviderRouter (клиент по id провайдера), ProviderClientBase (HTTP,
                                   таймаут из конфига, коды ошибок), ProviderException, ProviderConfigException
    Providers/                     IProviderConnection (проверка ключа), IModelProvider (картинки: IImageGenerator,
                                   каталог), IAssistantChatClient (чат), AssistantChatMessage/Request/Result, AssistantTool,
                                   AssistantToolCall (нейтральный формат чата), ImageGenerationRequest/Result
    Providers/                     IQueuedImageProvider (submit/poll/cancel, ADR 0003), ProviderJobHandle, JobState, JobUpdate,
                                   CancelOutcome, ProviderJobPoller (общий опрос заданий)
    Fal/                           FalImageProvider (очередь fal, IQueuedImageProvider)
    Http/                          LoggingHttpHandler, BodyRedactor (журнал HTTP без секретов)
    Diagnostics/                   IDebugTrace, TraceRedaction (отладочный трейс, docs/specs/debug-trace.md)
    OpenRouter/                    OpenRouterClient (картинки и чат)
    OpenAiCompatible/              OpenAiCompatibleClient (картинки и чат), OpenAiChatWire (формат chat/completions)
    AnthropicCompatible/           AnthropicCompatibleClient (только чат, Messages API)
    Services/                      ApiKeyService, ModelCatalogService, SessionService, TimelineOrdering, GenerationService,
                                   AssistantService, AssistantPrompts, ISeedSource, RandomSeedSource,
                                   ConversationSummaryService (сводка старой истории в фоне; одна задача на сессию),
                                   AttachmentService (вложения, ADR 0012), ImageDescriptionService (ADR 0013),
                                   ChatModelCatalogService (модели ассистента), GenerationParameterPlanner (ADR 0011)
    Search/                        SessionSearchService (поиск по названиям, промптам и описаниям), FuzzyMatcher (нечёткие
                                   совпадения слов), ISearchTextSource (тексты всех сессий; реализация в Data)
    Context/                       ContextBudget (бюджет истории), ContextBuilder (выбор сообщений), TokenEstimator,
                                   TokenCorrection (поправка по usage), SummaryPlanner и SummaryPlan (когда и что сводить, C3)
    Serialization/                 CoreJsonContext
    CoreServiceCollectionExtensions.cs   AddImageGenCore()
  ImageGen.Data/                   net10.0, Microsoft.Data.Sqlite: SqliteDatabase, Migrations, репозитории, FileImageStore,
                                   DataServiceCollectionExtensions (AddImageGenData())
  ImageGen.Core.Tests/             xUnit v3 + NSubstitute
  ImageGen.Data.Tests/             xUnit v3, временная БД в файле
  ImageGen.Presentation/           net10.0, CommunityToolkit.Mvvm: PageModels/, Services/ (INavigationService, IDialogService,
                                   IMainThread, ISecretPersistence, AppInitializer, буфер обмена, выбор файлов, сохранение картинок), Routes,
                                   Utilities/, Resources/Strings/ (AppResources.resx), Appearance/ (стиль и тема, AppearanceService),
                                   Logging/ (файловый журнал, FileDebugTrace)
  ImageGen.Presentation.Tests/     xUnit v3 + NSubstitute: модели страниц, маршруты, строки, архитектурный тест
  ImageGen.Avalonia/               десктоп (Avalonia 12.1, WinExe): App, Views/ (MainWindow, SessionsView, SessionsListView,
                                   ApiKeyView, ChatView, SessionModelsView, SessionParametersView, ModelPickerView, SettingsView,
                                   StarterExamples), Themes/ (стили по макетам), Composition/AppServices, Converters/ (FilePathToBitmap),
                                   Markup/ (расширение строк), Platform/ (DesktopAppPaths, InMemorySecretStore), Services/
                                   (AvaloniaMainThread, AvaloniaDialogService, буфер, выбор файлов, сохранение картинок,
                                   AvaloniaAppearanceApplier, Logging/, Navigation/ — стек страниц и маршруты),
                                   Services/Secrets/ (DesktopSecretStore и бэкенды: SecretServiceBackend для Linux,
                                   DpapiSecretBackend для Windows, KeychainSecretBackend для macOS; ADR 0008)
  ImageGen.Avalonia.Tests/         headless (Avalonia.Headless.XUnit), xunit.v3 3.2.2 (ADR 0007)
  ImageGen.Maui/                   MAUI: Pages/, Controls/ (SessionsListView, StarterExamples, ChatEditor), PageModels/ (только
                                   ChatItemTemplateSelector), Services/ (MauiSecretStore, MauiAppPaths, MauiMainThread,
                                   Shell-реализации, буфер, выбор файлов, MauiImageSaver, MauiAppearanceApplier), Converters/,
                                   Platforms/
```

Зависимости: `Maui → Presentation → Data → Core`; `Maui` ссылается также на `Core` и `Data` (регистрация в `MauiProgram`); `Avalonia → Presentation → Data → Core` (также ссылается на `Core` и `Data`, регистрация в `AppServices`); `Presentation → Core`.

Журнал: `Core`, `Data` и `Presentation` пишут через `ILogger<T>`. Файл `{DataDirectory}/logs/imagegen-yyyyMMdd.log` настраивает `ImageGen.Presentation/Logging` (`AddImageGenFileLogging`, Serilog; ADR 0010), оболочки его вызывают. Уровень: `IMAGEGEN_LOG_LEVEL` или `logging.json` в папке данных. Отладочный трейс (включается отдельно: `IMAGEGEN_DEBUG_TRACE` или `debugTrace` в `logging.json`) пишет вводы пользователя и HTTP-обмен с провайдерами в `debug/<дата>/` через `Core/Diagnostics/IDebugTrace`; реализация — `Presentation/Logging/FileDebugTrace`, спецификация `docs/specs/debug-trace.md`. `Core` ссылается только на `Microsoft.Extensions.Http`, `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.DependencyInjection.Abstractions`. Архитектурный тест в `ImageGen.Core.Tests` проверяет, что `Core` не ссылается на `Microsoft.Maui*`, `Microsoft.AspNetCore*`, `Microsoft.Data.Sqlite*`; тест в `ImageGen.Presentation.Tests` проверяет, что `Presentation` не ссылается на `Microsoft.Maui*`, `Avalonia*`, `Microsoft.AspNetCore*`.

Логика страниц (PageModels, маршруты, строки, `AppInitializer`) живёт в `ImageGen.Presentation` и не использует типы UI-фреймворков: тестируется обычными тестами на Linux, без ссылок на файлы MAUI. Типы MAUI (`IQueryAttributable`, `MainThread`, `ImageSource`) остаются в страницах, конвертерах и реализациях интерфейсов (`IMainThread`, `INavigationService`, `IDialogService`) в `ImageGen.Maui`.

## Скелет v0.1: потоки
**Первый запуск.** `App` оболочки вызывает `AppInitializer` (`ImageGen.Presentation`): он инициализирует БД (`SqliteDatabase.InitializeAsync`) и восстанавливает незавершённые генерации (`GenerationService.RecoverInterruptedAsync`): генерации с сохранённым хэндлом задания возобновляются без повторного submit, генерации провайдера, которого больше нет в конфигурации, завершаются «ModelUnavailable», остальные помечаются «Interrupted». Если ключа OpenRouter нет (`ApiKeyService.HasKeyAsync`), открывается страница ключа; ключ проверяется `GET /api/v1/key` и сохраняется через `ISecretStore` (реализации — в разделе «Скелет v0.1: хранение»).

**Сообщение в чат.** `ChatPageModel` → `AssistantService.SendUserMessageAsync`:
1. Сообщение пользователя пишется в БД; у новой сессии название берётся из первых 40 символов.
2. Запрос к OpenRouter chat completions: системный промпт, текущий промпт сессии, при наличии — сводка старой части истории, затем сообщения сессии по бюджету контекста модели (текущее сообщение пользователя — дословно), инструмент `generate_images`. Правила бюджета и сводки — `docs/specs/assistant-context.md`, ADR 0015.
3. Вызов инструмента → `GenerationService.StartRunAsync` (прогон) и сообщение ассистента со ссылкой на прогон. Текст без вызова → сообщение ассистента. Ошибка → сообщение ассистента с признаком ошибки.

**Прогон.** `GenerationService` берёт модели и N из настроек сессии, выбирает N сидов (`ISeedSource`), создаёт генерации «модель × сид» в статусе «в очереди» одной транзакцией и запускает их параллельно (не больше 4 одновременно). Путь генерации зависит от провайдера. OpenRouter и OpenAI-совместимые: «идёт» → `IImageGenerator.GenerateAsync` (`POST /api/v1/images`, синхронно) → файл через `IImageFileStore` → запись в БД → событие `GenerationUpdated`. Провайдер с очередью (`IQueuedImageProvider`, сейчас fal): submit → хэндл задания сохраняется в БД → `ProviderJobPoller` опрашивает до конечного состояния → файл, БД, событие. При старте `RecoverInterruptedAsync` снова следит за заданиями с сохранённым хэндлом (без повторного submit), задания провайдера, которого больше нет в конфигурации, завершает «ModelUnavailable», остальные незавершённые помечает «Interrupted». Ошибка самого опроса завершает генерацию: «Moderated» — «отфильтровано», остальные ошибки провайдера — «ошибка» с его кодом; ошибка обработки результата (файл, сохранение) ошибкой опроса не считается. Отказ модерации (403 с `metadata.reasons`) → «отфильтровано».

**Обновление экрана.** `ChatPageModel` после каждого своего действия перечитывает ленту сессии (`SessionService.GetTimelineAsync`), а событие `GenerationUpdated` обновляет одну карточку в UI-потоке (`IMainThread.BeginInvokeOnMainThread`; реализации `MauiMainThread` и `AvaloniaMainThread`).

## Скелет v0.1: хранение
SQLite-файл `imagegen.db` в `IAppPaths.DataDirectory`, схема в `ImageGen.Data/Migrations/NNNN_имя.sql` — встроенные ресурсы, применяются по номеру без списка в коде (`Migrations.cs`, ADR 0002). Картинки: `images/<sessionId>/<generationId>.<ext>` в той же папке; в БД хранится относительный путь. Вложения сообщений (картинки, которые пользователь прикрепил в чате, ADR 0012) лежат в `images/<sessionId>/attachments/<attachmentId>.<ext>`, строки в `chat_attachments`. Ключ OpenRouter хранится только за `ISecretStore` и в открытом виде не попадает ни в БД, ни в настройки, ни в файлы: в MAUI — `SecureStorage` (`MauiSecretStore`), в Avalonia — `DesktopSecretStore` (Secret Service на Linux, DPAPI на Windows, Keychain на macOS; без них — только в памяти до выхода, ADR 0008).

Известное упрощение скелета: удаление сессии удаляет её генерации вместе с ценами. Правило ADR 0002 «удаление картинки не удаляет запись о расходах» вводится вместе с учётом расходов (backlog).

## Целевая раскладка (после скелета)
```
ImageGen.Core       + Providers/Fal за контрактом ADR 0003, Together AI как OpenAI-совместимый провайдер (ADR 0017); Catalog с фильтрами; расходы
ImageGen.Data       + таблица расходов, теги, коллекции
ImageGen.Presentation, ImageGen.Avalonia   + модели и экраны новых фич (раскладка — «Текущая раскладка»)
ImageGen.Maui       + Галерея, Сравнение, Расходы, Настройки; сохранение в галерею Android и iOS; ru-локализация
ImageGen.Web        ASP.NET Core: вход по паролю из переменной окружения, Data Protection для секретов,
                    фоновое выполнение прогонов, поток событий браузеру
ImageGen.Web/ClientApp   Vue 3 + Vite + TypeScript
ImageGen.Web.Tests
Dockerfile, docker-compose.yml   том /data
```
- Провайдеры: контракт submit/poll/cancel (ADR 0003) уже есть (`IQueuedImageProvider`, fal), восстановление заданий после перезапуска тоже. По ADR 0005 OpenRouter тоже должен стать заданием в фоне в памяти процесса (`OpenRouterJobRunner`) за тем же контрактом, а узкий `IImageGenerator` — уйти. В коде этого нет: OpenRouter генерирует синхронно через `IImageGenerator`; владелец решил выполнить ADR 0005 (2026-10-29), до этого описание выше — задуманное, а не текущее.
- События: для Web подписка через `Channel` вместо события .NET.
- Web: однопользовательский, пароль из переменной окружения, cookie-сессия; секреты в файле под Data Protection; ключи шифрования и данные в томе Docker.
- MAUI: на телефоне вкладки внизу, на широком экране боковая панель.
- Оболочки MAUI и Avalonia: логика экранов одна (`Presentation`), разметка своя в каждой; каждая экранная фича делается в обеих. Зависимости: `Maui → Presentation → Data → Core`, `Avalonia → Presentation`.

## Правила слоёв
- `Core` не знает про MAUI, Avalonia, ASP.NET Core, SQLite и UI-типы (архитектурный тест `CoreDependencyTests` запрещает сборки с префиксами `Microsoft.Maui`, `Microsoft.AspNetCore`, `Microsoft.Data.Sqlite`, `Avalonia`) (`Brush`, `Color`, `Shell`, `Preferences`, `FileSystem`). Платформенное — через `Core/Platform`.
- Код вне `Providers/`, `OpenRouter/`, `OpenAiCompatible/`, `AnthropicCompatible/` не знает, какой провайдер рисует картинки или отвечает ассистенту: провайдер берётся по `ModelRef` (провайдер и модель) через `IProviderRouter`. Адреса и типы API провайдеров задаёт конфиг (ADR 0009), ключи — только в `ISecretStore`.
- Ядро не формирует тексты интерфейса: ошибки и состояния отдаёт кодами (`ErrorKind`, `GenerationStatus`), тексты в `.resx` слоя представления (`Presentation`, общий для MAUI и Avalonia).
- Страницы MAUI и view Avalonia знают только свой `PageModel`; `PageModel` знает сервисы ядра.

## Внешние системы
OpenRouter (HTTP API: `/api/v1/key`, `/api/v1/models` (чат-модели: входные модальности и окно контекста), `/api/v1/images/models`, `/api/v1/images`, `/api/v1/chat/completions`). Подключаемые через конфиг (`providers.json`): API в формате OpenAI (`images/generations`, `chat/completions`) и Anthropic Messages API (чат). Провайдер с очередью — fal.ai (ADR 0003); Together AI — через OpenAI-совместимый конфиг (ADR 0017).

## Ограничения
- Платформы: у MAUI нет цели Linux; Linux-десктоп обслуживает Avalonia-оболочка (ADR 0006), браузер и Docker — Web-оболочка. Цель Windows у MAUI собирается только на Windows (`docs/knowledge/dev-environment.md`).
- Безопасность: ключ только в шифрованном хранилище; ключи и пароли не попадают в журналы, экспорт и репозиторий.
- Производительность: генерации идут параллельно, UI-поток не блокируется.
- Ссылки провайдеров истекают, поэтому файл сохраняется сразу (у OpenRouter картинка приходит в ответе).
- Trimming и AOT на iOS: в ядре и хранилище без рефлексии (JSON через `JsonNode` и source generation, SQL через ADO.NET).
