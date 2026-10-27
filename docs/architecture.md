# Архитектура

Статус: целевая раскладка проработана 2026-10-27 (`docs/decisions/0001-core-and-heads.md`, `0002-sqlite-ado-net.md`, `0003-provider-contract-and-jobs.md`), в коде её пока нет. Порядок работ: ядро со всеми тремя провайдерами, затем MAUI, затем Web + Docker (решение владельца, `docs/status.md`). Задачи: `tasks/v0.1/` (ядро), `tasks/v0.2/` (MAUI), `tasks/v0.3/` (Web), `tasks/backlog/` (P1/P2).

Единственный код в репозитории — проект-пример `ImageGen/ImageGen/` и пустые заготовки в `ImageGen/ImageGen.Core/`; устройство примера описано в разделе «Состояние кода» и подробно в `docs/knowledge/example-patterns.md`.

## Целевая раскладка
Все проекты лежат в `ImageGen/` рядом с проектом-примером и входят в `ImageGen/ImageGen.slnx`. Проект-пример `ImageGen/ImageGen/` не меняется и после появления `ImageGen.Maui` остаётся только эталоном.

```
ImageGen/
  Directory.Build.props      общие свойства: net10.0, Nullable, ImplicitUsings, TreatWarningsAsErrors
  Directory.Packages.props   версии пакетов в одном месте
  ImageGen.Core/             net10.0, без UI и без хранилища
    Platform/                интерфейсы платформы: IAppPaths, IGallerySaver, IUserNotifier
    Config/                  ISecretStore, ISettingsStore, ConnectionService, AppSettings
    Providers/               IImageProvider, ProviderId, ProviderError
      Fal/  Replicate/  OpenRouter/   реализации, internal
    Catalog/                 ModelDescriptor, ModelCapabilities, PriceInfo, CatalogService, ModelSetService
    Generation/              GenerationRequest, CommonParameters, ParameterMapper, CostEstimator,
                             GenerationService (оркестратор), JobState, JobUpdate
    Results/                 RunGroup, Job, Result; IRunRepository, IResultFileStore, ISpendingRepository
    ServiceCollectionExtensions.cs   AddImageGenCore()
  ImageGen.Data/             net10.0: SQLite (ADO.NET) и файлы результатов, реализует Results/ и кеш каталога
  ImageGen.Core.Tests/       тесты ядра и архитектурные тесты
  ImageGen.Data.Tests/       тесты хранилища на временной БД
  ImageGen.Maui/             Android, iOS, Mac Catalyst, Windows: Pages, PageModels, платформенные сервисы
  ImageGen.Web/              ASP.NET Core: API, вход по паролю, фоновое выполнение заданий, статика Vue
  ImageGen.Web/ClientApp/    Vue 3 + Vite + TypeScript, собирается в wwwroot
  ImageGen.Web.Tests/        тесты API (WebApplicationFactory)
Dockerfile, docker-compose.yml   образ Web-оболочки, том /data
```

## Слои и зависимости
```
ImageGen.Maui ──┐                ┌── ImageGen.Web
                ├──> ImageGen.Data ──> ImageGen.Core
                └────────────────────> ImageGen.Core
```
- `Core` не ссылается на MAUI, ASP.NET Core, SQLite и UI-типы (`Brush`, `Color`, `Shell`, `Preferences`, `FileSystem` из MAUI Essentials). Разрешены только `Microsoft.Extensions.*` (DI, Http, Logging, Options) и `System.Text.Json`.
- `Data` ссылается на `Core` и `Microsoft.Data.Sqlite`; реализует интерфейсы из `Core/Results` и кеш каталога. Схема и миграции: `docs/decisions/0002-sqlite-ado-net.md`.
- Оболочки ссылаются на `Core` и `Data`, реализуют интерфейсы `Core/Platform` и `Core/Config` своими средствами и друг на друга не ссылаются.
- Внутри `Core` код вне `Providers/` не знает, какой провайдер за интерфейсом. Реализации провайдеров `internal`, наружу видны только `IImageProvider` и регистрация в DI.
- Страницы MAUI знают только свой `PageModel`; `PageModel` знает сервисы ядра и абстракции навигации и сообщений (`INavigationService`, `IToastService`), но не `Shell.Current` напрямую.
- Правила слоёв проверяет архитектурный тест в `ImageGen.Core.Tests` (ссылки сборок), а не только аудит.

