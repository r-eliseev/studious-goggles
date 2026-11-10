# Architecture

The main scenario is sessions with an assistant (`docs/specs/chat-sessions.md`, ADR 0004). The first release v0.1 is a walking skeleton: only OpenRouter, only MAUI. On top of the skeleton a shared presentation layer was extracted and the Avalonia head added (ADR 0006): they are part of the "Current layout" section. Together with fal and OpenRouter, Together AI is built in for image generation; the Web host is added, the browser UI remains a target part of the layout. Together is not offered as an assistant model.

The document describes the intended structure, not the progress: what is already done is visible from the task statuses. It is edited only when the code diverges from the description (`AGENTS.md`, "What to update when").

## Current layout
The v0.1 skeleton and the `ImageGen.Presentation` and `ImageGen.Avalonia` extracted after it (ADR 0006). The source of truth for tasks: the names of projects, folders and types in tasks match this section. Where each project builds: `docs/knowledge/dev-environment.md`.

```
.editorconfig                      formatting
global.json                        dotnet test mode: Microsoft.Testing.Platform
ImageGen/
  ImageGen.slnx
  ImageGen.LiveCheck/               net10.0 CLI for a budget-limited check of live APIs; uses the Core clients and the safe debug trace
  ImageGen.LiveCheck.Tests/         xUnit v3: arguments, budget limits, refusing to write secrets
  ImageGen.Web/                     ASP.NET Core minimal API host; a separate Web composition of Core/Data
  ImageGen.Web.Tests/               xUnit v3 integration tests through WebApplicationFactory
  Directory.Build.props            Nullable, ImplicitUsings, TreatWarningsAsErrors
  Directory.Packages.props         all package versions
  ImageGen.Core/                   net10.0, no UI and no SQLite
    Domain/Sessions/               Session, SessionSettings, SessionTimeline, TimelineEntry, GenerationParameters, VideoParameters
    Domain/Chat/                   ChatMessage, ChatRole, ChatAttachment, ConversationSummary
    Domain/Generations/            Run, RunOrigin, Generation, GenerationStatus, MediaKind, ErrorKind, ParameterOutcome, ParameterOutcomeKind, ParameterDropReason
    Domain/Keys/                   ApiKeyStatus, SecretNames (a key per provider)
    Domain/Models/                 ImageModel, VideoModel, ChatModel, ImageModelPrice, ImagePriceUnit (the price from the catalog), ModelRef (the "provider:model" reference), ModelParameter, ParameterKind
    Domain/Descriptions/           picture descriptions (ADR 0013)
    Domain/Gallery/                GalleryItem, GalleryQuery, GalleryCursor, GalleryPage
    Platform/                      ISecretStore, IAppPaths
    Storage/                       ISessionRepository, IChatMessageRepository, IRunRepository, IConversationSummaryRepository,
                                   IImageModelRepository and IVideoModelRepository (caches per provider), IImageFileStore, IChatAttachmentRepository,
                                   IImageDescriptionRepository, IGalleryRepository
    Providers/                     ProviderIds, ProviderResponseLimits, BoundedHttpContentReader, BoundedBase64ImageDecoder, ProviderConfig, ProviderKind, ConfiguredImageModel, ConfiguredChatModel, ProviderConfigLoader (the built-in
                                   builtin-providers.json and providers.json in DataDirectory), ProviderRegistry (validation),
                                   IProviderRouter / ProviderRouter (a client by provider id), ProviderClientBase (HTTP,
                                   timeout from the config, error codes), ProviderException, ProviderConfigException
    Providers/                     IProviderConnection (key check), IModelProvider (catalog),
                                   ISynchronousImageTransport (the HTTP transport inside the queued adapter), IAssistantChatClient (chat),
                                   AssistantChatMessage/Request/Result, AssistantTool,
                                   AssistantToolCall (the neutral chat format), ImageGenerationRequest/Result
    Providers/                     IQueuedImageProvider (submit/poll/cancel, ADR 0003), IQueuedVideoProvider (submit/poll/content), InMemoryQueuedImageProvider and
                                   InMemoryQueuedImageConnection (the adapter of a synchronous transport), ProviderJobHandle,
                                   VideoGenerationRequest, VideoJobUpdate, ProviderContent, ImageReference,
                                   JobState, JobUpdate, CancelOutcome, ProviderJobPoller (polling image jobs)
    Fal/                           FalImageProvider (the fal queue, IQueuedImageProvider), FalUrlValidator
    Http/                          LoggingHttpHandler, BodyRedactor (an HTTP log without secrets)
    Diagnostics/                   IDebugTrace, TraceRedaction (the debug trace, docs/specs/debug-trace.md)
    OpenRouter/                    OpenRouterClient (pictures, video and chat)
    OpenAiCompatible/              OpenAiCompatibleClient (pictures and chat), OpenAiChatWire (the chat/completions format)
    AnthropicCompatible/           AnthropicCompatibleClient (chat only, Messages API)
    Services/                      ApiKeyService, ModelCatalogService, SessionService, SessionChangeNotifier, TimelineOrdering, GenerationService,
                                   AssistantService, AssistantPrompts, ISeedSource, RandomSeedSource,
                                   ConversationSummaryService (a summary of the old history in the background; one job per session),
                                   AttachmentService (attachments, ADR 0012), ImageDescriptionService (ADR 0013),
                                   ChatModelCatalogService (assistant models), VideoModelCatalogService, VideoPriceCalculator,
                                   GenerationParameterPlanner (ADR 0011)
    Search/                        SessionSearchService (search over titles, prompts and descriptions), SemanticSearchService
                                   (cosine ranking), SemanticIndexService (opt-in, progress and refresh), FuzzyMatcher,
                                   ISearchTextSource, SearchSource, ITextEmbeddingRepository
    Context/                       ContextBudget (the history budget), ContextBuilder (choosing messages), TokenEstimator,
                                   TokenCorrection (a correction from usage), SummaryPlanner and SummaryPlan (when and what to summarize, C3)
    Serialization/                 CoreJsonContext
    CoreServiceCollectionExtensions.cs   AddImageGenCore()
  ImageGen.Data/                   net10.0, Microsoft.Data.Sqlite, MetadataExtractor (ADR 0021): SqliteDatabase, Migrations (the gallery index and keyset query),
                                   repositories, including the SQLite BLOB store of text embeddings,
                                   FileImageStore (reading dimensions from file headers in a bounded background),
                                   DataServiceCollectionExtensions (AddImageGenData())
  ImageGen.Core.Tests/             xUnit v3 + NSubstitute
  ImageGen.Data.Tests/             xUnit v3, a temporary DB in a file
  ImageGen.Presentation/           net10.0, CommunityToolkit.Mvvm: PageModels/, Search/SearchSettingsFile (opt-in in search.json), Services/ (INavigationService, IDialogService, IImageViewerService, IImageComparisonService, ImageViewerNavigationState, ImageViewerHitTest, IFolderLauncher, ICurrentSessionTracker, CurrentSessionTracker,
                                   IMainThread, ISecretPersistence, AppInitializer, clipboard, file picking, saving pictures), Routes,
                                   Utilities/, Resources/Strings/ (AppResources.resx), Appearance/ (style and theme, AppearanceService),
                                   Logging/ (the file log, FileDebugTrace); PageModels/ includes the gallery and spending
  ImageGen.Presentation.Tests/     xUnit v3 + NSubstitute: page models, routes, strings, the architecture test
  ImageGen.Avalonia/               desktop (Avalonia 12.1, WinExe): App, Views/ (MainWindow, SessionsView, SessionsListView,
                                   ApiKeyView, ChatView, SessionModelsView, SessionParametersView, ModelPickerView, SettingsView, GalleryView, SpendingView,
                                   StarterExamples), Themes/ (styles per the mockups), Composition/AppServices, Converters/ (FilePathToBitmap, ThumbnailConverter),
                                   Markup/ (the strings extension), Platform/ (DesktopAppPaths, InMemorySecretStore), Services/
                                   (AvaloniaMainThread, AvaloniaDialogService, clipboard, file picking, saving pictures,
                                   AvaloniaAppearanceApplier, AvaloniaImageViewerService, AvaloniaImageComparisonService, AvaloniaFullScreenController, AvaloniaFolderLauncher, Logging/, Navigation/ — the page stack and routes),
                                   Services/Secrets/ (DesktopSecretStore and backends: SecretServiceBackend for Linux,
                                   DpapiSecretBackend for Windows, KeychainSecretBackend for macOS; ADR 0008)
  ImageGen.Avalonia.Tests/         headless (Avalonia.Headless.XUnit), xunit.v3 3.2.2 (ADR 0007)
  ImageGen.Avalonia.Android/       Android (Avalonia 12.1): MobileApp/MainView without a window; uses AppServices.CreateShared,
                                   the shared Views, themes and Avalonia services; OverlayHost for dialogs, viewing and comparison,
                                   Platform/ for FilesDir, Android Keystore, saving to MediaStore and unsupported folders
  ImageGen.Avalonia.iOS/           iOS Simulator (Avalonia 12.1): MobileApp/MainView and AppDelegate; the shared Views,
                                   themes and Avalonia services; data in the app container, secrets in Keychain
  ImageGen.Maui/                   MAUI: Pages/ (including GalleryPage and SpendingPage), Controls/ (SessionsListView, StarterExamples, ChatEditor, SessionModelsView — the shared models screen of the page and the panel), PageModels/ (only
                                   ChatItemTemplateSelector), Services/ (MauiSecretStore, MauiAppPaths, MauiMainThread,
                                   Shell implementations, clipboard, file picking, MauiImageSaver, MauiAppearanceApplier, MauiImageViewerService, MauiImageComparisonService, MauiFullScreenController, MauiFolderLauncher, MauiSecretPersistence), Converters/,
                                   Platforms/
  ImageGen.E2E.Common/, ImageGen.E2E.Android/, ImageGen.E2E.Windows/   UI tests of the built MAUI apps (NUnit, Allure, Appium/FlaUI; ADR 0029)
```

