---
id: 045
status: approved
assignee:
claimed:
created: 2026-10-28
---

# Подробное логирование всего приложения

## Описание
Приоритет владельца (2026-10-28): приложение отлаживается в первую очередь по логам. Нужно подробное структурированное логирование во всех слоях — ядро, хранилище, слой представления, обе оболочки — с записью в файл в папке данных, чтобы по одному файлу логов можно было восстановить, что происходило: запуск, навигация, действия пользователя, запросы к провайдерам, переходы генераций, ошибки. Ключи и пароли в логи не попадают никогда.

Зависит от: — (первая в очереди после текущей работы; см. «Анализ», пересечение с 036).
Среда: Linux (с Android SDK для MAUI) или Windows. Windows-цель MAUI проверяет CI.

## Критерии готовности
- И MAUI, и Avalonia пишут логи в файл `<папка данных>/logs/imagegen-<дата>.log` (по файлу в день, хранятся 14 дней); путь к логам виден в интерфейсе или в первой строке лога.
- Уровень по умолчанию: Debug-сборка — `Debug`, Release — `Information`; меняется без пересборки (переменная окружения `IMAGEGEN_LOG_LEVEL` и файл настроек, см. план).
- В логах есть: запуск (версия, ОС, оболочка, папка данных), миграции БД, каждый HTTP-запрос к провайдеру (метод, путь, код, длительность, размер), каждая смена состояния генерации, ход ассистента, навигация, команды экранов, все необработанные исключения.
- Ключей нет ни в одной строке: тест сохраняет ключ, проходит запросы и проверяет, что файл лога не содержит ключ; заголовок `Authorization` всегда скрыт; base64 картинок в телах не пишется.
- Тест-кейсы из плана проходят; `Debug.WriteLine` в коде приложения не осталось.

## Анализ
- Сейчас: `ILogger` есть только в `AssistantService` и `GenerationService`; MAUI добавляет `AddDebug()`, Avalonia — только `LogToTrace()` самой Avalonia; `FireAndForgetSafeAsync` пишет ошибки в `Debug.WriteLine` и теряет их в Release. Файла логов нет, необработанные исключения не перехватываются.
- `Core` уже ссылается на `Microsoft.Extensions.Logging.Abstractions` (`docs/architecture.md`): ядро и Presentation пишут через `ILogger<T>`, а запись в файл настраивают оболочки. Ядро и Presentation не знают о библиотеке записи.
- Запись в файл: стандартного файлового провайдера у `Microsoft.Extensions.Logging` нет. Варианты: Serilog (`Serilog.Extensions.Logging` + `Serilog.Sinks.File`: ротация, хранение, структурированный вывод) или свой небольшой `ILoggerProvider`. Выбор — ADR в этой задаче (номер — следующий свободный). Рекомендация плана — Serilog: ротация и потокобезопасная запись уже готовы.
- Ключи: OpenRouter получает ключ в заголовке `Authorization: Bearer`; `IHttpClientFactory` по умолчанию логирует запросы, но заголовки — нет (а при включении нужно скрывать). Тело ответа картинки — большой base64.
- Пересечение с 036 (сейчас в работе в ветке `feat/queue-providers-assistant`): обе задачи меняют `CoreServiceCollectionExtensions` и HTTP-клиентов провайдеров. Делать 045 после мерджа 036, начинать от свежего `main`.
- Промпты и тексты чата — данные пользователя, но лог локальный и нужен для отладки: на уровне `Debug` они пишутся целиком, на `Information` — только длина и id.

## План
1. **Конфигурация логов (оболочки):**
   - Общий помощник в Presentation: `LoggingSetup.Configure(ILoggingBuilder, IAppPaths, LogOptions)` — без ссылки на конкретную библиотеку в типах Presentation, библиотека подключается в оболочках (по ADR).
   - `LogOptions`: уровень (из `IMAGEGEN_LOG_LEVEL`, затем `logging.json` в папке данных, затем значение по сборке), папка логов, срок хранения 14 дней, размер файла до 10 МБ с переходом на следующий.
   - Формат строки: время UTC с миллисекундами, уровень, категория, id потока, область (scope), сообщение, исключение целиком.
   - MAUI: файл + `AddDebug` (+ logcat на Android). Avalonia: файл + консоль; логи самой Avalonia (`LogToTrace`) перенаправить в тот же `ILogger` на уровне `Warning`.
   - Первая запись при старте: версия сборки, оболочка (MAUI/Avalonia), ОС и архитектура, .NET, папка данных, путь к файлу логов, уровень.
2. **Необработанные исключения:** `AppDomain.CurrentDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`, у Avalonia — `Dispatcher.UIThread.UnhandledException`, у MAUI — платформенные обработчики (Android `AndroidEnvironment.UnhandledExceptionRaiser`, Windows `Microsoft.UI.Xaml.Application.UnhandledException`) → `Critical` с исключением, затем сброс буфера лога.
   - `FireAndForgetSafeAsync` получает статический доступ к логгеру (`TaskExtensions.Logger`, задаётся при старте) и пишет `Error` вместо `Debug.WriteLine`.
3. **HTTP провайдеров:** `LoggingHttpHandler : DelegatingHandler` в Core (подключается ко всем клиентам провайдеров через `AddHttpMessageHandler`):
   - `Information`: метод, путь без строки запроса с секретами, код ответа, длительность, размер тела;
   - `Debug`: тело запроса и ответа, обрезанное до 4 КБ; поля `b64_json` и data URL заменяются на `<base64, N bytes>`;
   - заголовок `Authorization` и любые заголовки с ключами — `<redacted>`; ключ не пишется ни в каком виде;
   - ошибки сети и таймауты — `Warning` с длительностью.
