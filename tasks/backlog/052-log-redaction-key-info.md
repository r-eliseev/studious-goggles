---
id: 052
status: approved
assignee:
claimed:
created: 2026-10-28
---

# Журнал: не писать ответ проверки ключа целиком

## Описание
Дефект (ревью журнала первой ручной проверки, 2026-10-28). На уровне Debug `LoggingHttpHandler` пишет тело ответа `GET /api/v1/key` целиком. В нём `label` — маска ключа вида `sk-or-v1-0d5...c30` (первые 12 и последние 3 символа), а также `creator_user_id`, `workspace_id` и лимиты аккаунта. Правило `docs/conventions.md`: из ключа можно показывать только последние 4 символа; идентификаторы аккаунта в журнале не нужны.

Зависит от: —.
Среда: Linux или Windows (только `ImageGen.Core` и тесты).

## Критерии готовности
- Тело ответа проверки ключа (`/key` у OpenRouter и аналогичные запросы других провайдеров) в журнал не пишется ни на каком уровне; пишутся только метод, путь, код и длительность.
- Тест: ответ `/key` с полем `label` и `creator_user_id` → в записях журнала их значений нет.

## Анализ
- Где: `ImageGen/ImageGen.Core/Http/LoggingHttpHandler.cs` и `BodyRedactor.cs` (задача 045, ADR 0010). Сейчас редактор вырезает base64 и обрезает длину, но не знает про чувствительные ответы.
- Найдено в файле журнала MAUI на Windows: строка `Response body GET /api/v1/key: {"data":{"label":"sk-or-v1-0d5...c30", …, "creator_user_id":"user_…"}}`.

## План
1. `LoggingHttpHandler`: список путей без записи тел (`key`, а также пути проверки ключа других провайдеров, если они есть в `ProviderClientBase`) — запрос и ответ таких путей логируются только строкой «метод, путь, код, длительность».
2. Дополнительно `BodyRedactor`: поля `label`, `key`, `api_key`, `*_user_id` в JSON-телах заменяются на `<redacted>` (на случай других эндпоинтов).
3. Тест-кейсы: ответ `/key` → в журнале нет `sk-or-` и `user_`; тело другого запроса с полем `api_key` → `<redacted>`; обычный ответ картинки по-прежнему пишется с вырезанным base64.
4. Документация: `docs/conventions.md` (раздел про журнал) — какие тела не пишутся.

### Команды проверки
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Http/LoggingHttpHandler.cs`, `ImageGen/ImageGen.Core/Http/BodyRedactor.cs`, `ImageGen/ImageGen.Core.Tests/Http/*`, `docs/conventions.md`

## Отчёт

## Комментарии

### 2026-10-28, ревью (claude, Windows)
В файле журнала владельца на этой машине уже есть такая строка (`%LOCALAPPDATA%\User Name\com.companyname.imagegen.maui\Data\logs\imagegen-20261028.log`). Файл не трогал; ключ целиком в нём не записан, только маска OpenRouter.

### 2026-10-28, владелец (в чате, записал ревью)
План одобрен.