Dependencies: `Maui → Presentation → Data → Core`; `Maui` also references `Core` and `Data` (registration in `MauiProgram`); `Avalonia → Presentation → Data → Core` (also references `Core` and `Data`, registration in `AppServices`); `Avalonia.Android → Presentation → Data → Core` (reuses the Views sources and the shared Avalonia services, registers the platform composition through `AppServices.CreateShared`); `Presentation → Core`.

Logging: `Core`, `Data` and `Presentation` write through `ILogger<T>`. The `{DataDirectory}/logs/imagegen-yyyyMMdd.log` file is configured by `ImageGen.Presentation/Logging` (`AddImageGenFileLogging`, Serilog; ADR 0010), the heads call it. Level: `IMAGEGEN_LOG_LEVEL` or `logging.json` in the data folder. The debug trace (enabled separately: `IMAGEGEN_DEBUG_TRACE` or `debugTrace` in `logging.json`) writes user input and the HTTP exchange with providers to `debug/<date>/` through `Core/Diagnostics/IDebugTrace`; the implementation is `Presentation/Logging/FileDebugTrace`, the specification `docs/specs/debug-trace.md`. `Core` references only `Microsoft.Extensions.Http`, `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.DependencyInjection.Abstractions`. An architecture test in `ImageGen.Core.Tests` checks that `Core` does not reference `Microsoft.Maui*`, `Microsoft.AspNetCore*`, `Microsoft.Data.Sqlite*`, `Avalonia*`; a test in `ImageGen.Presentation.Tests` checks that `Presentation` does not reference `Microsoft.Maui*`, `Avalonia*`, `Microsoft.AspNetCore*`.

