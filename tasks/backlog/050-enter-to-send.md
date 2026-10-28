---
id: 050
status: done
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

### Остаток работы — только Android (уточнение 2026-10-28, claude-cloud-t9wd)
Avalonia и подсказка сделаны (см. «Отчёт»), MAUI Windows сделала задача 056 (`Controls/ChatEditor.cs`, `Platforms/Windows/ChatEditorKeyHandling.cs`, регистрация в `MauiProgram.cs` под `#if WINDOWS`). Шаги 2 и 3 (Windows) выше не выполнять. Осталось:
1. `ImageGen/ImageGen.Maui/Platforms/Android/ChatEditorImeHandling.cs` — `public static class ChatEditorImeHandling` по образцу `ChatEditorKeyHandling`: `Register()` → `EditorHandler.Mapper.AppendToMapping("ChatEditorImeSend", (handler, view) => ...)`; для `view is ChatEditor editor`: `handler.PlatformView.ImeOptions = Android.Views.InputMethods.ImeAction.Send`; `handler.PlatformView.SetRawInputType(Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextFlagCapSentences)` (без `TextFlagMultiLine`: иначе клавиатура показывает перевод строки вместо «Отправить»; длинный текст всё равно переносится визуально); подписка на `EditorAction`: при `ImeAction.Send` — `args.Handled = true`, затем `editor.SendCommand?.CanExecute(null) == true` → `Execute(null)`.
2. Аппаратный Enter на Android (клавиатура Bluetooth): в той же подписке `KeyPress` нативного `EditText` — `Keycode.Enter` без Shift при `KeyEventActions.Down` → то же, что п. 1; с Shift — не трогать.
3. `MauiProgram.cs`: `#if ANDROID` → `ImageGen.Maui.Platforms.Android.ChatEditorImeHandling.Register();` рядом с Windows-регистрацией.
4. Решение «перевод строки на экранной клавиатуре»: на Android его нет (поле однострочного ввода с переносом); записать в «Отчёт» и в `docs/knowledge/maui.md`.
5. Проверка: сборка Android (`-p:AndroidSdkDirectory=$ANDROID_HOME`), `dotnet format` MAUI, `scripts/check.sh`; ручные пункты — в 016 («Android: на экранной клавиатуре кнопка „Отправить“ отправляет сообщение; пустое поле не отправляется»).
6. После этого статус `done`.

Файлы (остаток): `ImageGen/ImageGen.Maui/Platforms/Android/ChatEditorImeHandling.cs` (новый), `ImageGen/ImageGen.Maui/MauiProgram.cs`, `docs/knowledge/maui.md`, `tasks/v0.1/016-skeleton-manual-check.md`

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

Сделано:
- Avalonia и подсказка (см. ниже; подробности в «Комментариях» 2026-10-28)
- Windows: задача 056 (`Controls/ChatEditor.cs`, `Platforms/Windows/ChatEditorKeyHandling.cs`).
- Android (эта волна): `Platforms/Android/ChatEditorImeHandling.cs` — `ImeAction.Send` и `SetRawInputType(ClassText | TextFlagCapSentences)` (без `TextFlagMultiLine`), `EditorAction` и `KeyPress` (Enter без Shift → отправка, Shift+Enter не трогаем), регистрация в `MauiProgram.cs` под `#if ANDROID`. Перевод строки на экранной клавиатуре Android не предусмотрен (поле однострочное для клавиатуры) — решение записано в `docs/knowledge/maui.md`.

Проверка: сборка `net10.0-android` (APK) проходит, `dotnet format --verify-no-changes` для MAUI — без изменений. Поведение на устройстве не проверено (нет эмулятора) — пункт 11 в задаче 016. Риск: однострочный ввод может не переносить длинный текст; если так, решение пересматривается по результатам 016.

Сделано:
- Avalonia (`ChatView.axaml`, `ChatView.axaml.cs`): обработчик Enter перенесён из XAML в туннельную фазу (`AddHandler(..., RoutingStrategies.Tunnel)` в конструкторе). Enter без Shift ставит `Handled` и отправляет, если отправка доступна; Shift+Enter не трогается, перевод строки остаётся за `TextBox`. Причина туннельной фазы — в `docs/knowledge/avalonia.md`.
- Подсказка в поле ввода (`AppResources.resx`, `ChatPlaceholder`): «Describe the image you want (Enter — send, Shift+Enter — new line)».
- Тесты `ChatKeyboardFlowTests` (headless, 3 сценария): Enter отправляет и не добавляет строку; Shift+Enter не обработан; Enter на пустом поле ничего не делает и не добавляет строку. Все 61 тест Avalonia проходят.

Не сделано (нужна работа на Windows, отсюда не собирается, WinUI-код нельзя проверить на Linux):
- MAUI на Windows: обработчик Enter для поля чата в `Platforms/Windows` (перехват `PreviewKeyDown` у нативного поля, отправка через команду `SendCommand`).
- MAUI на Android: действие клавиатуры «Отправить» у поля (в `Editor` нет `ReturnType`, нужна платформенная настройка), перевод строки на экранной клавиатуре — решение при реализации.
- Эти пункты проверяются вручную (задача 016) после реализации.

## Комментарии

### 2026-10-28, владелец (в чате, записал ревью)
План одобрен.

### 2026-10-28, ревью (claude, Windows)
Android-часть (действие «Отправить» у экранной клавиатуры) — не Windows-работа: цель Android собирается на Linux с Android SDK (`docs/knowledge/dev-environment.md`), её можно было сделать в той же сессии. Windows-часть (`Platforms/Windows`, `PreviewKeyDown` у WinUI `TextBox`) на Linux не собирается, но собирается в CI (задача `maui`) и на Windows-машине. Задача остаётся `approved`: оставшаяся работа — MAUI Windows и Android.

### 2026-10-28, ревью (claude)
Windows-часть (Enter в MAUI под Windows) выделена в задачу 056 по правилу `docs/tasks.md`. Здесь осталась Android-часть: действие «Отправить» у экранной клавиатуры (на Linux с Android SDK).
