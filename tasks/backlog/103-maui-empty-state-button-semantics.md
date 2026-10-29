---
id: 103
status: todo
assignee:
claimed:
created: 2026-10-29
---

# MAUI: у кнопки «New session» на пустом экране нет описания для экранного диктора

## Описание
Найдено аудитом 2026-10-29 (`docs/log/2026-10-29-audit.md`): `ImageGen/ImageGen.Maui/Pages/SessionsPage.xaml` (блок `EmptyState`, кнопка с `Command="{Binding NewSessionCommand}"`) не задаёт `SemanticProperties.Description`, хотя `docs/conventions.md` требует его у всех кнопок. Остальные кнопки MAUI-разметки выборочно проверены — описание есть. Сверить Avalonia (`AutomationProperties.Name` у той же кнопки в `SessionsView.axaml`).

Зависит от: —
Среда: Linux или Windows.

## Критерии готовности
- У кнопки задано описание из `AppResources` (та же строка, что и текст кнопки).
- Grep по `ImageGen.Maui/**/*.xaml` не находит других `Button`/`ImageButton` без `SemanticProperties.Description` (или исключения перечислены в «Отчёте» с причиной).
- То же в Avalonia для `AutomationProperties.Name`.

## Анализ

## План

## Отчёт

## Комментарии
