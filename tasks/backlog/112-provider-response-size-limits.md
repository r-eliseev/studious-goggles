---
id: 112
status: planned
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
- Proposed owner-approved limits: JSON 8 MiB, generated image 50 MiB, one constants type.
- Base/Fal sends используют ResponseHeadersRead; timeout token покрывает send+read.
- Header > limit rejected before body; absent/false header reads ≤ limit+1.
- Image download request marked internal option; unmarked defaults JSON for 127.
- Oversize from reader or future handler maps safe `ProviderException(Unknown)`.
- Probe/cancel don't read unused body.

## Анализ
`SendFalJsonAsync` separate path. Bounded reader alone не ограничивает current LoggingHttpHandler; это явно принимает 127. Debug logging не меняется здесь.

## План
Для Haiku/Luna: только transport/parser primitives and provider tests; LoggingHttpHandler/tests не трогать.

1. Add ProviderResponseLimits JSON/Image, request `HttpRequestOptionsKey` body kind, `ProviderResponseTooLargeException` safe fields.
2. BoundedHttpContentReader bytes/text: Content-Length precheck, non-seekable read max limit+1, no partial return.
3. Unit tests exact/+1/absent/understated header/chunked/read count.
4. ProviderClientBase SendJson/Probe and Fal JSON/download/cancel use ResponseHeadersRead. One timeout CTS until content complete; Fal image marks option; probe/cancel dispose no body read.
5. Catch size exception around both SendAsync and reader; map Unknown safe message without content/URL.
6. Tests ordinary OpenAI-compatible oversize JSON; fal status/result/image; credential probe/cancel counting stream unread; external cancellation remains OCE.
7. Document in task report that full guarantee waits 127; do not mark 127 done from this task.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Providers/ProviderResponseLimits.cs` (новый), `ImageGen/ImageGen.Core/Providers/ProviderResponseTooLargeException.cs` (новый), `ImageGen/ImageGen.Core/Providers/BoundedHttpContentReader.cs` (новый), `ImageGen/ImageGen.Core/Providers/ProviderClientBase.cs`, `ImageGen/ImageGen.Core/Fal/FalImageProvider.cs`, `ImageGen/ImageGen.Core.Tests/Providers/BoundedHttpContentReaderTests.cs` (новый), `ImageGen/ImageGen.Core.Tests/OpenAiCompatible/OpenAiCompatibleClientTests.cs`, `ImageGen/ImageGen.Core.Tests/Fal/FalImageProviderTests.cs`

## Отчёт

## Комментарии
