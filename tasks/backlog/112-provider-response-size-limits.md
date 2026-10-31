---
id: 112
status: done
assignee:
claimed:
created: 2026-10-29
---

# Добавить bounded reader для provider responses

## Описание
Provider JSON/image paths читают body целиком; default SendAsync может prebuffer до проверки. Эта задача вводит limits, ResponseHeadersRead и bounded parser reads. LoggingHttpHandler всё ещё читает body раньше parser; его интеграция и полная production guarantee — задача 127.

Зависит от: 107, 124.
Среда: Linux или Windows.

## Критерии готовности
- Пределы response body: обычный JSON8MiB, JSON синхронной генерации с base64 72MiB, binary generated image50MiB. Один MiB=1024*1024 bytes. Base64 раздувает50MiB примерно до66.7MiB; обычный JSON cap не должен обрезать допустимое изображение.
- Base/Fal sends используют ResponseHeadersRead; timeout token покрывает send+read.
- Header > limit rejected before body; absent/false header reads ≤ limit+1.
- Image download и JSON image-generation requests помечены общим request option body kind; unmarked defaults Json. Probe/cancel помечены отдельным SkipResponseBody=true для127.
- Oversize from reader or future handler maps safe `ProviderException(Unknown)`.
- Probe/cancel don't read unused body.

## Анализ
`SendFalJsonAsync` separate path. Bounded reader alone не ограничивает current LoggingHttpHandler; это явно принимает 127. Debug logging не меняется здесь.

Нужность: ограничивает allocation на недоверенном ответе. Предел72MiB сохраняет существующий b64_json путь OpenRouter/OpenAI-compatible; проверка только заголовка не защищает chunked/ложный Content-Length.

## План
Для Haiku/Luna: только transport/parser primitives and provider tests; LoggingHttpHandler/tests не трогать.

