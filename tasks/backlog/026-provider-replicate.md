---
id: 026
status: approved
assignee:
claimed:
created: 2026-10-27
---

# Провайдер Replicate

## Описание
Реализация Replicate за контрактом ADR 0003: предсказания, опрос, отмена, скачивание результата сразу (файлы живут час).

Детализировать (точные файлы, код, тесты, как в задачах v0.1) после задачи 016, когда будет код скелета.

Зависит от: 025 (контракт `IImageProvider` и общий опрос).
Среда: Linux или Windows (только ядро).

## Критерии готовности
- Тесты на подменённом HTTP.

## Анализ

### Зависимости
- Контракт `IImageProvider`, `ProviderJobHandle`, `JobUpdate` и потоки отправки/опроса/восстановления из 025. ADR 0003 пока «предложено», без его принятия задача не начинается.
- Общий слой из 036: `ProviderKind.Replicate`, `ProviderConfig` с адресом `https://api.replicate.com/v1/`, `SecretNames.For("replicate")`.

### Что говорит документация Replicate (проверено 2026-10-28)
- Создание предсказания (`https://replicate.com/docs/topics/predictions/create-a-prediction`, дата не указана): официальная модель — `POST /v1/models/{owner}/{name}/predictions`; версия модели — `POST /v1/predictions` с полем `version`. Тело: `input`. Заголовок: `Authorization: Bearer <токен>`.
- `Prefer: wait` держит HTTP-запрос открытым, по умолчанию 60 с, можно короче. Без заголовка ответ сразу со статусом `starting`, `output: null`. План использует асинхронный режим.
- Ответ: `id`, `status`, `output`, `error`, `logs`, `urls` (`get`, `cancel`, `web`).
- Статусы (`https://replicate.com/docs/reference/http`): `starting`, `processing`, `succeeded`, `failed`, `canceled`. В примере ответа стоит `successful`, в тексте опроса — `succeeded`. Страница противоречит сама себе.
- Отмена: `POST /v1/predictions/{id}/cancel` (описан в справочнике). Тело ответа не описано. Страница «cancel-a-prediction» вернула 404, отдельно её не прочитать.
- Веб-хуки: `webhook`, `webhook_events_filter` (`start`, `output`, `logs`, `completed`). Не используем (нужен публичный адрес).
- Cancel-After: заголовок, дедлайн от 5 с до 24 ч; отделён от `Prefer: wait`.
- Срок файлов (`https://replicate.com/docs/topics/predictions/output-files`, проверено 2026-10-28): для запусков через API файлы удаляются через 1 час; для запусков из веб-интерфейса хранятся бессрочно. Значит, скачивать сразу после `succeeded`.
- Лимиты (600 создания в минуту и т. п.) в спецификации помечены как данные из стороннего источника. Официально не проверены, в задаче не используются.

### Что не подтверждено
- Как Replicate сообщает о срабатывании safety checker: статус `failed` с текстом, или `succeeded` с чёрным кадром, или иначе. Документация, которую я прочитал, это не описывает. Без пробного запроса с токеном статус `Filtered` не реализуем.
- Эндпоинт проверки токена (F1.2) не найден.
- Цены: в задаче не указываются; официальную цену за выход и за секунды GPU ещё не нашёл.

### Связь с кодом
- Как в 025: слот семафора только на отправку, скачивание потоком, handle в `generations`.
- Replicate не даёт позицию очереди (в документации нет поля), `Queued` без позиции.

## План

Опирается на 025 (контракт и поток) и 036 (`ProviderKind.Replicate`, `ProviderConfig`, `SecretNames.For`). Здесь только отличия Replicate.

### Типы и классы (`ImageGen/ImageGen.Core/Replicate/`, новые)
- `ReplicateImageProvider : IImageProvider`: сопоставляет вызовы контракта с `ReplicateClient`.
- `ReplicateClient`: `CreatePredictionAsync`, `GetPredictionAsync(urls.get)`, `CancelPredictionAsync(urls.cancel или /v1/predictions/{id}/cancel)`. Токен берётся из `ISecretStore` на каждый запрос.
- `ReplicateJson` (записи для `System.Text.Json`, в `CoreJsonContext`).
- `ProviderJobHandle` для Replicate: `RequestId` (id предсказания), `GetUrl`, `CancelUrl`. Адреса берутся из ответа создания, если поле `urls` есть.

