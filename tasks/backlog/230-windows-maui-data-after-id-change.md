---
id: 230
status: approved
assignee:
claimed:
created: 2026-11-07
---

# MAUI Windows: данные и ключи после смены ApplicationId

## Описание
Находка ревью 2026-11-04 (коммит `4b9e0b3`, задача 030): у Windows-цели MAUI `ApplicationId` сменён с `com.companyname.imagegen.maui` на `com.eliseev.imagegen` без переноса данных. Прежний комментарий в `ImageGen.Maui.csproj` предупреждал, что на Windows id определяет папки данных и секретов. Для unpackaged-приложения `FileSystem.Current.AppDataDirectory` (папка данных, `MauiAppPaths`) и хранилище `SecureStorage` (ключи, `MauiSecretStore`), по всей видимости, зависят от id — после обновления сессии, картинки и ключи могут «пропасть» (старая папка остаётся на диске). Фактические пути на Windows не измерены (`docs/status.md`, задача 030).

Допущение (владелец может выбрать иначе — вернуть прежний id до миграции): данные переносятся автоматически при первом запуске новой версии.

Зависит от: —
Среда: **только Windows** (измерение путей, сборка и ручная проверка); логика переноса — общая, тестируется на Linux.

## Критерии готовности
- Измерено и записано в `docs/knowledge/dev-environment.md` («Логи»/«Пакеты»): папка данных MAUI Windows для старого id (релиз ≤ v0.1.10; первая строка журнала `ImageGen … Data: …`) и для нового; где `SecureStorage` unpackaged-приложения хранит значения (файл в папке данных, реестр или др.) и зависит ли от id.
- `LegacyDataMigration.TryMigrate(string legacyDirectory, string currentDirectory, ILogger)` в `ImageGen.Presentation` (без платформенных типов): если в `currentDirectory` нет `imagegen.db` (имя файла базы — `SqliteDatabase.FileName`), а в `legacyDirectory` есть — копирует всё содержимое `legacyDirectory` во временную папку рядом с `currentDirectory`, затем переименовывает в `currentDirectory` (если `currentDirectory` существует и пуст, кроме `logs/`, — переносит содержимое поверх); старая папка не удаляется; одна Information-запись (сколько файлов, откуда/куда, без содержимого). Любая ошибка — Warning, приложение стартует с пустыми данными (как сейчас), повторная попытка — при следующем запуске.
- Вызов в `MauiProgram` под `#if WINDOWS` **до** создания логгера и открытия базы: путь старой папки вычисляется по измеренному правилу (шаг 1), а не вписывается строкой из чужой машины.
- Ключи: если `SecureStorage` переносим (например, файл в папке данных) — он копируется вместе с данными; если нет — после переноса данных первый запуск откроет экран ключа (уже так при отсутствии ключа) и покажет один раз сообщение «Provider keys could not be moved after the update; enter them again.» (ресурс).
- Тесты (Linux, временные папки): перенос при пустой новой папке; ничего не делать, если в новой есть база; ничего, если в старой нет базы; ошибка копирования оставляет новую папку без частичных данных; логи в новой папке не мешают переносу.
- Ручная проверка на Windows в 016: установить релиз со старым id, создать сессию и ключ, поставить новую версию — сессия на месте; ключ на месте или показано сообщение.

## Анализ
`ImageGen/ImageGen.Maui/ImageGen.Maui.csproj` (`ApplicationId`), `ImageGen/ImageGen.Maui/Services/MauiAppPaths.cs` (`FileSystem.Current.AppDataDirectory`), `ImageGen/ImageGen.Maui/Services/MauiSecretStore.cs` (`SecureStorage.Default`), `ImageGen/ImageGen.Maui/MauiProgram.cs` (≈строка 62: `paths`, `logDirectory`, логирование), `ImageGen/ImageGen.Data/SqliteDatabase.cs` (имя файла базы), задача 030 (`tasks/backlog/030-app-identity.md`, комментарии). Шаг измерения нельзя сделать на Linux: если исполнитель без Windows — выполнить только общую часть с тестами и оставить в «Комментарии» точные шаги измерения для владельца.

## План
1. На Windows: измерить пути и место `SecureStorage` (см. «Критерии»), записать.
2. Падающие тесты `LegacyDataMigrationTests` (Presentation.Tests).
3. `LegacyDataMigration`, ресурс сообщения.
4. Вызов в `MauiProgram` под `#if WINDOWS` с вычислением старого пути; сборка Windows-цели (CI).
5. Пункт 016; запись в knowledge; в 030 — комментарий о том, что миграция данных выделена сюда.

Файлы: `ImageGen/ImageGen.Presentation/Services/LegacyDataMigration.cs` (новый), `ImageGen/ImageGen.Presentation.Tests/Services/LegacyDataMigrationTests.cs` (новый), `ImageGen/ImageGen.Maui/MauiProgram.cs`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.cs`, `docs/knowledge/dev-environment.md`, `tasks/v0.1/016-skeleton-manual-check.md`, `tasks/backlog/030-app-identity.md`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-windows10.0.19041.0
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
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

### 2026-11-07, claude-audit
Создано по находке ревью 2026-11-04 (030, смена Windows ApplicationId без переноса). Допущение: переносить, а не откатывать id. План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
Поручение «на те проблемы, для которых нет задач, создай задачи»; план одобрен ранее данным поручением «заапрувь все таски которые будут».