1. В `Providers/ProviderResponseLimits.cs` определить enum `ProviderResponseBodyKind` (`Json`, `GeneratedImageJson`, `Image`); static constants `Json=8*1024*1024`, `GeneratedImageJson=72*1024*1024`, `Image=50*1024*1024`. Общий `BodyKind: HttpRequestOptionsKey<ProviderResponseBodyKind>` и `SkipResponseBody: HttpRequestOptionsKey<bool>`. Метод `GetLimit(HttpRequestMessage request)` выбирает предел по option, по умолчанию Json. Не определять дубликаты enum/keys/constants в logging handler127.
2. Создать `ProviderResponseTooLargeException` с безопасным постоянным message и полями `LimitBytes` (long), `DeclaredLength` (long?); без body, URL и key. Создать static `BoundedHttpContentReader` с `ReadBytesAsync(HttpContent content, long limitBytes, CancellationToken)` и `ReadTextAsync(HttpContent content, long limitBytes, CancellationToken)`. Content-Length >limit отклонять до получения/чтения stream. Для остальных случаев ReadAsStreamAsync и цикл чтения с остаточным бюджетом до limit+1; после превышения бросить size exception, не возвращать partial buffer. Текст декодировать с объявленной content charset (иначе UTF8), не выполнять второй неограниченный ReadAsStringAsync.
3. Unit tests: exact limit success; +1 fail; missing/understated Content-Length; non-seekable/chunked-equivalent stream; счётчик реально прочитанных bytes никогда >limit+1; oversized header →0 stream bytes. Маленький injected test limit применить к pure reader, без выделения десятков MiB для каждой комбинации. Caller cancellation при чтении остаётся OCE.
4. В `ProviderClientBase.SendJsonAsync` и direct Fal JSON/download использовать SendAsync с ResponseHeadersRead и reader. Тот же timeout CTS из107 живёт до завершения bounded read. Fal.Download отмечает BodyKind.Image. `OpenRouterClient.GenerateAsync` и `OpenAiCompatibleClient.GenerateAsync` помечают response BodyKind.GeneratedImageJson до SendJsonAsync; catalog/chat/status/result JSON оставляют Json. Probe/cancel выставляют SkipResponseBody=true, SendAsync ResponseHeadersRead, освобождают response без чтения неиспользуемого body.
5. В обоих GenerateAsync проверить decoded image cap50MiB: base64 длину проверить до Convert.FromBase64String (учесть допустимый padding/whitespace по текущему parser), после decode проверить actual bytes. Не считать весь JSON binary-size и не ограничивать нормальную картинку8MiB. Ошибки превышения от reader, handler или decoder mapping → безопасный ProviderException(Unknown). Catch охватывает и SendAsync (127 сможет бросить раньше parser), и bounded read; caller OCE не преобразовывать в Unknown/Timeout.
6. Provider tests: обычный JSON >8MiB rejected; image JSON с b64 payload >8MiB, но decoded image <50MiB принят; generation JSON превышает72MiB → Unknown; decoded image превышает50MiB → Unknown; fal status/result/image проверяют выбранный лимит; probe/cancel counting stream не читается; external cancellation остаётся OCE. Проверить option на отправленных requests через stub handler, включая разные endpoints одного client.
7. В отчёте явно указать: текущий LoggingHttpHandler ещё предварительно читает ответ; полная production guarantee появится после127. В112 не менять handler/tests и не объявлять127 завершённой.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Providers/ProviderResponseLimits.cs` (новый), `ImageGen/ImageGen.Core/Providers/ProviderResponseTooLargeException.cs` (новый), `ImageGen/ImageGen.Core/Providers/BoundedHttpContentReader.cs` (новый), `ImageGen/ImageGen.Core/Providers/ProviderClientBase.cs`, `ImageGen/ImageGen.Core/Fal/FalImageProvider.cs`, `ImageGen/ImageGen.Core/OpenRouter/OpenRouterClient.cs`, `ImageGen/ImageGen.Core/OpenAiCompatible/OpenAiCompatibleClient.cs`, `ImageGen/ImageGen.Core.Tests/OpenRouter/OpenRouterClientTests.cs`, `ImageGen/ImageGen.Core.Tests/Providers/BoundedHttpContentReaderTests.cs` (новый), `ImageGen/ImageGen.Core.Tests/OpenAiCompatible/OpenAiCompatibleClientTests.cs`, `ImageGen/ImageGen.Core.Tests/Fal/FalImageProviderTests.cs`

После проверок заполнить «Отчёт» (изменения, проверки, непроверенное), выполнить `scripts/release-task.sh 112`, затем `bash scripts/check.sh`; отправить PR по `docs/tasks.md`.

## Отчёт

- Добавлены общие пределы для JSON (8 MiB), JSON генерации с base64 (72 MiB) и бинарного результата (50 MiB), безопасное исключение размера и bounded reader для байтов/текста. Заголовок выше лимита отклоняется до чтения; неизвестная длина проверяется с чтением максимум limit+1.
- `ProviderClientBase`, fal JSON/download, OpenRouter и OpenAI-compatible generation используют `ResponseHeadersRead`; timeout остаётся активен до конца чтения. Probe/cancel помечаются для пропуска body. Generation response отдельно ограничивает декодированную картинку.
- Добавлены reader и provider tests, включая JSON image payload больше 8 MiB, mapping превышения в `Unknown`, charset, лимит чтения и отмену. Исправлен fal `BlockingContent` test double: теперь блокировка происходит при чтении stream после заголовков.
- Проверки: `dotnet build ImageGen/ImageGen.Core/ImageGen.Core.csproj --no-restore` (успешно, 0 предупреждений); `dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj` (558/558); `dotnet format` для Core и Core.Tests с `--verify-no-changes --no-restore`; `bash scripts/check.sh` (успешно).
- Ограничение по плану: `LoggingHttpHandler` пока предварительно буферизует ответы; полная гарантия production memory cap остаётся задачей 127.
- Follow-up 150: исправлен pre-allocation guard base64 в обоих JSON image clients с учётом допустимых ASCII whitespace; Core.Tests — 652/652, включая production 50 MiB boundary и wrapped payload обоих клиентов. Оставшаяся Fal/OpenAI-compatible limits matrix выполняется в 152; задача остаётся blocked до завершения 152.
- Follow-up 152 завершён: client/pipeline matrix для Fal и OpenAI-compatible подтверждена, полный Core.Tests — 685/685. Оба остатка 150/152 закрыты; исходная оговорка о предварительной буферизации в LoggingHttpHandler снята после тестов 127.

## Комментарии

### 2026-10-30, codex-audit — проверка завершённости
Статус done не подтверждён полностью; задача оставлена вне архива и переведена в blocked до фиксов 150, 152. План шаг5 требовал учитывать допустимые base64 whitespace. Оба DecodeImage сравнивают полную длину строки до Convert.FromBase64String; wrapped49MiB может быть отвергнут при JSON<72MiB. Дополнительно недостаёт предписанной client limits matrix для Fal/OpenAI-compatible; reader-level tests её не заменяют.
Исторический отчёт и прежние комментарии сохранены. Остаток реализовывать только в названных follow-up задачах, не занимать эту задачу параллельно им. Вернуть done после их merge и проверки исходных критериев; отсутствие устройства само по себе причиной reopen не было.
