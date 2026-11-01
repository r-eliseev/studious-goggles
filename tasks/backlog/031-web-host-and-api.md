---
id: 031
status: in-progress
assignee: codex-gpt6
claimed: 2026-11-01T19:43:19Z
created: 2026-10-27
---

# Web-оболочка: хост, вход, API

## Описание
ASP.NET Core: вход по паролю из переменной окружения, cookie-сессия, секреты под Data Protection, API над сервисами ядра, фоновое выполнение и поток событий браузеру (`docs/architecture.md`, «Целевая раскладка»). Нужна для утверждённого Web/Vue/Docker сценария; Core не должен зависеть от ASP.NET Core.

Зависит от: —
Среда: Linux или Windows.

Код скелета уже есть. 016 не блокирует планирование; статус `todo`, требуется разбить большой объём и одобрить контракты/планы до реализации.

## Критерии готовности
- Без входа защищённый API и event stream отвечают 401 JSON, не HTML/redirect; неверный пароль не создаёт cookie.
- API работает поверх общих Core/Data сервисов, не дублирует правила генерации и не принимает произвольный путь к файлу.
- Секреты шифруются Data Protection, key ring и данные находятся в сохраняемой папке; API никогда не возвращает исходный ключ после сохранения.
- Прогон продолжает выполняться после закрытия вкладки/HTTP-request; события ограничены по буферу, потеря соединения восстанавливается перечитыванием состояния.
- Хост не запускается с пустым паролем; state-changing запросы защищены от CSRF, авторизация проверяется и при получении картинок.
- Автотесты покрывают auth, CRUD/ошибки, шифрованные секреты/restart и lifecycle генерации.

## Анализ
Проекта ImageGen.Web сейчас нет. Повторно использовать Core/Data через DI; типы Presentation, содержащие desktop navigation/dialog contracts, не превращать в серверные endpoint-обработчики. `ISecretStore` и `IAppPaths` имеют небольшие платформенные интерфейсы — Web реализует их сам. Для событий использовать отдельную bounded подписку Channel на браузер, а не общий Channel, где вкладки будут конкурировать за обновления.

Ключевой lifetime: caller cancellation HTTP-запроса и disconnect event stream не должны отменять уже принятый run. Инициализация БД/recovery выполняется один раз до приёма API, background service останавливается согласованно при shutdown. Текущий `AppInitializer` живёт в Presentation — план должен явно решить извлечение или серверную композицию, а не создавать зависимость Core от Presentation/ASP.NET.

Для Haiku/Luna это эпик, не один патч. Нужны последовательные маленькие части: (1) host/paths/DI/startup integration tests; (2) auth/session/CSRF; (3) Data Protection secret store; (4) DTO и session/model/key endpoints; (5) chat/generation/image endpoints; (6) bounded event stream/reconnect. Номера и зависимые задачи назначаются при одобрении декомпозиции.


Уточнение 2026-10-31 (claude-audit, по коду `b4d00d9`): платформенные контракты маленькие — `ISecretStore` (Get/Set/Remove по имени) и `IAppPaths.DataDirectory`. События ядра: `GenerationService.GenerationUpdated` и `SessionChangeNotifier.SessionChanged` — обычные .NET события, синхронные. `AppInitializer` (Presentation) делает две вещи: `SqliteDatabase.InitializeAsync` и `GenerationService.RecoverInterruptedAsync` — Web повторяет их сам, без ссылки на Presentation. Пакета `Microsoft.AspNetCore.Mvc.Testing` в `Directory.Packages.props` нет — нужен ADR.

## План
Шесть этапов, каждый — отдельный PR в этой задаче, по порядку. Сначала падающий тест (ub-mode). Перед этапом 1 — ADR `0025-web-host.md`: ASP.NET Core minimal API в `ImageGen.Web` (FrameworkReference, без новых пакетов в продукте), тестовый пакет `Microsoft.AspNetCore.Mvc.Testing` (MIT, проверить лицензию при добавлении), SSE для событий, cookie + antiforgery.

**1. Хост и запуск.** Проекты `ImageGen.Web` (net10.0, ссылки Core, Data) и `ImageGen.Web.Tests` (xUnit v3, `WebApplicationFactory`), оба в `ImageGen.slnx`. `WebAppPaths` (папка данных из `IMAGEGEN_DATA_DIR`, иначе `/data`; создаётся при старте). `StartupInitializer : IHostedService` — миграции и `RecoverInterruptedAsync` до приёма запросов. Без `IMAGEGEN_WEB_PASSWORD` (пустого или отсутствующего) хост не стартует с понятной ошибкой. `GET /api/health` без авторизации — только `{"status":"ok"}`. Тесты: старт на временной папке, отсутствие пароля → ошибка, повторный старт не переприменяет миграции.

**2. Вход и защита.** `POST /api/auth/login {password}` — сравнение SHA-256 через `CryptographicOperations.FixedTimeEquals`, cookie `HttpOnly`, `SameSite=Strict`, `Secure` (кроме `localhost`), срок 30 дней со скольжением; ограничение 5 попыток в минуту на IP (`Microsoft.AspNetCore.RateLimiting`). `POST /api/auth/logout`, `GET /api/auth/me`. Неавторизованный `/api/*` → `401` JSON, без редиректа. Изменяющие запросы требуют заголовок antiforgery (`GET /api/auth/csrf` выдаёт токен). Тесты: неверный пароль без cookie, 401 JSON, CSRF без токена → 400, лимит попыток.