Page logic (PageModels, routes, strings, `AppInitializer`) lives in `ImageGen.Presentation` and does not use UI framework types: it is tested by ordinary tests on Linux, without links to MAUI files. MAUI types (`IQueryAttributable`, `MainThread`, `ImageSource`) stay in pages, converters and implementations of interfaces (`IMainThread`, `INavigationService`, `IDialogService`) in `ImageGen.Maui`.

## Skeleton v0.1: flows
**First launch.** The head's `App` calls `AppInitializer` (`ImageGen.Presentation`): it initializes the DB (`SqliteDatabase.InitializeAsync`) and recovers unfinished generations (`GenerationService.RecoverInterruptedAsync`). For fal pictures and OpenRouter videos a saved handle allows polling to continue without a repeated submit. Handles of the in-memory adapter are valid only while the process runs: after a restart such a request ends as interrupted, without a repeated paid generation. Generations of a provider that is no longer in the configuration end with "ModelUnavailable", the others without a restorable handle are marked "Interrupted". The assistant model for new sessions comes from `IDefaultAssistantModel` (`defaults.json`); first run requires the key of the chosen provider. On the key screen you can choose another configured chat provider, check and save its key, then choose a model of its catalog as the default. The setting changes only new sessions. The key is saved through `ISecretStore` (implementations are in the "Skeleton v0.1: storage" section); the other configured keys are optional and managed from Settings through `ApiKeyService`. Settings shows a masked status, and Remove deletes only the secret of the chosen provider; deleting the key of the current assistant model leads to a repeated request the next time Sessions opens.

