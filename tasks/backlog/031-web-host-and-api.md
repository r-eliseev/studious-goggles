---
id: 031
status: todo
assignee:
claimed:
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

## План
Этап подготовки плана:
1. Сверить docs/architecture.md и существующие сервисы. Зафиксировать target net10.0, ImageGen.Web/ImageGen.Web.Tests, зависимости на Core/Data, persistent DataDirectory, startup/recovery/shutdown и конфигурацию password env без дефолтного пароля.
2. Описать API contract: endpoints/DTO, ids и validation, JSON enum/error codes, HTTP statuses; исключить наружу provider keys, raw exceptions и JobJson. Для изображения — lookup GenerationId, проверка принадлежности/доступа, безопасное чтение relative ImagePath.
3. Описать login/logout, cookie SameSite/HttpOnly/Secure policy в reverse-proxy deployment, server-side verification и CSRF token для mutations, bounded неудачные login попытки. Не внедрять OAuth/multi-user, которых продукт не требует.
4. Определить encrypted ISecretStore file format и atomic write, persistent Data Protection key ring/application name; тестирование рестарта с тем же и другим key ring без печати секретов.
5. Выбрать формат event stream (SSE либо иной один протокол) и правило reconnect: snapshot/refetch до incremental updates, bounded fan-out, slow/disconnected subscriber cleanup. Не реализовывать неограниченный replay всей истории и не обещать exactly-once доставку.
6. Для каждой выделенной части составить Файлы, контракты и тесты WebApplicationFactory с временной БД/фейковыми провайдерами. Явно тестировать zero calls до авторизации, два одновременных клиента получают update, disconnect не отменяет run, shutdown не теряет наблюдение за job.
7. Опубликовать декомпозицию и планы, поставить planned и ждать одобрения. Будущие проверки: Web.Tests, Core/Data regression по затронутым контрактам, format затронутых проектов, `bash scripts/check.sh`; docs/architecture/spec и отчёт/release-task по docs/tasks.md.

Файлы для анализа: `ImageGen/ImageGen.Core/CoreServiceCollectionExtensions.cs`, `ImageGen/ImageGen.Data/DataServiceCollectionExtensions.cs`, `ImageGen/ImageGen.Core/Platform/ISecretStore.cs`, `ImageGen/ImageGen.Core/Platform/IAppPaths.cs`, `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `ImageGen/ImageGen.Presentation/Services/AppInitializer.cs`, `ImageGen/ImageGen.slnx`, `ImageGen/Directory.Packages.props`, `docs/architecture.md`. Новые файлы/пакеты назвать в одобряемых планах; существующие scripts/workflows без отдельной просьбы не менять.

## Отчёт

## Комментарии

### 2026-10-29, claude-536f
Заблокировано: Детализация после задачи 016 (ручная проверка у владельца). Задача остаётся без плана до снятия блокера; код не пишется.

### 2026-10-29, codex-backlog-review
Устаревший блокер ожиданием016 снят: код скелета существует. Задача допускается к планированию, но не получила автоматического одобрения реализации. Исторические комментарии сохранены.