### Адресация модели
- `ModelId` `owner/name` → официальная модель: `POST /v1/models/{owner}/{name}/predictions`.
- `ModelId` `owner/name:version` → версия: `POST /v1/predictions` с `version` (вопрос 2).
- Список моделей в v1 задаётся вручную в конфиге; схема входа и `DescribeModelAsync` — F2.5, P1, в 026 не делается. Метод интерфейса возвращает только id.

### Поток отправки и опроса
1. `SubmitAsync` → `POST` без заголовка `Prefer`; сохраняется handle из ответа.
2. `PollAsync` читает `GetUrl` один раз; цикл и интервалы — общий `ProviderJobPoller` из 025 (ADR 0005).
3. Сопоставление статусов:
   - `starting` → `Queued` (без позиции);
   - `processing` → `Running` (логи из ответа);
   - `succeeded` → скачивание `output` (первый элемент, если массив; строка — как есть), затем `Succeeded`;
   - `successful` → по умолчанию то же, что `succeeded` (вопрос 4);
   - `failed` → `Failed` с текстом `error`; тип ошибки — `Unknown`, пока не подтверждён (вопрос 3);
   - `canceled` → `Cancelled`;
   - неизвестный статус → опрос продолжается, предел — `Timeout` из `ProviderConfig`.
4. Скачивание выполняется до перехода в `Succeeded`: срок файла 1 час.

### Отмена
- `CancelAsync` → `POST CancelUrl`. Итог — по опросу: `canceled` → `Cancelled`. Ответ `POST` не описан, поэтому до опроса статус `CancellationRequested`.
- Если `POST` вернул ошибку, сопоставление как в 025: `404` → `Failed`, остальное → `Failed` с текстом.

### Крайние случаи
- Сохранён handle, `succeeded`, файл не скачан (сбой приложения) → при восстановлении скачивание по `output` из опроса; срок 1 час может истечь → `Failed` с текстом «срок хранения истёк».
- `output` пустой при `succeeded` → `Failed` с `EmptyResponse`.
- Ответ `429` при создании → `RateLimited`; повтор создания не делается (платный запуск).
- Токен не задан → `InvalidKey` без HTTP.

### Тест-кейсы (условие → результат; HTTP подменяется `StubHttpMessageHandler`)
- Официальная модель `owner/name` → `POST .../models/owner/name/predictions`, заголовок `Authorization: Bearer <токен>`, заголовка `Prefer` нет.
- Модель `owner/name:version` → `POST /v1/predictions` с `version`.
- Ответ создания со статусом `starting` → `Queued`, позиции нет.
- Опрос `processing` → `Running`.
- Опрос `succeeded` с `output` (URL) → скачивание, файл сохранён, `Succeeded`.
- Опрос `successful` → то же, что `succeeded` (пока вопрос 4 не закрыт).
- Опрос `failed` с `error` → `Failed`, текст без токена.
- Опрос `canceled` → `Cancelled`.
- Неизвестный статус `weird` → опрос продолжается; после порога → `Failed` с `Timeout`.
- `succeeded` с пустым `output` → `Failed`, `EmptyResponse`.
- `POST` отмены → `CancellationRequested`, затем опрос `canceled` → `Cancelled`.
- `429` при создании → `RateLimited`, второй `POST` создания не отправлен.
- Сбой сети при опросе → опрос повторён, новое создание не отправлено.
- Перезапуск с handle в `processing` → опрос (`ProviderJobPoller`) с тем же handle, `SubmitAsync` не вызван.

