---
id: 055
status: planned
assignee:
claimed:
created: 2026-10-28
---

# Копирование текста в чате

## Описание
Владелец (ручная проверка 016, 2026-10-28): нужно копирование текста в чате. Сейчас тексты сообщений, промпт прогона и детали карточки — обычные надписи, их нельзя выделить и скопировать. Нужно в обеих оболочках (MAUI и Avalonia): выделять и копировать текст сообщений и промпта, плюс быстрая кнопка «Copy» у сообщения ассистента и у промпта прогона.

Зависит от: —.
Среда: Linux или Windows (Avalonia — headless-тесты на Linux; MAUI — сборка Android и CI, ручная проверка на Windows — 016).

## Критерии готовности
- Текст сообщений пользователя и ассистента, промпт прогона и текст ошибки можно выделить мышью и скопировать стандартным сочетанием клавиш (Ctrl+C / Cmd+C) в обеих оболочках на десктопе.
- У сообщения ассистента и у промпта прогона есть кнопка «Copy» (иконка или текст), которая кладёт весь текст в буфер обмена; после нажатия — короткое подтверждение («Copied»), доступное экранному диктору.
- На Android — долгое нажатие на сообщение или кнопка «Copy» копирует текст.
- Тест-кейсы из плана проходят.

## Анализ
- Где текст: `MessageItemViewModel.Text`, `RunItemViewModel.PromptText`, `GenerationItemViewModel.ErrorText` (Presentation); разметка — `ImageGen.Maui/Pages/ChatPage.xaml`, `ImageGen.Avalonia/Views/ChatView.axaml`.
- Выделение: в Avalonia — `SelectableTextBlock`; в MAUI у `Label` выделения нет, обычный путь — `Editor`/`Entry` с `IsReadOnly="True"` в виде надписи (на Windows выделяется), либо только кнопка «Copy» (решение при реализации, записать в «Отчёт»).
- Буфер обмена: MAUI — `Clipboard.Default.SetTextAsync`; Avalonia — `TopLevel.Clipboard.SetTextAsync`. Модели страниц не знают UI-типов (`docs/conventions.md`), поэтому нужен интерфейс `IClipboardService` в Presentation с реализациями в оболочках.

## План
1. Presentation: `Services/IClipboardService` (`SetTextAsync(text)`); команды `CopyCommand` у `MessageItemViewModel` и `RunItemViewModel` (копируют `Text` и промпт без префикса «Prompt:»); короткое состояние «скопировано» (свойство с текстом `AppResources.Copied`, сбрасывается через 2 с через `TimeProvider`/таймер, который можно подменить в тесте). Строки `Copy`, `Copied` в `AppResources`.
2. Оболочки: реализации `MauiClipboardService` и `AvaloniaClipboardService`, регистрация в DI. Разметка: выделяемый текст (Avalonia — `SelectableTextBlock`; MAUI — выбранный при реализации способ) и кнопка «Copy» с `AutomationProperties.Name`/`SemanticProperties.Description`.
3. Тест-кейсы:
   - `MessageItemViewModel.CopyCommand` → `IClipboardService.SetTextAsync` с текстом сообщения; `Copied` видно и пропадает после времени.
   - `RunItemViewModel.CopyCommand` → в буфере точный промпт прогона (без «Prompt: »).
   - Avalonia headless: текст сообщения — `SelectableTextBlock`; кнопка «Copy» у сообщения ассистента вызывает буфер обмена (подмена).
4. Документация: пункт в 016 и 046 (выделение, Ctrl+C, кнопка Copy); `docs/knowledge/maui.md` — как сделано выделение в MAUI.

### Команды проверки
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format <каждый изменённый проект> --verify-no-changes
scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Presentation/Services/IClipboardService.cs` (новый), `ImageGen/ImageGen.Presentation/PageModels/MessageItemViewModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/RunItemViewModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/ChatPageModel.cs`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.*`, `ImageGen/ImageGen.Maui/Services/MauiClipboardService.cs` (новый), `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, `ImageGen/ImageGen.Maui/MauiProgram.cs`, `ImageGen/ImageGen.Avalonia/Services/AvaloniaClipboardService.cs` (новый), `ImageGen/ImageGen.Avalonia/Views/ChatView.axaml`, `ImageGen/ImageGen.Avalonia/Composition/AppServices.cs`, тесты, `docs/knowledge/maui.md`, `tasks/v0.1/016-skeleton-manual-check.md`, `tasks/backlog/046-desktop-manual-check.md`

## Отчёт

## Комментарии