## Сервисы ядра
Оболочки работают с ядром только через эти сервисы (MAUI вызывает их из PageModel, Web API — из эндпоинтов):

| Сервис | Что делает | Функции спецификации |
|---|---|---|
| `ConnectionService` | ключи провайдеров: сохранить с проверкой, статус, маска (последние 4 символа), удалить | F1.1–F1.3 |
| `CatalogService` | единый каталог моделей всех подключённых провайдеров, кеш в БД, поиск и фильтры, карточка модели | F2.1–F2.3, F2.4 частично |
| `ModelSetService` | именованные наборы и быстрый выбор | F3.1, F3.2 |
| `GenerationService` | проверка перед запуском, оценка цены, запуск группы, отмена, повтор, подписка на события | F4.2–F4.4, F4.9, F5.1–F5.5, F8.1–F8.2 |
| `GalleryService` | история групп, открыть группу в композере, избранное, удаление | F6.4, F7.1, F7.3 |
| `SpendingService` | расходы по заданиям и группам | F8.1, F8.2 (экран расходов F8.3 позже) |

Ядро не возвращает готовых текстов для пользователя: ошибки, замены параметров и состояния приходят кодами (`ProviderErrorKind`, `ParameterChange`, `JobState`), локализуют их оболочки (ru и en, F10.1).

## Провайдеры и задания
Контракт провайдера и жизненный цикл задания: `docs/decisions/0003-provider-contract-and-jobs.md`. Коротко:
- `IImageProvider`: проверка ключа, список моделей, описание модели по идентификатору, `SubmitAsync` → `ProviderJobHandle`, `WatchAsync(handle)` → поток `JobUpdate`, `CancelAsync` → `CancelOutcome` (отменено, запрос отправлен, не поддерживается).
- fal: очередь (submit, status с позицией, result, cancel), `has_nsfw_concepts` → состояние «отфильтровано». Replicate: предсказание, опрос, отмена, `Cancel-After`. OpenRouter: синхронный `POST /api/v1/images`, результат и `usage.cost` приходят в ответе, Img2Text через chat completions.
- Webhook не используется ни в одной оболочке: опрос статуса работает и без публичного адреса.
- Ключ читается из `ISecretStore` в момент вызова, в журналы и исключения не попадает.

## Потоки данных
Основной сценарий:
1. Оболочка вызывает `GenerationService.PrepareAsync(request, models)`: `ParameterMapper` строит для каждой модели план (что применено, заменено, отброшено, сколько запросов), `CostEstimator` даёт оценку (точную или «≈»). Пользователь видит это до запуска (F4.3, F4.4).
2. `StartAsync` записывает группу и задания в БД (состояние «в очереди») и возвращает их сразу, чтобы карточки заняли место.
3. Для каждого задания отдельная задача .NET: семафор провайдера (лимит одновременных заданий), `SubmitAsync`, запись `ProviderJobHandle` в БД, затем `WatchAsync`. Сбой одного задания не трогает остальные (F5.2).
4. На «готово» файл сразу скачивается (или берётся из base64) и пишется в `IResultFileStore`, запись результата и фактическая цена — в БД; только после этого оболочка получает событие (F7.2).
5. События идут через подписку `SubscribeAsync(groupId)` (`IAsyncEnumerable<GroupEvent>` на `Channel`). MAUI переносит их в UI-поток, Web отдаёт браузеру потоком событий.
6. После перезапуска приложения `GenerationService` находит незавершённые задания с сохранённым handle и продолжает опрос (fal, Replicate). Задание OpenRouter без ответа помечается ошибкой «прервано»: у OpenRouter нечего опрашивать.

