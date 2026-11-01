---
id: 176
status: done
assignee: codex-gpt6
claimed: 2026-11-01T00:00:00Z
created: 2026-11-01
---

# Сборка и тесты LiveCheck в CI

## Описание
Найдено при ревью 2026-11-01: проекты `ImageGen.LiveCheck` и `ImageGen.LiveCheck.Tests` (задача 170) есть в `ImageGen.slnx`, но `.github/workflows/build.yml` их не собирает и не тестирует — инструмент может незаметно сломаться к моменту прогона. Владелец разрешил правку workflow в объёме этой задачи (2026-11-01).

Зависит от: —
Среда: Linux или Windows; проверка — CI.

## Критерии готовности
- В задании `core` файла `.github/workflows/build.yml` добавлены: `dotnet format` для `ImageGen.LiveCheck` и `ImageGen.LiveCheck.Tests` с `--verify-no-changes`, сборка `ImageGen.LiveCheck.Tests` (Release) и `dotnet test --no-build` — тем же образцом, что для Core/Data/Presentation.
- Тесты не ходят в сеть и не требуют ключей (уже так: заглушки); переменные `IMAGEGEN_LIVE_*` в CI не задаются.
- CI зелёный на PR; другие задания и `release.yml` не меняются.

## Анализ
`build.yml`, задание `core` (ubuntu-latest): шаги идут парами format → build → test для Core, Data, Presentation. Проект `ImageGen.LiveCheck.Tests` ссылается на `ImageGen.LiveCheck` (Core, Data, Presentation) — собирается на Linux без дополнительных нагрузок.

## План
1. Локально: `dotnet format` обоих проектов `--verify-no-changes`, `dotnet build ImageGen/ImageGen.LiveCheck.Tests/ImageGen.LiveCheck.Tests.csproj -c Release`, `dotnet test --project … -c Release --no-build`.
2. Добавить четыре шага в конец задания `core` (после Presentation) в том же стиле.
3. Проверка: CI на PR (задание `core` зелёное, LiveCheck в его логе), `scripts/check.sh`.

Файлы: `.github/workflows/build.yml`.

## Отчёт

Добавлены четыре шага в `core` workflow: форматирование LiveCheck и его тестового проекта, Release-сборка тестового проекта и запуск тестов без повторной сборки. Секреты и `IMAGEGEN_LIVE_*` в CI не добавлялись; остальные jobs и `release.yml` не менялись.

Локально прошли `dotnet format` обоих проектов (`--verify-no-changes`), Release-сборка `ImageGen.LiveCheck.Tests` и тесты (4/4). `scripts/check.sh` прошёл; после публикации PR проверяется CI.

## Комментарии

### 2026-11-01, владелец (в чате, записал claude-audit)
Разрешено добавить сборку и тесты LiveCheck в `build.yml` (выбрано в вопросе); объём — только эти шаги.
