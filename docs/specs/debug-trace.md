# Отладочный трейс (debug trace)

Задача: 085. Назначение: по записи лога написать юнит-тест, который повторяет проблему на моке провайдера.

## Включение

- Выключено по умолчанию. Вне режима не создаётся ни одной записи; обычное логирование (045) не меняется.
- Переменная `IMAGEGEN_DEBUG_TRACE`: `1`, `true`, `on`, `yes` — включить; любое другое значение — выключить. Важнее файла.
- Или поле `debugTrace: true` в `logging.json` в папке данных (то же, что для уровня логов, см. `docs/knowledge/dev-environment.md`, раздел «Логи»).
- Уровень логов (`IMAGEGEN_LOG_LEVEL`) на трейс не влияет.

## Где лежит

`{папка данных}/debug/<yyyy-MM-dd>/trace.jsonl` (UTC-дата записи). Рядом — файлы изображений.

- Одна строка файла — одна запись (JSON, UTF-8).
- Изображение — отдельный файл `<имя блоба>.<расширение>` в той же папке, расширение по типу (`png`, `jpg`, `webp`, `gif`, иначе `bin`).

## Формат: `debug-trace/1`

Поля верхнего уровня каждой строки:

| Поле | Значение |
|---|---|
| `schema` | всегда `debug-trace/1` |
| `time` | время записи, ISO 8601 (`O`) |
| `kind` | вид записи (см. ниже) |
| `fields` | объект «ключ → текст»; набор ключей зависит от `kind` |
| `blobs` | массив `{name, file, mediaType}`; `file` — имя файла в папке дня; у `b64_json` `mediaType` — `application/octet-stream` (тип из ответа не берётся) |

Виды записей (`kind`):

- `user-input` — ввод в ассистенте. Поля: `session`, `text` (текст сообщения), `attachments` (id вложений через запятую; сами файлы уже лежат в папке данных).
- `generation-request` — запуск генерации. Поля: `session`, `prompt` (нормализованный), `models` (`провайдер/модель` через запятую), `seeds`, `aspectRatio`, `resolution`, `outputFormat`, `quality`, `attachments`.
- `http-request` — запрос к провайдеру. Поля: `id` (связывает с ответом), `method`, `path` (без строки запроса), `headers`, `body`.
- `http-response` — ответ провайдера. Поля: `id`, `method`, `path`, `status`, `elapsedMs`, `headers`, `body`.

Ключ отсутствует в `fields`, если значения нет (например, `body` у запроса без тела).

Пример строки ответа с картинкой (переносы добавлены для чтения):

    {"schema":"debug-trace/1","time":"2026-10-29T09:30:00.0000000+00:00","kind":"http-response",
     "fields":{"id":"a1b2c3d4","method":"POST","path":"/api/v1/chat/completions","status":"200",
               "elapsedMs":"812","headers":"Content-Type: application/json","body":"{\"url\":\"blob:a1b2c3d4-response-0\"}"},
     "blobs":[{"name":"a1b2c3d4-response-0","file":"a1b2c3d4-response-0.png","mediaType":"image/png"}]}

В `body` картинка заменена ссылкой `blob:<name>`; её байты — в файле из `blobs[].file`.

## Что не пишется

- Заголовки кроме allowlist: `Content-Type`, `Accept`, `User-Agent`, `Content-Length`. `Authorization`, `x-api-key` и любые другие заголовки отбрасываются, а не маскируются.
- Тело ответа проверки ключа (`GET .../key`) и его заголовки не пишутся вовсе: в нём имя аккаунта.
- Ключи провайдеров — ни в каком виде (`AGENTS.md`).
- В теле значения полей аккаунта (`label`, `key`, `api_key`, `*_user_id`) заменяются на `<redacted>`, как в логах 045 (`BodyRedactor.RedactAccountFields`).
- Тело не обрезается: трейс должен содержать весь ответ, иначе по нему не воспроизвести проблему. Обрезка (`BodyRedactor.MaxLength`) относится только к строкам обычного лога 045.
- Изображения не остаются в тексте: data-URL изображений и поля `b64_json` выносятся в файлы. Data-URL, которую не удалось декодировать, остаётся в тексте как есть (это не ключ).

## Воспроизведение в тесте

Из записи `http-response` собирается мок: `body` с заменой `blob:<name>` на data-URL из файла, `status` из поля `status`. Примеры: `ImageGen.Core.Tests/Diagnostics/DebugTraceProviderReplayTests.cs` (генерация через `OpenRouterClient`, мок из записи) и `DebugTraceHandlerTests.cs` (восстановление ответа на уровне HTTP). Запись `generation-request` даёт модели и сиды для повторного запуска.

## Реализация

- `ImageGen.Core/Diagnostics`: `IDebugTrace`, `TraceRecord`, `TraceBlob`, `NullDebugTrace` (по умолчанию), `TraceRedaction` (allowlist заголовков, вынос картинок).
- `ImageGen.Core/Http/LoggingHttpHandler` пишет `http-request` и `http-response`; `AssistantService` пишет `user-input`; `GenerationService` пишет `generation-request`.
- `ImageGen.Presentation/Logging`: `DebugTraceSettings` (включение, `Create`), `FileDebugTrace` (запись в файлы).
- Оболочки: `AppServices.Create(debugTrace:)` в Avalonia, `MauiProgram` регистрирует трейс до `AddImageGenCore`; `AddImageGenCore` ставит `NullDebugTrace`, если трейс не зарегистрирован.

## Открытые вопросы

- Размер папки `debug/`: очистки нет (в отличие от логов, где 14 дней). Неизвестно, нужна ли ротация; пока трейс включается вручную и удаляется пользователем.
- Изображения-вложения пользователя в `user-input` и `generation-request` записаны ссылками на id, без копий байтов.
