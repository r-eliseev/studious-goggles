---
id: 073
status: todo
assignee:
claimed:
created: 2026-10-28
---

# Десктоп: строка меню macOS и сочетания клавиш

## Описание
По `docs/design/research.md` («Соглашения платформ») и макетам: на macOS у приложения обязательна строка меню (ImageGen, File, Edit, View, Window, Help), на всех десктопах — сочетания клавиш для основных действий: новая сессия (`Cmd/Ctrl+N`), настройки (`Cmd/Ctrl+,`, частично в 065), Repeat (`Cmd/Ctrl+R`), New seed (`Cmd/Ctrl+Shift+R`), Edit prompt (`Cmd/Ctrl+E`), Save image (`Cmd/Ctrl+S`, вместе с 074). Avalonia — `NativeMenu`; MAUI на Mac — `MenuBarItem`.

Справочник: `docs/design/implementation.md` (ключи ресурсов, значения, шрифты, значки — дословно оттуда); токены: `docs/design/tokens.json`; макеты: `docs/design/prototype.html` (открыть: `python3 -m http.server` в `docs/design/`, затем http://localhost:8000/prototype.html; опубликованная версия — https://claude.ai/artifact/M9QzPf39KLkRFTnYBtCi6F); решение: ADR 0014.

Зависит от: 065, 069.
Среда: Linux или Windows для Avalonia (сборка и тесты); проверка строки меню — только на macOS (нет машины, `docs/status.md`: «macOS-части по документации с пометкой „не проверено“»).

## Критерии готовности
Детализировать при планировании: какие действия, в каких оболочках, как действие выбирает текущую карточку (фокус или последняя готовая).

## Анализ

## План

## Отчёт

## Комментарии