**A chat message.** `ChatPageModel` → `AssistantService.SendUserMessageAsync`:
1. The user's message is written to the DB; a new session takes its title from the first 40 characters.
2. A request to the chat completions of the chosen chat provider: the system prompt, the current session prompt, a summary of the old part of the history if there is one, then the session messages by the model's context budget (the current user message verbatim), the `generate_images` tool. The budget and the summary are described in `docs/specs/chat-sessions.md` (S2.6), ADR 0015.
3. A tool call → `GenerationService.StartRunAsync` (a run) and an assistant message with a link to the run. Text without a call → an assistant message. An error → an assistant message with an error flag.

**An image run.** `GenerationService` takes the models and N from the session settings, chooses N seeds (`ISeedSource`), creates the "model × seed" generations in the "queued" status in one transaction and runs them in parallel: at most 4 locally tracked jobs per provider id. Provider A does not block provider B. Providers go the shared path: `IQueuedImageProvider.SubmitAsync` → a handle saved in the DB → `ProviderJobPoller` → a terminal state → a file through `IImageFileStore`, a DB record and the `GenerationUpdated` event. Fal keeps a remote handle and continues polling after a restart. The OpenRouter and OpenAI-compatible clients run a synchronous HTTP transport (`ISynchronousImageTransport`) inside `InMemoryQueuedImageProvider`; their handle lives only in process memory, so after a restart the generation is interrupted without a repeated submit. The local adapter is polled quickly (50–250 ms), a native provider with an interval of 1–5 seconds. For pictures the provider slot is held until the terminal status; when a session is deleted, the local adapter also waits for the cancelled transport to finish. If a remote cancel failed, the limit guarantees only the locally tracked work, not that the remote operation stopped. A polling error ends the generation: "Moderated" — "filtered", other provider errors — "failed" with its code. An error writing the result to a file ends the generation with the `ImageSaveFailed` code; an error writing the terminal state to the DB is not replaced by this code and stays a persistence error. A moderation refusal (403 with `metadata.reasons`) → "filtered".

**A video run.** `StartVideoRunAsync` creates one `Generation` of the Video kind per selected model and saves the parameters in `Run`. The first frame is allowed only from a successful picture of the same session. OpenRouter gets the request through `IQueuedVideoProvider`; the handle is saved before polling starts and restored after a restart. The provider slot is taken only by sending the video, polling runs without a slot with an interval of 5–30 seconds. On success the response is opened as a stream, written through `SaveMediaStreamAsync` and then recorded in the DB before the `GenerationUpdated` event; a save error ends the generation without a file path. Pictures keep their slot lifecycle and use `ProviderJobPoller`.