**3. Ключи провайдеров.** `DataProtectionSecretStore : ISecretStore` — файл `secrets.json` в папке данных, значения через `IDataProtector` (purpose `ImageGen.Secrets`), запись атомарно (временный файл + rename); key ring — `PersistKeysToFileSystem(<data>/keys)`, `SetApplicationName("ImageGen")`. Тесты: значение в файле не открытым текстом; перезапуск с тем же key ring читает; с другим key ring — ключ считается отсутствующим, не падает.

**4. Сессии, модели, ключи (DTO).** `GET/POST /api/sessions`, `PATCH /api/sessions/{id}` (название), `DELETE /api/sessions/{id}`, `GET /api/sessions/{id}/timeline`, `PUT /api/sessions/{id}/settings`, `GET /api/models/image`, `GET /api/models/assistant`, `GET /api/providers` (статус ключа с последними 4 символами), `PUT/DELETE /api/providers/{id}/key`. Ошибки — `{"error": "<ErrorKind>"}` и HTTP-статус, без текста исключений и `JobJson`. Тесты: CRUD, неизвестный id → 404, ключ никогда не возвращается.

**5. Чат, генерации, картинки.** `POST /api/sessions/{id}/messages` (вложения — `multipart`), `POST /api/generations/{id}/repeat|new-seed|edit|other-model`, `GET /api/generations/{id}/image` — путь берётся только из БД по id, проверка, что файл внутри папки данных. Выполнение прогонов — на singleton `GenerationService`: токен HTTP-запроса не передаётся в фоновую работу, закрытие вкладки не отменяет прогон. Тесты на подменённых провайдерах: прогон завершается после отмены запроса; картинка без входа → 401; чужой путь невозможен.

**6. Поток событий.** `GET /api/events` — SSE (`text/event-stream`): события `generation` (`{generationId, sessionId, status}`) и `session` (`{sessionId}`). Каждый подписчик — свой `Channel` на 256 событий, `DropOldest`; при переполнении отправляется `resync`, клиент перечитывает состояние. Отключение клиента освобождает подписку; heartbeat-комментарий раз в 20 с. Тесты: два клиента получают одно событие; медленный клиент получает `resync`; отключение не отменяет прогон.

Документы: `docs/architecture.md` (раскладка Web), `docs/knowledge/dev-environment.md` (запуск Web), `docs/specs/image-client.md` (Web-часть). Проверки каждого этапа: Web.Tests, Core/Data.Tests при изменении контрактов, `dotnet format` новых проектов, `scripts/check.sh`. Изменения CI — отдельно, с разрешения владельца.

Файлы (по этапам; точные — в отчётах этапов): `docs/decisions/0025-web-host.md`, `ImageGen/ImageGen.slnx`, `ImageGen/Directory.Packages.props`, `ImageGen/ImageGen.Web/ImageGen.Web.csproj`, `Program.cs`, `WebAppPaths.cs`, `StartupInitializer.cs`, `Auth/*`, `Secrets/DataProtectionSecretStore.cs`, `Api/*Endpoints.cs`, `Api/Dtos/*`, `Events/EventHub.cs`, `Events/EventsEndpoint.cs`; `ImageGen/ImageGen.Web.Tests/*`.

## Отчёт

Этапы 1–4: добавлены `ImageGen.Web` и `ImageGen.Web.Tests`, ASP.NET Core minimal API композиция, `IMAGEGEN_DATA_DIR` (по умолчанию `/data`), проверка непустого `IMAGEGEN_WEB_PASSWORD`, hosted startup initializer (миграции SQLite и восстановление generation jobs), открытый `GET /api/health`. Хранилище `ISecretStore` реализовано через ASP.NET Core Data Protection с постоянным key ring. Вход по паролю сравнивает SHA-256 через `FixedTimeEquals`, применяет лимит 5 попыток за минуту и выдаёт 30-дневную cookie (`HttpOnly`, `SameSite=Strict`, `Secure` вне localhost); защищённые auth endpoints возвращают JSON 401 без redirect. Antiforgery token endpoint выдаёт request token, а все изменяющие API-запросы его проверяют. Реализованы DTO endpoints для сессий/timeline, image/assistant моделей и ключей провайдеров; timeline не возвращает `JobJson`, ключи только проверяются и маскируются, исходное значение не возвращается. Неизвестная сессия/провайдер даёт 404, ошибки API скрывают текст исключения. Добавлен ADR 0025. Этапы 5–6 ещё не реализованы.

Проверено: Web.Tests 11/11, форматирование Web и Web.Tests, `scripts/check.sh`. `Microsoft.AspNetCore.Mvc.Testing` имеет MIT лицензию; использован патч 10.0.14. ASP.NET Core Data Protection Extensions входит в shared framework и не требует отдельной ссылки на пакет.

## Комментарии

### 2026-10-29, claude-536f
Заблокировано: Детализация после задачи 016 (ручная проверка у владельца). Задача остаётся без плана до снятия блокера; код не пишется.

### 2026-10-29, codex-backlog-review
Устаревший блокер ожиданием016 снят: код скелета существует. Задача допускается к планированию, но не получила автоматического одобрения реализации. Исторические комментарии сохранены.

### 2026-10-31, claude-audit
План готов, статус planned. Допущения: пароль из `IMAGEGEN_WEB_PASSWORD`, папка данных из `IMAGEGEN_DATA_DIR` (по умолчанию `/data`), cookie на 30 дней, лимит 5 попыток входа в минуту, SSE для событий, ADR 0025 до кода.

### 2026-10-31, владелец (в чате, записал claude-audit)
План одобрен вместе с допущениями из комментария выше.
