---
id: 050
status: approved
assignee:
claimed:
created: 2026-10-28
---

# Отправка сообщения по Enter

## Описание
Владелец (2026-10-28): сообщения в чате должны отправляться по Enter. Перевод строки — Shift+Enter. В обеих оболочках одинаково. Сейчас в Avalonia отправка — Ctrl+Enter, Enter переносит строку; в MAUI отправка только кнопкой Send.

Зависит от: —.
Среда: Linux или Windows (Avalonia — headless-тесты на Linux; MAUI Windows-часть — код в `Platforms/Windows`, сборку проверяет CI, ручная проверка — задача 016).

## Критерии готовности
- Avalonia: Enter отправляет (если команда отправки доступна), Shift+Enter вставляет перевод строки; пустой текст по Enter не отправляется и не добавляет строку.
- MAUI на Windows: то же поведение в поле ввода чата.
- MAUI на Android: экранная клавиатура показывает действие «Отправить»; аппаратный Enter (если есть) — как на Windows; перевод строки на экранной клавиатуре — по решению при реализации (записать в «Отчёт»).
- Подсказка в поле ввода говорит, как отправить и как перенести строку (`AppResources`).

## Анализ
- Avalonia: `Views/ChatView.axaml.cs`, обработчик `OnInputKeyDown` (сейчас проверяет `KeyModifiers.Control`). Нужно поменять условие и не пропускать Enter в `TextBox`, когда он отправляет (`e.Handled = true`). Для перехвата до вставки строки может понадобиться обработчик на туннельной фазе (`AddHandler(KeyDownEvent, …, RoutingStrategies.Tunnel)`) — проверить в headless-тесте.
- MAUI: `Editor` своего события клавиш нет. На Windows — платформенный обработчик: `EditorHandler.Mapper.AppendToMapping` и подписка на `PreviewKeyDown` нативного `TextBox` (WinUI) в `Platforms/Windows`; на Android — `ImeOptions` (`ImeAction.Send`) и `EditorAction`.
- Логика «можно ли отправить» уже в `ChatPageModel.SendCommand.CanExecute`; оболочки только вызывают команду.

## План
1. Presentation: ничего не меняется, кроме строки подсказки `ChatPlaceholder` («Message… (Enter — send, Shift+Enter — new line)»).
2. Avalonia: `OnInputKeyDown` на туннельной фазе: Enter без Shift → если `SendCommand.CanExecute` — выполнить и `Handled = true`; иначе `Handled = true` (строку не добавлять). Shift+Enter → обычный перевод строки.
3. MAUI:
   - `Platforms/Windows/ChatEditorKeys.cs`: маппер `EditorHandler` для поля чата (по `AutomationId` или своему наследнику `ChatEditor`), `PreviewKeyDown`: Enter без Shift → выполнить команду из `BindingContext`, `Handled = true`.
   - `Platforms/Android`: `ImeOptions = Send` у поля чата, `EditorAction` → команда отправки.
4. Тест-кейсы:
   - Avalonia headless: ввод «hi» и Enter → `SendCommand` выполнен, поле пустое; Shift+Enter → в тексте `\n`, команда не вызвана; пустое поле и Enter → ничего, строка не добавлена.
   - MAUI: модульных тестов нет (платформенный код); сборка Android и CI для Windows; ручные пункты — в 016.
5. Документация: `docs/knowledge/avalonia.md` и `docs/knowledge/maui.md` — как перехвачен Enter; пункт в 016 и 046.

### Команды проверки
```
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Avalonia/Views/ChatView.axaml.cs`, `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, `ImageGen/ImageGen.Maui/Platforms/Windows/ChatEditorKeys.cs` (новый), `ImageGen/ImageGen.Maui/Platforms/Android/*` (настройка поля), `ImageGen/ImageGen.Maui/MauiProgram.cs`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.*`, `ImageGen/ImageGen.Avalonia.Tests/**`, `docs/knowledge/avalonia.md`, `docs/knowledge/maui.md`, `tasks/v0.1/016-skeleton-manual-check.md`, `tasks/backlog/046-desktop-manual-check.md`

## Отчёт

## Комментарии

### 2026-10-28, владелец (в чате, записал ревью)
План одобрен.