### Команды проверки
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Replicate/ReplicateImageProvider.cs` (новый), `ImageGen/ImageGen.Core/Replicate/ReplicateClient.cs` (новый), `ImageGen/ImageGen.Core/Replicate/ReplicateJson.cs` (новый), `ImageGen/ImageGen.Core/Providers/ProviderKind.cs` (значение `Replicate`), `ImageGen/ImageGen.Core/Domain/Keys/SecretNames.cs`, `ImageGen/ImageGen.Core/Serialization/CoreJsonContext.cs`, `ImageGen/ImageGen.Core/CoreServiceCollectionExtensions.cs`, `ImageGen/ImageGen.Core.Tests/Replicate/ReplicateImageProviderTests.cs` (новый), `ImageGen/ImageGen.Core.Tests/Services/GenerationServiceTests.cs` (тесты поведения по провайдеру, если нужны)

## Отчёт

## Комментарии

### 2026-10-28, claude-qnrqyt
План написан (статус `planned`), код не начат. Блокеры: ADR 0003 (статус «предложено»), задача 025 (контракт), задача 036 (`ProviderKind.Replicate`). Вопросы владельцу:

1. Порядок: 036, затем 025, затем 026. Ок?
2. Адресация моделей: `owner/name` → официальная модель (`/v1/models/{owner}/{name}/predictions`), `owner/name:version` → версия (`/v1/predictions` с `version`). Ок?
3. Срабатывание safety checker: в прочитанной документации не описано, это статус `failed` с текстом, `succeeded` с чёрным кадром или иначе. Без пробного запроса с токеном `Filtered` не реализуем; до него фильтр показывается как `Failed` с текстом провайдера, тип ошибки `Unknown`. Нужен ли пробный запрос (`poc/v0.1/026-provider-replicate/`) с вашим токеном?
4. Статус успеха: в примере ответа `successful`, в тексте опроса `succeeded`. План принимает оба; неизвестный статус продолжает опрос до лимита. Подтвердить на реальном ответе?
5. Срок файлов: по `output-files` для запусков через API файлы удаляются через 1 час (проверено 2026-10-28). План скачивает сразу после `succeeded`. Ок?
6. Cancel-After (дедлайн предсказания 5 с–24 ч) ограничивает расход GPU. Ставить ли его, и какое значение по умолчанию? Без вашего числа не ставлю.
7. Проверка токена (F1.2): эндпоинт не проверен. Какой запрос использовать?
8. Лимит 600 создания в минуту (спецификация, сторонний источник) не подтверждён официально. Ок, что в задаче не используется?
9. Тело ответа `POST .../cancel` в справочнике не описано. План опирается на финальный статус `canceled` в опросе. Ок?

### 2026-10-28, ревью (claude, Windows)
Общий порядок по пересечениям файлов и таблиц (предложение ревью, решает владелец): 035 → 036 → 020 → 017 → 018 → 019 → 027; 025 и 026 после 036 и принятия ADR 0003. Причины: 036 меняет `ImageModel`, `image_models` (ключ `(provider_id, id)`), `SessionSettings.ImageModelIds` (`ModelRef`) и `GenerationService`; 020 вводит полный список параметров модели, из которого 018 берёт лимит референсов; 017 вводит каталог чат-моделей, `IImageFileStore.ReadAsync` и `ImageDataUrl` для 018, 019 и 027. Номера миграций в планах заменены на `NNNN_<смысл>.sql` (правило в `docs/tasks.md`), строка «Среда» добавлена в «Описание».

### 2026-10-28, владелец (ответы в чате, записал ревью)
Порядок выполнения (решение владельца): 035 → 036 → 020 → 017 → 018 → 019 → 027, затем 025 и 026. План одобрен владельцем сейчас, не дожидаясь 016 (статус `approved`); начинать в этом порядке, после мерджа предыдущей задачи сверить план с кодом.
Ответы: порядок 036 → 025 → 026; адресация `owner/name` и `owner/name:version` — как в плане; фильтр модерации до живой проверки показывается ошибкой с текстом провайдера; `succeeded` и `successful` равнозначны; файлы скачиваются сразу (живут 1 час); **`Cancel-After: 10m`** на каждое предсказание (тест-кейс: заголовок есть в запросе создания); проверка токена — по допущению, живая проверка вручную; лимиты из стороннего источника не используются; итог отмены — по опросу.
