---
id: 152
status: planned
assignee:
claimed:
created: 2026-10-30
---

# Дополнить обязательные тесты bounded HTTP pipeline

## Описание
Production реализация127 выглядит соответствующей контракту, но её обязательная матрица проверок не завершена. Текущие oversized tests проверяют только объявленный Content-Length для default JSON; отсутствуют проверки stream overrun, cancel body, trace oversize и больших допустимых payload. В112 также недостаёт client integration checks Fal/OpenAI-compatible. Это задача на проверку, runtime-дефект сверх150 пока не установлен.

Зависит от: 150.
Среда: Linux или Windows.

## Критерии готовности
- Все сценарии плана127 шаги1/3/4/6 подтверждены тестами реального LoggingHttpHandler с подменённым transport и non-seekable streams.
- Exact limit разрешён, +1 отклонён; без/с заниженным Content-Length прочитано не больше limit+1, response/request dispose подтверждён.
- Information не читает request/response bodies, размер unknown не вызывает чтение. SkipResponseBody для probe и cancel не читает response ни при Debug, ни при trace.
- На oversize отсутствуют частичные response body trace/log records; допустимые body остаются доступны downstream parser.
- Fal/OpenAI-compatible mapping oversize→Unknown и caller cancellation подтверждены на своих путях.
- Существующие redaction/replay/timeout tests остаются зелёными, публичные лимиты не увеличены.

## Анализ
Evidence: Core.Tests/Http/LoggingHttpHandlerTests.cs:46–126 — Information response известной длины, Skip только /key, oversized tests лишь header JSON/request. Diagnostics/DebugTraceHandlerTests.cs содержит маленькие replay tests. BoundedHttpContentReaderTests проверяет reader, но не pipeline ownership/replacement/disposal. План127 требует проверки разных BodyKind, trace и request4 attachments; план112 требует provider mapping. Не добавлять ещё один response reader или искусственные публичные options только для теста. Большие сценарии выполнять последовательно, генерируемым stream, не многими static byte[]/строками одновременно. Матрицу ограничить независимыми контрактами ниже, не перемножать все флаги без причины.

## План
1. Прочитать LoggingHttpHandler, BoundedHttpContentReader/ProviderResponseLimits, все три текущих test files (logging, trace, reader), client oversize tests. Выписать уже покрытые сценарии в отчёт, не дублировать их.
2. В тестовых doubles добавить/переиспользовать non-seekable counting stream: заданное число байт, блоки, независимые declared length/null/заниженный length, счётчик реально выданных bytes, disposed flag. Он должен соблюдать requested buffer length. Не выдавать body заранее через StringContent для теста отсутствующего Content-Length.
3. Logging debug response: параметризовать Json/GeneratedImageJson/Image, exact/+1 с отсутствующей длиной; дополнительно один dishonest-low-length сценарий. Assert bytes<=limit+1 при отказе, no partial body record и disposal; success byte-for-byte downstream читается. Вызывать HttpClient.SendAsync с ResponseHeadersRead, иначе сам HttpClient буферизует body и портит измерение.
4. Trace enabled: один oversized response для unknown length и один oversized request до transport. Проверять отсутствие именно body/blobs records превышающего тела, не запрещать штатные metadata records. Valid generated-image JSON >8MiB сохраняется целиком replay-readable. Binary response остаётся бинарно читаемым.
5. Request cap72MiB: exact/+1 с unknown length, один dishonest-low-length; при +1 transport не вызван, нет partial request record. Valid JSON с четырьмя attachment data URLs проходит и доходит transport; размер держать в пределах request cap. Logger redaction не публикует base64, full debug trace по своей спецификации может хранить blobs.
6. Information-only: unknown response и request stream оба остаются непрочитанными именно handler-ом; для request transport fake не читает body. Operational entry содержит unknown. Parameterize credential-check и SkipResponseBody cancel с Debug/trace: счётчик response body0, response body trace отсутствует.
7. Client интеграция: OpenAI-compatible generated JSON header/unknown-length oversize и Fal status/result JSON/download binary oversize; проверить Unknown и безопасное сообщение, без содержимого. Для blocked stream cancellation проверять caller OCE отдельно от provider timeout; использовать TCS, не долгий sleep. Переиспользовать текущие clients/tests, платных запросов нет.
8. Если регрессионный тест обнаружит новый runtime баг, исправлять только соответствующий контракт маленьким TDD шагом в разрешённых files ниже, зафиксировать находку в отчёте. Не менять redaction semantics. После выполнения дополнить127/112 проверенной матрицей и закрыть127;112 закрывается только вместе с150.

Файлы: `ImageGen/ImageGen.Core.Tests/Http/LoggingHttpHandlerTests.cs`, `ImageGen/ImageGen.Core.Tests/Diagnostics/DebugTraceHandlerTests.cs`, `ImageGen/ImageGen.Core.Tests/OpenAiCompatible/OpenAiCompatibleClientTests.cs`, `ImageGen/ImageGen.Core.Tests/Fal/FalImageProviderTests.cs`, существующие test doubles; при обнаруженном баге только `ImageGen/ImageGen.Core/Http/LoggingHttpHandler.cs` или `ImageGen/ImageGen.Core/Providers/BoundedHttpContentReader.cs`; задачи127/112.

Проверка: `dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj`; format Core.Tests и изменённого Core; `bash scripts/check.sh`.

### Команды проверки

Выполнять по одной, из корня репозитория:

```bash
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
bash scripts/check.sh
```

### Порядок исполнения и проверки
План рассчитан на Luna: прочитать перечисленные файлы, выполнить шаги по порядку; сначала падающий регрессионный тест, затем минимальный фикс (ub-mode из AGENTS.md). Не реализовывать соседние задачи. Сигнатуры сверять с текущим main; при rebase сохранять чужие изменения. Не использовать платные API и не записывать секреты. Команды выполнять последовательно: общие build outputs не допускают параллельные dotnet build/test.
После изменения каждого проекта: `dotnet format ImageGen/<Проект>/<Проект>.csproj --verify-no-changes`; после всех изменений — перечисленные тесты и `bash scripts/check.sh`. В отчёте указать команды, результаты, ручные ограничения и отличие от плана. Код писать только после одобрения плана по docs/tasks.md.

## Отчёт

## Комментарии

### 2026-10-30, codex-audit
Подробный план по поручению владельца для Luna. Реализация не начата; planned не означает approved.