**User cancellation.** For queued image jobs `GenerationService` addresses the active jobs from the `_running` registry, waits for the submit to finish, then sends one remote cancel with a separate timeout. Cancelling before submit stops local execution without a provider request. A confirmed cancellation is serialized with polling/saving the result: the first recorded terminal state wins. `CancellationRequested` is saved in `cancel_requested_at` and keeps the watcher and the slot active until the result; the mark stays in the record on a later success. OpenRouter provides no remote cancel for video; stopping the local wait does not confirm that the provider stopped or did not charge.

**Screen updates.** `ChatPageModel` re-reads the session feed after each of its own actions (`SessionService.GetTimelineAsync`), and the `GenerationUpdated` event updates one card on the UI thread (`IMainThread.BeginInvokeOnMainThread`; implementations `MauiMainThread` and `AvaloniaMainThread`). `SessionService` and `GenerationService` report a session change through `SessionChangeNotifier`, and `SessionsPageModel` re-reads one session and updates its row on the UI thread.

**Deleting a session.** `SessionService` calls `ISessionGenerationCoordinator` before deleting records and files; in production this interface and `GenerationService` resolve to one singleton instance. The caller's cancellation is checked before stopping starts. Stopping cancels the local tokens and waits for submit phases; for already known queued jobs a remote cancel is requested with the provider's timeout. The remote cancel is best effort: on an error the provider's operation may continue. After stopping, local writes are guaranteed to have stopped, then the DB rows and the file folder are deleted and a session change notification is sent. Cleanup runs with `CancellationToken.None`; an error of a step stops the chain, a repeated deletion may continue the idempotent cleanup. A job the process has not registered yet is not cancelled by this mechanism.

## Skeleton v0.1: storage
The SQLite file `imagegen.db` in `IAppPaths.DataDirectory`, the schema in `ImageGen.Data/Migrations/NNNN_name.sql` — embedded resources, applied by number without a list in code (`Migrations.cs`, ADR 0002). Pictures: `images/<sessionId>/<generationId>.<ext>` in the same folder; the DB stores the relative path. Message attachments (pictures the user attached in the chat, ADR 0012) are in `images/<sessionId>/attachments/<attachmentId>.<ext>`, rows in `chat_attachments`. Provider keys are stored only behind `ISecretStore` (`SecretNames.ForProvider`) and never get into the DB, settings or files in plain text: in MAUI — `SecureStorage` (`MauiSecretStore`), in Avalonia — `DesktopSecretStore` (Secret Service on Linux, DPAPI on Windows, Keychain on macOS; without them only in memory until exit, ADR 0008).

Spending is stored in the independent `expenses` ledger without a foreign key to a session or a generation. An operation has a stable `operation_id` and is written in a transaction together with the source data; a repeated save updates the same record. Known prices are summed, unknown ones stay unknown and are counted over the still existing source rows. So deleting a session or a picture does not change the ledger, but deleting source rows limits the availability of statistics on unknown prices.

