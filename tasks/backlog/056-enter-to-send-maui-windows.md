---
id: 056
status: planned
assignee:
claimed:
created: 2026-10-28
---

# Отправка по Enter в MAUI под Windows

## Описание
Windows-часть задачи 050, выделенная отдельно по правилу `docs/tasks.md` («часть, которая собирается и проверяется только на Windows, — отдельной задачей»). Владелец при ручной проверке 016 (2026-10-28): «Enter всё ещё не отправляет сообщение» — в MAUI под Windows. Нужно: в поле ввода чата Enter отправляет сообщение, Shift+Enter переносит строку, как уже сделано в Avalonia (050).

Зависит от: —.
Среда: только Windows (`Platforms/Windows`, WinUI `TextBox`; проверка запуском).

## Критерии готовности
- MAUI на Windows: Enter в поле чата отправляет (если отправка доступна), Shift+Enter вставляет перевод строки, Enter на пустом поле ничего не делает и строку не добавляет.
- Поведение совпадает с Avalonia (050, `ChatKeyboardFlowTests`).
- Ручная проверка на Windows записана в «Отчёте»; сборка Windows и Android без предупреждений.

## Анализ
- У MAUI `Editor` нет события клавиш. На Windows его нативный элемент — WinUI `TextBox`; нажатие перехватывается `PreviewKeyDown` (до вставки перевода строки) через `EditorHandler.Mapper.AppendToMapping` для поля чата (по `AutomationId` или своему классу `ChatEditor`).
- Команда — `ChatPageModel.SendCommand` (Presentation); проверка «можно ли отправить» — её `CanExecute`.
- Shift: `Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)` (WinUI 3) — проверить на машине.
- Задача 050 после выделения отвечает только за Android-часть (действие «Отправить» у экранной клавиатуры) — комментарий в 050.

## План
1. `ImageGen.Maui/Controls/ChatEditor.cs` — наследник `Editor` с привязываемой командой отправки `SendCommand` (без логики).
2. `Platforms/Windows/ChatEditorKeyHandling.cs` — маппинг для `ChatEditor`: на `PreviewKeyDown` Enter без Shift → `Handled = true`; если `SendCommand.CanExecute(null)` — `Execute(null)`. Shift+Enter — пропустить.
3. Регистрация маппинга в `MauiProgram` (под `#if WINDOWS`); в `ChatPage.xaml` поле ввода — `ChatEditor` с привязкой `SendCommand`.
4. Ручная проверка на Windows: «hi» + Enter → отправлено, поле пустое; Shift+Enter → новая строка; Enter на пустом поле — ничего. Запись в «Отчёт» и пункт в 016.
5. `docs/knowledge/maui.md` — как перехвачен Enter в WinUI.

### Команды проверки
```
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-windows10.0.19041.0
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Maui/Controls/ChatEditor.cs` (новый), `ImageGen/ImageGen.Maui/Platforms/Windows/ChatEditorKeyHandling.cs` (новый), `ImageGen/ImageGen.Maui/MauiProgram.cs`, `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, `docs/knowledge/maui.md`, `tasks/v0.1/016-skeleton-manual-check.md`

## Отчёт

## Комментарии
