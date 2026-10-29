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
- Logging handler в fal transport pipeline один раз.

## Анализ
Выбор limit не по untrusted Content-Type, а по request option 112: Image only marked download, default JSON. CapturingLogger configurable minimum level/structured state появляется в 110.

## План
Для Haiku/Luna: не менять BodyRedactor/TraceRedaction semantics; не сокращать valid debug trace body внутри limit.

1. Information path убрать ReadAsByteArray; capture nullable header and log size/unknown.
2. Debug/trace path read via BoundedHttpContentReader with request option. Small content replace ByteArrayContent/StringContent-equivalent and copy all content headers before existing redactors.
3. Credential check skips response body even Debug/trace; no size-driven read.
4. Replace current `LogWarning(ex,...)` network/timeout with safe Warning metadata only and separate LogDebug(ex,...). No content/exception message above Debug.
5. Tests with logger min levels from110: Information-only counting stream untouched; Debug redacted/truncated body parser-readable; trace full body replay; exactly limit/+1 JSON/image; no partial records; headers preserved.
6. Credential/account redaction regressions and network failure log-level test.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Http/LoggingHttpHandler.cs`, `ImageGen/ImageGen.Core.Tests/Http/LoggingHttpHandlerTests.cs`, `ImageGen/ImageGen.Core.Tests/Diagnostics/DebugTraceHandlerTests.cs`

## Отчёт

## Комментарии