## Target layout (after the skeleton)
```
ImageGen.Core       + Catalog with filters; spending
ImageGen.Data       + spending table, tags, collections
ImageGen.Presentation, ImageGen.Avalonia   + models and screens of new features (layout — "Current layout")
ImageGen.Maui       + Gallery, Comparison, Spending, Settings; saving to the Android and iOS gallery
ImageGen.Web        ASP.NET Core: login by a password from an environment variable, Data Protection for secrets,
                    background execution of runs, an event stream to the browser
ImageGen.Web/ClientApp   Vue 3 + Vite + TypeScript
ImageGen.Web.Tests
ImageGen.Acceptance.Tests   Gherkin scenarios on Reqnroll over headless Avalonia and the real composition (ADR 0031)
Dockerfile, docker-compose.yml   the /data volume
```
- The Vue client talks only to the same-origin `/api`: `WebApiClient` adds the antiforgery token to mutating requests, and the provider key stays only in the form's memory until sent. `vue-router` handles the session list, chat, models and settings; SSE events are merged by generation id, `resync` and reconnecting re-read the timeline.
- Provider connections retain catalog/key/chat responsibilities; synchronous image HTTP calls are transport-only and wrapped by `InMemoryQueuedImageProvider` before orchestration sees them. ADR 0020 replaces ADR 0005's proposed OpenRouter-specific runner with this shared adapter for OpenRouter and OpenAI-compatible clients.
- Events: for Web a subscription through a `Channel` instead of a .NET event.
- Web: single-user, a password from an environment variable, a cookie session; secrets in a file under Data Protection; encryption keys and data in a Docker volume.
- MAUI: bottom tabs on a phone, a side panel on a wide screen.
- The MAUI and Avalonia heads: one screen logic (`Presentation`), its own markup in each; every screen feature is done in both. Dependencies: `Maui → Presentation → Data → Core`, `Avalonia → Presentation`.
- Acceptance scenarios: `ImageGen.Acceptance.Tests` references `ImageGen.Avalonia` and builds the app through `AppServices.Create` with a test seam that replaces the network, time, seeds and platform dialogs; the same `.feature` files tagged `@e2e` also run in `ImageGen.E2E.*` over Appium/FlaUI.

## Layer rules
- `Core` knows nothing about MAUI, Avalonia, ASP.NET Core, SQLite and UI types (the `CoreDependencyTests` architecture test forbids assemblies with the prefixes `Microsoft.Maui`, `Microsoft.AspNetCore`, `Microsoft.Data.Sqlite`, `Avalonia`) (`Brush`, `Color`, `Shell`, `Preferences`, `FileSystem`). Platform specifics come through `Core/Platform`.
- Code outside `Providers/`, `Fal/`, `OpenRouter/`, `OpenAiCompatible/`, `AnthropicCompatible/` does not know which provider draws the pictures or answers as the assistant: the provider is taken by `ModelRef` (provider and model) through `IProviderRouter`. Addresses and API kinds of providers are set by the config (ADR 0009), keys only in `ISecretStore`. Exceptions: `ModelVendors` recognizes the `fal-ai` prefix (S5.5), `GenerationService` polls the local adapter quickly (ADR 0020), first run checks the OpenRouter key.
- The core does not produce UI texts: it returns errors and states as codes (`ErrorKind`, `GenerationStatus`), the texts are in the `.resx` of the presentation layer (`Presentation`, shared by MAUI and Avalonia).
- MAUI pages and Avalonia views know only their `PageModel`; a `PageModel` knows the core services.

## External systems
Built-in providers: OpenRouter (HTTP API: `/api/v1/key`, `/api/v1/models`, `/api/v1/images/models`, `/api/v1/images`, `/api/v1/videos/models`, `/api/v1/videos`, `/api/v1/chat/completions`), fal.ai (queued API, ADR 0003) and Together AI (OpenAI-compatible `images/generations`, images only). Other OpenAI-compatible APIs (`images/generations`, `chat/completions`) and the Anthropic Messages API (chat) are connected through `providers.json`. Together has a built-in FLUX catalog; a user entry with `id=together` replaces it by the common config override rules. The Together list adds no chat models.

## Constraints
- Platforms: MAUI has no Linux target; the Linux desktop is served by the Avalonia head (ADR 0006), the browser and Docker by the Web head. The MAUI Windows target builds only on Windows (`docs/knowledge/dev-environment.md`).
- Security: the key only in encrypted storage; keys and passwords never get into logs, exports and the repository.
- Performance: generations run in parallel, the UI thread is not blocked.
- Provider links expire, so the file is saved immediately (at OpenRouter the picture comes in the response).
- Trimming and AOT on iOS: no reflection in the core and storage (JSON through `JsonNode` and source generation, SQL through ADO.NET).
