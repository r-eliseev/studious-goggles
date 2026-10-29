---
id: 127
status: approved
assignee:
claimed:
created: 2026-10-29
---

# Ограничить HTTP logging buffer, сохранив Debug content

## Описание
После bounded provider reader 112 `LoggingHttpHandler` всё ещё безусловно буферизует body на Information и при Debug/trace читает первым. Нужно применить request body-kind limits и сохранить решение владельца: redacted content и full exception details остаются доступны на Debug; Information/Warning их не содержат.

Зависит от: 110, 112.
Среда: Linux или Windows.

## Критерии готовности
- Information использует declared Content-Length или unknown и не читает body.
- Обычный Debug сохраняет текущий redacted body с `BodyRedactor.MaxLength`; debug trace сохраняет full replay body в provider limit.
- Oversized body не пишется частично ни в один channel; size exception доходит до provider mapping 112.
- Small buffered copy remains parser-readable, content headers preserved.
- Credential-check body не читается/logged/traced.
- Information/Warning: no request/response content, exception object/message; operational metadata only. Full exception detail separate Debug.
- Pipeline не переконфигурируется в этой задаче: единственность fal logging handler уже проверяет124. Сохраняется проходящий тест124.

## Анализ
Выбор response limit не по untrusted Content-Type, а по request option112 `ProviderResponseLimits.BodyKind` (`HttpRequestOptionsKey<ProviderResponseBodyKind>`): Json (8MiB, default), GeneratedImageJson (72MiB для b64_json/data URL), Image (50MiB). Использовать контракт112 без новых параллельных констант. CapturingLogger configurable minimum level/structured State появляется в110.

Необходимость: без127 handler читает response раньше bounded parser112, так что112 отдельно не обеспечивает ограничение памяти. Request и response — разные направления: request с четырьмя вложениями по10MiB становится примерно53.4MiB после base64; применять к нему default JSON response limit8MiB нельзя. Для request Debug/trace установить отдельный ограниченный буфер72MiB. При превышении не читать дальше и не писать partial body; вернуть безопасную size error. Information не буферизует request или response.

Нормативное правило safe operational log отсутствует в conventions/ADR0010. В127 создать новый ADR, дополняющий0010 (следующий свободный номер на момент реализации; старый ADR не переписывать) и уточнить conventions.106/110 уже применяют safe metadata локально;127 консолидирует правило, не создавая обратных зависимостей.

## План
Для Haiku/Luna: не менять BodyRedactor/TraceRedaction semantics; не сокращать valid debug trace body внутри limit.

1. `Information_DoesNotReadResponseBody`: использовать Information-only CapturingLogger и non-seekable counting stream; вызывать HttpClient.SendAsync с ResponseHeadersRead, иначе сам HttpClient буферизует content и исказит проверку handler. После return stream read count=0, лог содержит объявленный Content-Length либо явное unknown. Для request Information также не вызывает LoadIntoBuffer/ReadAsString.
2. Debug/trace response: вызвать общий `BoundedHttpContentReader` из112 с response limit по BodyKind; после полного успешного чтения заменить content на ByteArrayContent с теми же content headers и dispose старый content. Только затем передавать полную копию в прежние redactors. Downstream parser читает те же байты, включая бинарную картинку. При size error — dispose response, никаких частичных response body records; exception доходит до mapping112 (catch охватывает SendAsync).
3. Debug/trace request: заменить безлимитный LoadIntoBufferAsync ограниченным чтением с отдельным request cap72MiB, сохранив headers и parser/transport-readable content. Малый text request по-прежнему логируется с BodyRedactor; full replay trace сохраняется целиком в лимите. Превышение request cap выбрасывает ProviderResponseTooLargeException с безопасным описанием направления request (не request body/URI). Добавить константу RequestBody=72MiB в общий ProviderResponseLimits, не в handler. Size test доказывает ≤limit+1 bytes с отсутствующим/заниженным Content-Length.
4. Любой request с `ProviderResponseLimits.SkipResponseBody` (`HttpRequestOptionsKey<bool>`) = true из112 (ProbeKeyAsync/Fal.CancelAsync), а также credential check (`IsCredentialCheck`) никогда не читает response body ради логирования, включая Debug/trace. Сохранить operational metadata, пропустить response body trace. Для cancel добавить counting-stream тест наряду key probe. Request credentials по-прежнему защищены header allowlist/redactors; не добавлять логирование заголовков. Source/response probe из112 остаются без чтения неиспользуемого body.
5. В network/timeout catches Warning содержит только Method/Path/ElapsedMs и безопасный kind, без exception object/message; detail отдельным LogDebug(ex,...). Caller cancellation проходит без operational timeout warning. Обновить прежний NetworkError test: Assert.Single только для Warning, дополнительно проверить Debug entry и exception — logger по умолчанию теперь сохраняет обе записи.
6. Именованные проверки `Debug_BodyRemainsParserReadableAndRedacted`, `Trace_PreservesFullReplayWithinLimit`, `ResponseSize_ExactLimitAllowed_OneByteOverRejected`, `Oversize_NoPartialBodyRecords`, `CredentialCheck_AllLevelsLeaveBodyUnread`, `NetworkFailure_OperationalMetadataOnly`. Параметризовать Json/GeneratedImageJson/Image и известный/неизвестный Content-Length; проверять State и Exception=null выше Debug. Валидный request с четырьмя attachments и валидный GeneratedImageJson больше8MiB не отбрасываются. Для large boundary tests использовать generated stream, не обращаться к реальным API. Существующие redaction/trace tests сохранить.
7. В `docs/conventions.md` записать: operational Information/Warning/Error — без content/provider text/exception detail, только разрешённые структурированные метаданные; отдельный Debug для деталей, существующее правило credential redaction действует на всех уровнях. Создать новый ADR с областью transport + затронутые assistant/generation ветки, причиной и связью с0010; не обещать аудит всех существующих LogWarning вне scope127. В `docs/knowledge/csharp.md` заменить рекомендацию вычислять size через безлимитный `ReadAsByteArrayAsync().Length` на Content-Length/unknown в Information и bounded reader в Debug/trace; пример exception logging сослать на отдельные operational metadata/Debug detail по новой policy. Не оставлять безлимитный ReadAsByteArray в guidance как способ определения размера.
8. После проверок заполнить «Отчёт» (изменения, команды, ограничения), выполнить `scripts/release-task.sh 127` и `bash scripts/check.sh`; обновить PR по `docs/tasks.md`. Код менять маленькими шагами TDD.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Http/LoggingHttpHandler.cs`, `ImageGen/ImageGen.Core.Tests/Http/LoggingHttpHandlerTests.cs`, `ImageGen/ImageGen.Core.Tests/Diagnostics/DebugTraceHandlerTests.cs`, `ImageGen/ImageGen.Core/Providers/ProviderResponseLimits.cs` (из112), `docs/conventions.md`, `docs/knowledge/csharp.md`, `docs/decisions/NNNN-safe-operational-logging.md` (новый; следующий свободный номер)

## Отчёт

## Комментарии