4. **Ядро и хранилище:**
   - `GenerationService`: каждая смена статуса — `Information` с областью `{SessionId, RunId, GenerationId}`: модель, сид, применён ли сид, статус, длительность, цена, `ErrorKind`; создание прогона — модели × сиды; восстановление «Interrupted» — число записей.
   - `AssistantService`: модель, число сообщений истории, вызванные инструменты, итог хода; на `Debug` — текст сообщения и промпт.
   - `SessionService`, `ApiKeyService` (результат проверки ключа без самого ключа, маска не пишется), `ModelCatalogService` (откуда каталог: кеш или API, число моделей), `SqliteDatabase` (версия схемы до и после, применённые миграции, длительность), `FileImageStore` (путь и размер сохранённого файла).
5. **Presentation и оболочки:**
   - навигация: каждый переход (маршрут, параметры) — `Information`; диалоги — `Debug` (заголовок, ответ);
   - команды моделей страниц: начало и конец (с длительностью) — `Debug`, ошибка — `Error` с исключением (через общий помощник, а не в каждом методе);
   - хранилище ключей: какой бэкенд выбран (DPAPI, Secret Service, Keychain, память) и почему — `Information`.
6. **Правило в `docs/conventions.md`:** новый код логирует через `ILogger<T>` с шаблонами сообщений (не интерполяцией), id в областях; что запрещено писать (ключи, пароли, base64); уровни по смыслу.
7. **Документация:** ADR о библиотеке записи; `docs/architecture.md` (логирование: кто настраивает, куда пишет); `docs/knowledge/dev-environment.md` (где лежат логи на каждой ОС, как поднять уровень); `AGENTS.md` («Команды»: где смотреть логи при отладке).

### Тест-кейсы (условие → результат)
- `LoggingHttpHandler`: запрос с `Authorization: Bearer sk-test-123` → ни в одной записи нет `sk-test-123`, есть `<redacted>`.
- `LoggingHttpHandler` на `Debug`: ответ с `b64_json` длиной 1 МБ → в записи `<base64, … bytes>`, длина записи не больше предела.
- `LoggingHttpHandler`: ответ 429 → запись `Information` с кодом 429 и длительностью; сбой сети → `Warning`.
- `GenerationService`: успешная генерация → записи Queued→Running→Succeeded с одинаковым `GenerationId` в области; модерация → запись со статусом Filtered.
- `AssistantService` на `Information` → нет текста сообщения; на `Debug` → есть.
- Интеграционный (Avalonia headless или Data.Tests): сохранить ключ через `ApiKeyService`, сделать ход ассистента и генерацию на подменённом HTTP, прочитать файл лога → ключа нет, есть строки старта, навигации, HTTP и генерации.
- `LogOptions`: `IMAGEGEN_LOG_LEVEL=Trace` перекрывает `logging.json`; неверное значение → уровень по умолчанию и предупреждение в логе.
- `FireAndForgetSafeAsync` с падающей задачей → запись `Error` с исключением.
- Ротация: файлы старше 14 дней удаляются при старте (тест на временной папке с поддельными датами).

### Команды проверки
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format <каждый изменённый проект> --verify-no-changes
scripts/check.sh
```
Ручная проверка: запустить Avalonia на Linux, пройти «ключ → сессия → сообщение», открыть файл лога — видна вся цепочка; то же в задаче ручной проверки 046.

Файлы: `ImageGen/Directory.Packages.props`, `ImageGen/ImageGen.Core/Http/LoggingHttpHandler.cs` (новый), `ImageGen/ImageGen.Core/CoreServiceCollectionExtensions.cs`, `ImageGen/ImageGen.Core/Services/*.cs`, `ImageGen/ImageGen.Data/SqliteDatabase.cs`, `ImageGen/ImageGen.Data/FileImageStore.cs`, `ImageGen/ImageGen.Data/Repositories/*.cs` (по необходимости), `ImageGen/ImageGen.Presentation/Logging/**` (новый), `ImageGen/ImageGen.Presentation/Utilities/TaskExtensions.cs`, `ImageGen/ImageGen.Presentation/PageModels/*.cs`, `ImageGen/ImageGen.Maui/MauiProgram.cs`, `ImageGen/ImageGen.Maui/Platforms/**` (обработчики исключений), `ImageGen/ImageGen.Maui/Services/*.cs`, `ImageGen/ImageGen.Avalonia/Program.cs`, `ImageGen/ImageGen.Avalonia/App.axaml.cs`, `ImageGen/ImageGen.Avalonia/Services/**`, тесты во всех четырёх тестовых проектах, `docs/decisions/NNNN-logging.md` (новый), `docs/conventions.md`, `docs/architecture.md`, `docs/knowledge/dev-environment.md`, `AGENTS.md`

## Отчёт

## Комментарии

### 2026-10-28, владелец (ответы в чате)
План одобрен как есть. Библиотека записи в файл — Serilog (`Serilog.Extensions.Logging` + `Serilog.Sinks.File`), решение оформить ADR в этой задаче.
Ветка: вместо `task/045-…` работаю в designated-ветке `ccr-07d63c19-ma1lxw` (по инструкции сессии), от `origin/main` (`d5c5c91`, с 036).
