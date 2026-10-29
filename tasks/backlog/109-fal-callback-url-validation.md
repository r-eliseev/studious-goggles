---
id: 109
status: approved
assignee:
claimed:
created: 2026-10-29
---

# Проверять URL и model endpoint, возвращённые fal

## Описание
Security-review 2026-10-29: fal callback URLs идут в authorized request без origin validation; absolute model id также может заменить base URI. Image URL может указывать на HTTP/localhost/private IP. Automatic redirects отдельно отключает задача 124.

Зависит от: 107.
Среда: Linux или Windows.

## Критерии готовности
- status/response/cancel: absolute URL с теми же scheme, normalized host и effective port, что уже провалидированный `Config.BaseAddress`, без userinfo/fragment; built-in fal остаётся HTTPS, локальный HTTP допустим только когда сам base прошёл localhost-исключение ProviderRegistry; проверка при submit и перед использованием persisted handle.
- `ProviderJobHandle.ProviderId == Config.Id` до secret-store/HTTP access.
- fal `request.ModelId` только relative endpoint id: absolute URI, `//host`, query, fragment и traversal отклоняются до secret lookup/request.
- Image URL: absolute HTTPS без userinfo/fragment; запрещены `localhost`, `localhost.`, loopback, unspecified, link-local, RFC1918, ULA и IPv4-mapped non-public IP literals.
- Invalid URL даёт `ProviderException(ErrorKind.Unknown)` с безопасным сообщением без исходного URL/query.
- Existing callback query сохраняется; `logs=1` добавляется/заменяется URI-aware без потери остальных query parameters и без string concatenation.
- DNS hostname→private IP остаётся документированным residual risk; redirect bypass закрывает 124.

## Анализ
`new Uri(base, absolute)` заменяет base. `ProviderRegistry` валидирует только config address. Fal catalog `endpoint_id` попадает в Submit. Stub handler не моделирует redirect, поэтому redirect policy не входит в эту задачу.

Пересечения: 124 и 112 по HTTP; порядок 109 → 124 → 112. Задача 121 remote cancel зависит также от этой validation.

## План
Для Haiku/Luna: один internal validator, без DNS lookup и универсальной SSRF-библиотеки. Все invalid provider data маппить в `Unknown`, не оставлять выбор ErrorKind исполнителю.

Прочитай: `FalImageProvider`, `ProviderClientBase.CreateRequest`, `ProviderConfig/Registry`, fal tests.

1. Создать `FalUrlValidator`: callback validator; image URL validator; model endpoint validator. Возвращать `Uri`/validated relative string.
2. Tests callback: default/effective port, host case/normalization, external host, HTTP, userinfo, fragment, malformed URI. Same-origin happy path проходит.
3. При `SubmitAsync` валидировать model endpoint до `CreateAuthorizedRequestAsync`; tests `https://evil`, `//evil`, query, fragment, `..` и normal `fal-ai/flux/dev`.
4. Валидировать callback URLs до сохранения handle. В Poll/Completed/Cancel сначала проверить handle provider id и URLs, затем читать key.
5. Добавлять/заменять `logs=1` через URI API без string concatenation; сохранить остальные existing query parameters. Tests no query, one query и existing logs value.
6. Image tests: normal HTTPS hostname проходит; запрещённые hostname/IP literals, включая IPv4-mapped IPv6, не вызывают handler.
7. Safe error/log tests: exception не содержит rejected URL, query, userinfo, prompt или key.
8. В отчёте явно записать residual DNS risk и ссылку на 124 для redirect.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Fal/FalUrlValidator.cs` (новый), `ImageGen/ImageGen.Core/Fal/FalImageProvider.cs`, `ImageGen/ImageGen.Core.Tests/Fal/FalUrlValidatorTests.cs` (новый), `ImageGen/ImageGen.Core.Tests/Fal/FalImageProviderTests.cs`

## Отчёт

## Комментарии