Img2Text (F4.9) идёт тем же путём с видом генерации `ImageToText`: результатом становится текст, а не файл; в промпт он подставляется только действием пользователя.

## Оболочки
**MAUI (`ImageGen.Maui`)**: Shell с пятью разделами (Студия, Галерея, Модели, Расходы, Настройки), на телефоне вкладки внизу, на широком экране боковая панель. `ISecretStore` на `SecureStorage`, `IAppPaths` на `FileSystem.AppDataDirectory`, `IGallerySaver` на MediaStore (Android) и Photos (iOS). Задания выполняются в процессе приложения; если ОС выгрузила приложение, они продолжаются при следующем запуске (шаг 6 выше). Локализация через `.resx`.

**Web (`ImageGen.Web`)**: однопользовательский. Вход по форме, пароль из переменной окружения, cookie-сессия. `ISecretStore` — файл, зашифрованный ASP.NET Core Data Protection, ключи шифрования в томе. `GenerationService` живёт в процессе сервера как singleton, поэтому задания идут и при закрытой вкладке. Vue-фронт собирается в `wwwroot` и отдаётся тем же процессом. Docker: многоэтапная сборка (node → dotnet publish → aspnet), БД, файлы результатов, секреты и ключи Data Protection в томе `/data`.

## Внешние системы
fal.ai, Replicate, OpenRouter (HTTP API через `IHttpClientFactory`). Хранилища на устройстве или сервере: SQLite, каталог файлов результатов, хранилище секретов платформы или зашифрованный файл секретов. Docker: образ Web-оболочки.

## Состояние кода: проект-пример `ImageGen/ImageGen/`
Это MAUI-приложение «проекты и задачи» (шаблон), а не клиент генерации изображений. Один проект `ImageGen/ImageGen/ImageGen.csproj`, `SingleProject`, целевые платформы Android, iOS, Mac Catalyst, Windows (Linux в шаблоне нет). Ссылается на `ImageGen.Core`. Слои внутри проекта:

| Каталог | Что внутри |
|---|---|
| `Data/` | репозитории на `Microsoft.Data.Sqlite`, сидирование из `Resources/Raw/SeedData.json`, `JsonContext` |
| `Models/` | POCO-модели, часть с UI-типами |
| `PageModels/` | MVVM-логика на `CommunityToolkit.Mvvm` |
| `Pages/`, `Pages/Controls/` | XAML-страницы и переиспользуемые контролы |
| `Services/` | обработчик ошибок для UI |
| `Utilities/` | расширения |
| `Resources/` | стили, цвета, шрифты, иконки, сырые ассеты |
| `Platforms/` | точки входа и манифесты платформ |

`ImageGen/ImageGen.Core/` сейчас содержит только пустые `internal`-интерфейсы в `Integration/` (`IProvider`, `IGeneration`, `ITextToImage`, `IImageToImage`, `IImangeToText`). Они заменяются раскладкой выше в задаче `tasks/v0.1/001`.

Что переиспользовать из примера, а что нет, расписано в `docs/knowledge/example-patterns.md` (разделы «Брать» и «Не переносить»).

## Ограничения
- Платформы: MAUI не поддерживает Linux; Linux обслуживает Web-оболочка (`docs/status.md`).
- Безопасность: ключи провайдеров только в шифрованном хранилище; в Web-варианте доступ закрыт паролем из переменной окружения; ключи и пароль не попадают в журналы, экспорт и репозиторий.
- Производительность: задания идут параллельно, UI не блокируется, карточки резервируют место сразу (сетка не прыгает).
- Совместимость: ссылки результатов провайдеров живут от часа, поэтому файл сохраняется сразу.
- Trimming и AOT на iOS: в ядре и хранилище без рефлексии (JSON через source generation, SQL через ADO.NET).
