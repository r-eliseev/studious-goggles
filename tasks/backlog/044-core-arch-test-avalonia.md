---
id: 044
status: done
assignee:
claimed:
created: 2026-10-28
---

# Архитектурный тест Core: запрет ссылок на Avalonia

## Описание
Правило слоёв (`docs/architecture.md`, «Правила слоёв»; `AGENTS.md`, «Главные правила кода»): ядро не знает про UI-фреймворки. Архитектурный тест ядра защищает это правило только для MAUI: `ImageGen.Core.Tests/Architecture/CoreDependencyTests.cs:5-9` запрещает префиксы `Microsoft.Maui`, `Microsoft.AspNetCore`, `Microsoft.Data.Sqlite`. После появления третьей оболочки (ADR 0006) ссылка Core на `Avalonia*` тестом не ловится. Для сравнения, тест слоя представления (`ImageGen.Presentation.Tests/Architecture/PresentationDependencyTests.cs:5-9`) `Avalonia` уже запрещает.

Нужно добавить `Avalonia` в запрещённые префиксы теста Core и поправить описание теста в `docs/architecture.md` (абзац «Зависимости»).

Источник: аудит 2026-10-28 (`docs/log/2026-10-28-audit.md`).
Среда: Linux или Windows (нужен только .NET 10 SDK).

## Критерии готовности
- `CoreDependencyTests` падает, если сборка `ImageGen.Core` ссылается на сборку с префиксом `Avalonia`; на текущем коде тест зелёный.
- `docs/architecture.md` перечисляет запрещённые для Core префиксы так же, как тест.
- `ImageGen.Core.Tests` зелёные, `dotnet format --verify-no-changes` для `ImageGen.Core.Tests` без изменений, `scripts/check.sh` пройден.

## Анализ
- Правка одной строки списка `ForbiddenPrefixes` и абзаца «Зависимости» в `docs/architecture.md` (раздел «Текущая раскладка» и «Правила слоёв»). Пересечений с задачами в работе нет: ни одна из одобренных задач не трогает `ImageGen.Core.Tests/Architecture/`.
- Текущий тест запрещает `Microsoft.Maui`, `Microsoft.AspNetCore` и `Microsoft.Data.Sqlite`. Прямая ссылка Core на Avalonia сейчас прошла бы незамеченной.
- Имя сборки Avalonia начинается с `Avalonia` (`Avalonia`, `Avalonia.Base`, `Avalonia.Controls` и т. п.), поэтому префикс `Avalonia` ловит все её сборки.

## План

Команды проверки: `dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj`, `dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes`, `scripts/check.sh`.

1. Тест: добавить префикс `Avalonia` в `ForbiddenPrefixes` в `CoreDependencyTests.cs`. Тест-кейс словами: «сборка Core ссылается на сборку с именем, начинающимся с `Avalonia`» → тест падает. Проверка «красного» случая — временная ссылка `ImageGen.Core` на пакет Avalonia в рабочей копии; в коммит не входит, откатывается до сборки.
2. `docs/architecture.md`: в абзаце «Зависимости» (раздел «Правила слоёв») перечислить запрещённые для Core префиксы так же, как в тесте: `Microsoft.Maui`, `Microsoft.AspNetCore`, `Microsoft.Data.Sqlite`, `Avalonia`.
3. Прогон команд проверки; зелёный тест на текущем коде.

Файлы: `ImageGen/ImageGen.Core.Tests/Architecture/CoreDependencyTests.cs`, `docs/architecture.md`

## Отчёт

Сделано (`claude-536f`, 2026-10-29) по плану: в `ForbiddenPrefixes` добавлен `Avalonia`; `docs/architecture.md` (раздел «Правила слоёв») перечисляет запрещённые префиксы так же, как тест.

Проверено: `CoreAssembly_ReferencedAssemblies_DoNotIncludeUiOrStorage` зелёный на текущем коде; красный случай — временно префикс, который Core точно использует, тест упал; префикс возвращён. `ImageGen.Core.Tests` проходят, `dotnet format --verify-no-changes` чистый, `scripts/check.sh` проходит.

Основание работы: владелец в `/goal` (2026-10-29) велел закончить задачи и не останавливаться на одобрении планов. Статус `approved` я сам не ставил — реализация сделана по этому указанию, а не по записанному одобрению плана.

## Комментарии
