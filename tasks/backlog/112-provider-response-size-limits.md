---
id: 112
status: planned
assignee:
claimed:
created: 2026-10-29
---

# Ограничить размер ответов провайдеров без удаления debug-логов

## Описание
Найдено ревью 2026-10-29. `LoggingHttpHandler` безусловно читает весь response в `byte[]` ради размера на Information. Затем provider clients снова читают полный JSON или изображение. Удалённый endpoint может вернуть неограниченное тело и исчерпать память. Решение владельца в чате 2026-10-29: логирование контента и подробностей ошибок под Debug должно сохраниться.

Зависит от: 106, 109.
Среда: Linux или Windows.

## Критерии готовности
- Максимум JSON body: 8 MiB; максимум binary image response: 50 MiB. Значения находятся в одном типе констант и покрыты boundary-тестами.
- `Content-Length` больше лимита отклоняется до чтения. При отсутствующем или ложном `Content-Length` поток читается не дальше `limit + 1`.
- На Information handler не буферизует response только ради размера; использует `Content-Length`, если он есть, иначе пишет неизвестный размер.
- На Debug тела запросов/ответов и подробности ошибок продолжают логироваться и редактироваться существующими `BodyRedactor`/`TraceRedaction`.
- Debug trace остаётся отдельной явной настройкой. Ключи и Authorization не логируются.
- Превышение лимита становится контролируемой `ProviderException`, а generation получает terminal `Failed` через задачу 106.

## Анализ
- `LoggingHttpHandler` строка чтения `ReadAsByteArrayAsync` выполняется независимо от уровня логирования.
- `ProviderClientBase.SendJsonAsync` читает строку без лимита; отдельный `FalImageProvider.SendFalJsonAsync` без лимита читает status/result/catalog; `FalImageProvider.DownloadAsync` читает `byte[]` без лимита.
- Base64 image внутри JSON ограничивается JSON-лимитом; это осознанно. Если реальные ответы OpenRouter больше 8 MiB, тест/ручная проверка должна заблокировать задачу и запросить новое число, а не молча поднять лимит.
- Пересекается с 109 по fal и с debug trace 085 по `LoggingHttpHandler`; debug-поведение не удалять.

## План
Для исполнителя (Haiku/Luna): не оптимизируй весь HTTP pipeline. Добавь один bounded reader и замени только названные чтения. Все числа и error mapping переноси дословно.

Прочитай: `LoggingHttpHandler.cs`, `ProviderClientBase.cs`, `FalImageProvider.cs`, `LoggingHttpHandlerTests.cs`, provider tests, `docs/conventions.md` раздел «Журнал».

1. Создать internal `ProviderResponseLimits` с `JsonBytes = 8 * 1024 * 1024` и `ImageBytes = 50 * 1024 * 1024`.
2. Создать internal `BoundedHttpContentReader`: precheck nullable `Content-Length`; потоковое чтение в buffer не больше limit+1; методы bytes и UTF-8 text; превышение бросает отдельное internal исключение с limit и фактическим минимумом размера, без содержимого body.
3. Boundary-тесты reader: limit, limit+1, отсутствующий Content-Length, заниженный Content-Length, chunked stream.
4. В `ProviderClientBase.SendJsonAsync` и `FalImageProvider.SendFalJsonAsync` читать JSON через bounded reader и преобразовать превышение в `ProviderException(ErrorKind.Unknown, понятное сообщение)`. Добавить oversized-тесты отдельно для обычного JSON и fal status/result.
5. В fal image download использовать image limit и то же контролируемое преобразование.
6. В `LoggingHttpHandler` на Information не читать body: размер брать из header или логировать nullable/`unknown`. На Debug/trace выбрать предел по `Content-Type`: JSON-типы — 8 MiB, `image/*` — 50 MiB, отсутствующий/прочий тип — безопасный fallback 50 MiB; downstream parser всё равно применяет свой более узкий предел. Credential check по-прежнему не пишет body.
7. Тестами доказать: при Information custom content stream ещё не прочитан до передачи caller; при Debug маленькое тело логируется и остаётся читаемым caller; oversized JSON/image дают контролируемую ошибку; секреты редактируются как раньше.
8. В «Отчёте» отдельно подтвердить сохранение debug-контента по решению владельца.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Providers/ProviderResponseLimits.cs` (новый), `ImageGen/ImageGen.Core/Providers/BoundedHttpContentReader.cs` (новый), `ImageGen/ImageGen.Core/Providers/ProviderClientBase.cs`, `ImageGen/ImageGen.Core/Fal/FalImageProvider.cs`, `ImageGen/ImageGen.Core/Http/LoggingHttpHandler.cs`, `ImageGen/ImageGen.Core.Tests/Providers/BoundedHttpContentReaderTests.cs` (новый), `ImageGen/ImageGen.Core.Tests/Http/LoggingHttpHandlerTests.cs`, `ImageGen/ImageGen.Core.Tests/Fal/FalImageProviderTests.cs`

## Отчёт

## Комментарии
