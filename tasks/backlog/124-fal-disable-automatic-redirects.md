---
id: 124
status: done
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
- Для submit/catalog/status/result/cancel/image download любой 3xx даёт `ProviderException(ErrorKind.Unknown)` без Location/body. Key probe сохраняет публичный контракт `ApiKeyStatus.NetworkError` на 3xx, а не бросает ProviderException. Location не попадает в operational log; отдельный Debug/debug-trace сохраняет действующий режим диагностики.
- Ни callback, ни image redirect не выполняют второй HTTP request.
- `LoggingHttpHandler` остаётся в pipeline ровно один раз; Debug/debug-trace behavior не удалено.

## Анализ
`ProviderClientBase` вызывает `CreateClient(Config.Id)`, прямые fal paths делают то же. `ConfigureHttpClientDefaults` добавляет logging всем clients. Нужны protected transport-name/client helpers и named registration с primary handler; нельзя регистрировать policy только для `ProviderIds.Fal`.

Нужность: URL validation 109 не контролирует redirect, поэтому без transport policy её защита обходится.

## План
Для Haiku/Luna: не писать manual redirect follower. Redirect для fal всегда terminal error. Не менять redirect policy других providers.

Прочитай: `ProviderClientBase`, `FalImageProvider`, `ProviderRouter`, `CoreServiceCollectionExtensions`, `Data.Tests/CompositionTests`, HTTP/fal tests.

1. В `ProviderClientBase` добавить protected virtual `TransportClientName` (default `Config.Id`) и protected `CreateHttpClient()`; все base sends, включая ProbeKeyAsync, используют helper. Ввести protected virtual проверку ответа `ValidateTransportResponse(HttpResponseMessage)` (default no-op), вызываемую до чтения/parsing body. Fal override отклоняет status 300–399 безопасным Unknown, чтобы base submit не вытащил сообщение/Location из redirect body. Для probe не вызывать бросающий hook: 3xx уже маппится в NetworkError.
2. В `FalImageProvider` задать `internal const string TransportName = "ImageGen.Internal.FalTransport"`, override TransportClientName и перевести direct sends на CreateHttpClient. После 107 удалить ставшее ненужным поле `_httpClientFactory`; не менять Config.Id и SecretNames.ForProvider(Config.Id).
3. Зарегистрировать named client этого имени в `CoreServiceCollectionExtensions` с `HttpClientHandler.AllowAutoRedirect=false`. Не добавлять второй `LoggingHttpHandler`: default configuration уже применяется.
4. Вызывать единый fal response hook в direct JSON/download/cancel до чтения body и до EnsureSuccessStatusCode (иначе download превратит redirect в Network). Проверить submit через base hook. Для key probe добавить regression 302 → NetworkError.
5. Provider tests с sequence handler для 301/302/307/308: callback и image дают Unknown с безопасным сообщением, один request; redirect response с error-message/Location sentinel не попадает в исключение. Отдельно custom fal id использует ожидаемое имя transport и секрет своего id. Stub доказывает mapping и выбор client, но сам по себе не доказывает отключение automatic redirects.
6. В `ImageGen.Data.Tests/CompositionTests.cs` использовать реальный DI AddImageGenCore+Data и `IHttpMessageHandlerFactory.CreateHandler(TransportName)`. Пройти DelegatingHandler.InnerHandler до primary HttpClientHandler и проверить AllowAutoRedirect=false; в цепочке ровно один LoggingHttpHandler. В Data.Tests использовать строковый контракт `ImageGen.Internal.FalTransport` (internal-член не доступен из другого проекта); Core.Tests отдельно проверяет совпадение имени запросов с этим контрактом. Не поднимать HTTP server и не открывать сокеты. Этот тест доказывает фактическую DI policy; Core tests отдельно доказывают использование именно этого имени всеми fal requests, включая custom id.
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

После проверок заполнить «Отчёт» (изменения, проверки, непроверенное), выполнить `scripts/release-task.sh 124`, затем `bash scripts/check.sh`; код отправить PR по `docs/tasks.md`.

## Отчёт

Зарегистрирован стабильный fal transport `ImageGen.Internal.FalTransport` с `HttpClientHandler.AllowAutoRedirect=false`; все запросы `ProviderKind.Fal`, включая custom provider ids, выбирают этот клиент, сохраняя Config.Id для ключей и конфигурации. Общий `ProviderClientBase` получил выбор имени клиента и hook проверки ответа до чтения тела. Fal отклоняет любой 3xx безопасным `ProviderException(ErrorKind.Unknown)`. Probe не вызывает hook и переводит 3xx в `ApiKeyStatus.NetworkError`. Submit/catalog/status/result/cancel/image download покрыты путями с запретом редиректа.

Добавлены тесты последовательных 301/302/307/308 для status/result/image, проверки submit/catalog/cancel и key probe, проверки transport/secret id для custom Fal id и DI composition тест фактического `AllowAutoRedirect=false` с ровно одним `LoggingHttpHandler`.

Проверки: Core 524/524; Data 79/79; `dotnet format --verify-no-changes` для Core, Core.Tests и Data.Tests; `bash scripts/check.sh`.

Живые запросы к fal не выполнялись; redirect-поведение проверено тестовым handler и DI-конфигурацией. Ручной проверки UI задача не требует.

## Комментарии
