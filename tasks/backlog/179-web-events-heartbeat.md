---
id: 179
status: in-progress
assignee: codex-gpt6
claimed: 2026-11-02T17:48:14Z
created: 2026-11-02
---

# Web: поток событий перестаёт работать после первого heartbeat

## Описание
Находка код-ревью 2026-11-02 (PR #67, задача 031). `ImageGen/ImageGen.Web/Events/EventsEndpoint.cs`, `StreamAsync`: один `heartbeatCancellation` переиспользуется во всех итерациях. После первого срабатывания (20 с без событий) он остаётся отменённым навсегда: `CancelAfter` не снимает отмену. Дальше каждый `WaitToReadAsync` сразу получает отменённый токен, цикл без паузы пишет `: heartbeat`, а события из канала больше не читаются.

Зависит от: —
Среда: Linux или Windows.

## Критерии готовности
- После простоя дольше интервала heartbeat следующее событие (`session`) доходит до клиента.
- За время простоя пишется не больше одного heartbeat на интервал, без цикла без паузы.
- Интервал heartbeat в рабочем режиме прежний — 20 с.
- Регрессионный тест в `ImageGen.Web.Tests` с коротким интервалом.

## Анализ
Канал `BoundedChannel` при уже отменённом токене возвращает отменённый `WaitToReadAsync`, не глядя на данные, поэтому события не читаются. Существующие тесты `EventApiTests` не ждут 20 с.

Чтобы тест не ждал 20 с, интервал нужно задавать извне. Самый простой путь — маленький класс настроек в DI.

## План
1. Новый класс `EventStreamOptions` (в `ImageGen.Web/Events/`) с одним свойством `HeartbeatInterval`, по умолчанию 20 с. Зарегистрировать в `Program.cs` как singleton (`AddSingleton(new EventStreamOptions())`).
2. Падающий тест в `EventApiTests`: `Events_AfterIdleHeartbeat_StillDeliversSessionEvent`.
   - Хост через `WebApplicationFactory` с `ConfigureTestServices`, где `EventStreamOptions` заменён на интервал 100 мс (по образцу `TestHost`; если в `TestHost` нет способа подменить сервисы — добавить в него необязательный параметр-делегат для `ConfigureTestServices`).
   - Войти, открыть `/api/events` (`OpenEventsAsync`), подождать ~350 мс (больше трёх интервалов).
   - Вызвать `NotifySessionChange(id)`.
   - Читать поток построчно с таймаутом 5 с до строки `event: session`; ожидание — строка пришла, а число строк `: heartbeat` до неё не больше 10.
   - На текущем коде тест должен упасть по таймауту (или из-за сотен heartbeat).
3. В `StreamAsync` получать `EventStreamOptions` параметром (DI в minimal API). В каждой итерации цикла создавать **новый** `CancellationTokenSource` таймаута (`using` внутри итерации), связанный с `RequestAborted`. Отличать «таймаут heartbeat» от «запрос прерван» как сейчас, по флагам двух источников. Общий `heartbeatCancellation` и `finally` с `CancelAfter(Infinite)` удалить.
4. Остальное поведение не менять: формат `event:/data:`, flush после каждого события.

Файлы: `ImageGen/ImageGen.Web/Events/EventsEndpoint.cs`, `ImageGen/ImageGen.Web/Events/EventStreamOptions.cs` (новый), `ImageGen/ImageGen.Web/Program.cs`, `ImageGen/ImageGen.Web.Tests/EventApiTests.cs`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Web.Tests/ImageGen.Web.Tests.csproj
dotnet format ImageGen/ImageGen.Web/ImageGen.Web.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Web.Tests/ImageGen.Web.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

### Порядок исполнения (для Luna)
- Сначала прочитай только файлы из строки «Файлы» и названные тесты; сигнатуры сверяй с текущим `main`.
- Шаги строго по порядку (ub-mode): падающий тест → запуск, убедиться, что он падает именно по описанной причине → минимальный фикс → тест зелёный.
- Не меняй файлы вне строки «Файлы», не переименовывай существующие члены сверх плана, не чини соседние находки — они в отдельных задачах.
- Если код расходится с планом (нет такого метода, другая сигнатура, тест уже есть), не импровизируй: остановись и запиши вопрос в «Комментарии».
- Команды `dotnet` выполняй по одной, не параллельно. После каждого изменённого проекта — `dotnet format <проект> --verify-no-changes`.
- В «Отчёте»: что изменено, команды и результаты, отличия от плана.

## Отчёт

## Комментарии

### 2026-11-02, claude-audit
Создано по код-ревью PR #67/#70 по поручению владельца; план для Luna. Ждёт одобрения.

### 2026-11-02, владелец (в чате, записал claude-audit)
План одобрен: «таски заапрувлены».
