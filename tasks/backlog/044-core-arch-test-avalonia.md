---
id: 044
status: todo
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
- Правка в одну строку списка префиксов. Пересечений с задачами в работе нет: 036 не трогает `ImageGen.Core.Tests/Architecture/`.
- Проверить «красный» случай можно временной ссылкой Core на пакет Avalonia (в коммит не входит) или словами в «Отчёте».

## План

## Отчёт

## Комментарии
