---
id: 124
status: approved
assignee:
claimed:
created: 2026-10-29
---

# Отключить automatic redirects для всех fal transport requests

## Описание
Security-review 2026-10-29: initial URL validation 109 можно обойти HTTP redirect. Callback и image redirects обходят проверку конечного origin и создают SSRF-риск. `HttpClientHandler` очищает Authorization при auto-redirect; перенос credentials этой задачей не заявляется. Текущий client name равен dynamic provider id, поэтому policy только имени `fal` не покрывает custom configs.

Источник поведения handler: [.NET `HttpClientHandler.AllowAutoRedirect`](https://learn.microsoft.com/dotnet/api/system.net.http.httpclienthandler.allowautoredirect?view=net-10.0). Сведения пересказаны.

Зависит от: 109.
Среда: Linux или Windows.

## Критерии готовности
- Все экземпляры `ProviderKind.Fal`, включая custom ids, используют один stable internal transport client с `AllowAutoRedirect=false`.
- Provider id продолжает определять config/secret identity; transport client name на это не влияет.
- Submit/probe/catalog/status/result/cancel/image download используют этот transport.
- Любой 3xx даёт `ProviderException(ErrorKind.Unknown)`; `Location` не попадает в сообщение/log.
- Ни callback, ни image redirect не выполняют второй HTTP request.
- `LoggingHttpHandler` остаётся в pipeline ровно один раз; Debug/debug-trace behavior не удалено.

## Анализ
`ProviderClientBase` вызывает `CreateClient(Config.Id)`, прямые fal paths делают то же. `ConfigureHttpClientDefaults` добавляет logging всем clients. Нужны protected transport-name/client helpers и named registration с primary handler; нельзя регистрировать policy только для `ProviderIds.Fal`.

## План
Для Haiku/Luna: не писать manual redirect follower. Redirect для fal всегда terminal error. Не менять redirect policy других providers.

Прочитай: `ProviderClientBase`, `FalImageProvider`, `ProviderRouter`, `CoreServiceCollectionExtensions`, `Data.Tests/CompositionTests`, HTTP/fal tests.

1. В `ProviderClientBase` добавить protected virtual transport client name (default `Config.Id`) и protected `CreateHttpClient`; все base sends используют helper.
2. В `FalImageProvider` задать internal stable transport name и перевести direct sends на base helper.
3. Зарегистрировать named client этого имени в `CoreServiceCollectionExtensions` с `HttpClientHandler.AllowAutoRedirect=false`. Не добавлять второй `LoggingHttpHandler`: default configuration уже применяется.
4. Явно map 3xx во всех fal paths, включая download/cancel, в `ProviderException(Unknown)` без Location.
5. Provider tests с sequence handler: 302 response приводит к одному request для callback и image; второй target не вызывается.
6. Composition test in `ImageGen.Data.Tests/CompositionTests.cs`: create temp data directory with custom fal id and localhost HTTP base, resolve provider through real DI, local server returns callback 302 to a second local path; assert response mapped Unknown and second path was never requested. Этот test доказывает production handler policy для dynamic id без внешней сети.
7. Regression: request auth использует secret имени custom provider id, а не transport name; logging handler event не дублируется.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Providers/ProviderClientBase.cs`, `ImageGen/ImageGen.Core/Fal/FalImageProvider.cs`, `ImageGen/ImageGen.Core/CoreServiceCollectionExtensions.cs`, `ImageGen/ImageGen.Core.Tests/Fal/FalImageProviderTests.cs`, `ImageGen/ImageGen.Data.Tests/CompositionTests.cs`

## Отчёт

## Комментарии
