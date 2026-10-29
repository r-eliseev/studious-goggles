---
id: 109
status: planned
assignee:
claimed:
created: 2026-10-29
---

# Проверять callback URL и URL изображения fal

## Описание
Найдено security-review 2026-10-29. fal возвращает `status_url`, `response_url`, `cancel_url` и URL результата. Callback URL без проверки передаются в `CreateAuthorizedRequestAsync`. Абсолютный URL заменяет `Config.BaseAddress`, после чего запрос получает `Authorization: Key <fal-key>`. Это позволяет отправить ключ на посторонний origin. URL изображения скачивается без Authorization, но может указывать на HTTP, localhost или приватный IP.

Зависит от: 107.
Среда: Linux или Windows.

## Критерии готовности
- status/response/cancel URL принимаются только как абсолютные HTTPS URL с теми же host и effective port, что `Config.BaseAddress`; userinfo и fragment запрещены.
- Проверка выполняется при чтении submit-ответа и повторно перед использованием сохранённого handle.
- К URI другого origin API-ключ никогда не добавляется.
- URL изображения должен быть абсолютным HTTPS без userinfo; localhost, loopback, link-local и private IP literal отклоняются до HTTP-запроса.
- Redirect с авторизованного callback не переносит Authorization на другой origin; это подтверждено тестом либо автоматические redirect для этого клиента отключены и обработаны явно.
- Негативные тесты проверяют внешний host, HTTP, userinfo, loopback IPv4/IPv6 и подменённый сохранённый handle.

## Анализ
- `ProviderRegistry` проверяет только настроенный `baseAddress`; URL из ответа не проверяются.
- `new Uri(Config.BaseAddress, path)` принимает абсолютный `path`.
- Текущий встроенный fal origin — `https://queue.fal.run/`; happy-path тесты уже используют callback на этом origin.
- DNS rebinding без собственного transport/DNS policy полностью не закрывается. В этой задаче блокируются опасные URI и IP literals; остаточный риск hostname→private IP описать в «Отчёте», не добавлять сетевой DNS lookup в валидатор.
- Пересечение с 107 и 112 по `FalImageProvider`; выполнять после 107 и до 112.

## План
Для исполнителя (Haiku/Luna): не создавай универсальную SSRF-библиотеку. Добавь один маленький internal validator и точечные тесты. Не ослабляй поддержку `localhost` у настраиваемых OpenAI-compatible providers: правила этой задачи относятся к URL, возвращённым fal.

Прочитай: `FalImageProvider.cs`, `ProviderClientBase.cs`, `ProviderRegistry.cs`, `FalImageProviderTests.cs`, `builtin-providers.json`.

1. Создать internal `FalUrlValidator` рядом с `FalImageProvider`. Методы разделить по назначению: callback с exact-origin policy; image download с HTTPS/public-literal policy. Возвращать проверенный `Uri` или бросать `ProviderException(ErrorKind.BadRequest или Unknown)` с сообщением без ключа. Выбрать один существующий kind и закрепить тестом; новый `ErrorKind` не добавлять.
2. TDD для callback: same-origin проходит; host/port/scheme/userinfo/fragment отличаются → исключение до вызова handler.
3. Вызывать callback validation при `SubmitAsync` перед созданием `ProviderJobHandle`, а также в `PollAsync`, `CompletedAsync` и `CancelAsync` для handle из БД.
4. TDD для image URL: обычный HTTPS CDN проходит; HTTP, localhost, `127.0.0.1`, `::1`, RFC1918, link-local IP literals отклоняются до handler.
5. Проверить redirect policy `IHttpClientFactory`. Добавить handler-тест, что Authorization не приходит на другой origin после redirect. Если стандартный handler нельзя проверить через текущий stub, отключить auto-redirect только для fal через отдельную регистрацию и явно отклонять 3xx; не менять все provider clients.
6. Debug-логирование контента и ошибок оставить включаемым как сейчас; не логировать полный URL с query/userinfo и ключ.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Fal/FalUrlValidator.cs` (новый), `ImageGen/ImageGen.Core/Fal/FalImageProvider.cs`, `ImageGen/ImageGen.Core/CoreServiceCollectionExtensions.cs` (только если нужен redirect policy), `ImageGen/ImageGen.Core.Tests/Fal/FalUrlValidatorTests.cs` (новый), `ImageGen/ImageGen.Core.Tests/Fal/FalImageProviderTests.cs`

## Отчёт

## Комментарии
