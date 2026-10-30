---
id: 109
status: done
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

Нужность: закрывает отправку ключа на чужой callback origin и запросы к локальным адресам из provider data; это отдельная проверка от transport redirects.

Пересечения: 124 и 112 по HTTP; порядок 109 → 124 → 112. Задача 121 remote cancel зависит также от этой validation.

## План
Для Haiku/Luna: один internal validator, без DNS lookup и универсальной SSRF-библиотеки. Все invalid provider data маппить в `Unknown`, не оставлять выбор ErrorKind исполнителю.

Прочитай: `FalImageProvider`, `ProviderClientBase.CreateRequest`, `ProviderConfig/Registry`, fal tests.

1. Создать internal `FalUrlValidator` с тремя явно названными pure методами: `ValidateCallback(string value, Uri baseAddress)` и `ValidateImage(string value)` возвращают Uri; `ValidateModelEndpoint(string value)` возвращает проверенную относительную строку. Некорректное значение → `ProviderException(Unknown, безопасное постоянное сообщение)`; не прикладывать исходную UriFormatException с URL. Ошибочный handle ProviderId проверяет FalImageProvider до secret store.
2. Tests callback: default/effective port, host case/normalization, external host, HTTP, userinfo, fragment, malformed URI. Same-origin happy path проходит.
3. При `SubmitAsync` валидировать model endpoint до `CreateAuthorizedRequestAsync`; tests `https://evil`, `//evil`, query, fragment, `..`, `.` segments, начальный `/`, обратный слеш, percent-encoded traversal/separators и normal `fal-ai/flux/dev`. Для endpoint проверять исходную строку до `new Uri` (нормализация иначе скроет traversal); после однократного unescape каждого segment запретить `.`, `..`, `/`, `\\` и остаточный `%` (не поддерживать многократно encoded endpoint). Разрешать обычные непустые slash-separated fal segments, не менять валидный model id.
4. Валидировать все три callback URLs до сохранения handle; `Uri.Scheme`, `IdnHost` с OrdinalIgnoreCase и `Port` сравниваются с base, userinfo/fragment запрещены. В отличие от image URL, callback разрешает HTTP только если таков уже проверенный локальный base. Catalog и key probe ходят на собственный фиксированный ModelsAddress; same-origin callback validation к нему не применять. В Poll/Completed/Cancel сначала проверить handle provider id и URLs, затем читать key.
5. Добавлять/заменять `logs=1` через URI API без string concatenation; сохранить остальные existing query parameters. Tests no query, one query и existing logs value.
6. Image tests: normal HTTPS hostname проходит; localhost с/без конечной точки, IPv4 0/8, 10/8, 127/8, 169.254/16, 172.16/12, 192.168/16; IPv6 unspecified/loopback, link-local, fc00::/7 и mapped IPv4 из этих сетей не вызывают handler. IPv4-mapped адрес сначала привести к IPv4. Дополнить multicast IPv4 224/4 и IPv6 ff00::/8; запрещать IPv6 scope id. URI-normalized варианты числового IPv4 проверять через IPAddress.TryParse, а не regexp. Hostname в DNS не резолвить; это явно оставшийся риск, а не утверждение полной защиты от SSRF.
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

После проверок заполнить «Отчёт» (изменения, проверки, непроверенное), выполнить `scripts/release-task.sh 109`, затем `bash scripts/check.sh`; код отправить PR по `docs/tasks.md`.

## Отчёт

- Добавлен внутренний `FalUrlValidator`: fal model endpoint остаётся относительным и не содержит traversal или кодированных разделителей; callback URLs ограничены origin проверенного `BaseAddress`; image URLs требуют HTTPS и отклоняют локальные/специальные IP literals, включая IPv4-mapped адреса.
- `ProviderId` и все callback URLs в сохранённом handle проверяются до чтения секрета и HTTP. URL-ошибки имеют `ErrorKind.Unknown` и постоянное безопасное сообщение. `logs=1` добавляется через `UriBuilder`, сохраняя другие query-параметры и заменяя прежнее значение `logs`.
- DNS-имена не разрешаются во время проверки: hostname, который разрешается в private IP, остаётся документированным residual risk. Автоматические redirects остаются отдельной задачей 124.
- Проверено: `dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj` (479 passed); `dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes --no-restore`; `dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes --no-restore`.
- Отличие от плана: нет.

## Комментарии
