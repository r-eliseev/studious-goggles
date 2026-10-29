---
id: 073
status: in-progress
assignee: claude-536f
claimed: 2026-10-29T06:08:44Z
created: 2026-10-28
---

# Десктоп: строка меню и сочетания клавиш

## Описание
По `docs/design/research.md` («Соглашения платформ») и решению владельца: строка меню на macOS (в MAUI — и на Windows через `MenuBarItem`), сочетания клавиш на всех десктопах. Основные: `Cmd/Ctrl+N` — новая сессия, `Cmd/Ctrl+,` — настройки, `Cmd/Ctrl+F` — поиск сессий. Для карточки генерации в фокусе: `R` — Repeat, `Shift+R` — New seed, `E` — Edit prompt, `Cmd/Ctrl+S` — Save image, `Cmd/Ctrl+C` — Copy prompt. На macOS модификатор — Cmd, на Windows и Linux — Ctrl.

Справочник: `docs/design/implementation.md`; макеты: `docs/design/prototype.html` (https://claude.ai/artifact/M9QzPf39KLkRFTnYBtCi6F); решения владельца — задача 074 и «Комментарии» этой задачи.

Зависит от: 065 (настройки), 069 (действия карточки), 076 (Copy prompt), 079 (поле поиска), 080 (Save image).
Среда: Linux или Windows (Avalonia — headless-тесты на Linux; MAUI — сборка Android на Linux с Android SDK, Windows-цель собирает CI; ручные проверки — 016 и 046). Строка меню macOS проверяется только на macOS — нет машины, помечать «не проверено» (`docs/status.md`).

## Критерии готовности
- Все сочетания из «Описания» работают в Avalonia; в MAUI на Windows и Mac — через `KeyboardAccelerators` у пунктов меню.
- Сочетания карточки действуют только когда карточка в фокусе клавиатуры и не перехватывают ввод в поле текста (`R` в поле ввода печатает букву).
- Пункты меню и подсказки (`ToolTip`) показывают сочетание.
- Тесты из «Плана» проходят.

## Анализ
- Модификатор по ОС — одна функция, иначе разметка расползётся: `PlatformKeys.Primary` (Avalonia: `KeyModifiers.Meta` на macOS, `KeyModifiers.Control` иначе).
- Avalonia: `NativeMenu.Menu` на `App` (меню приложения на macOS) и на `MainWindow` (File, Edit, View, Window, Help); на Windows и Linux `NativeMenu` без `NativeMenuBar` ничего не показывает — там только `Window.KeyBindings`. Сочетания карточки — `KeyBindings` на корневом элементе шаблона карточки (`Focusable=true`).
- MAUI: `ContentPage.MenuBarItems` (`MenuBarItem` → `MenuFlyoutItem` с `KeyboardAccelerators`), показывается на Windows и Mac Catalyst; на телефонах не показывается.
- Команды уже есть: `SessionsPageModel.NewSessionCommand`, `OpenSettingsCommand` (065), команды карточки (`RepeatCommand`, `NewSeedCommand`, `EditCommand`, `SaveCommand` — 080, `CopyPromptCommand` — 076). Фокус на поиск — событие view (фокус поля `SearchBox` из 079).

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов, ключей и строк переноси дословно. Если шаг не получается так, как написано (API другое, компилятор не принимает разметку), не придумывай обход: опиши в «Комментариях», что пробовал, поставь `blocked` и остановись.

1. Avalonia `Services/PlatformKeys.cs`: `static KeyModifiers Primary` и `static KeyModifiers For(bool isMacOs)` (для тестов); `static string Display(Key key, bool shift)` → «⌘N» / «Ctrl+N».
2. `MainWindow`: `KeyBindings` для `Primary+N` (`NewSessionCommand` синглтона `SessionsPageModel`), `Primary+OemComma` (`OpenSettingsCommand`; если 065 уже добавил — перевести на `PlatformKeys`), `Primary+F` (фокус поля поиска: метод `SessionsListView.FocusSearch()`; если список не виден на узком окне — сначала переход на `Routes.Sessions`).
3. `App.axaml` и `MainWindow`: `NativeMenu` — ImageGen (About, Settings… с `Gesture` `Cmd+,`), File (New Session `Cmd+N`), Edit (стандартные Copy/Paste не трогаем), Window, Help. Пункты привязаны к тем же командам.
4. Шаблон карточки в `ChatView.axaml`: корневой элемент `Focusable=true`, видимая рамка фокуса (`:focus-visible`, стиль 063), `KeyBindings`: `R`, `Shift+R`, `E`, `Primary+S`, `Primary+C` → команды карточки. Подсказки кнопок-значков (`ToolTip.Tip`) дополнить сочетанием (`Repeat (R)`).
5. MAUI: `MenuBarItems` на `SessionsPage` и `ChatPage` (File: New Session, Settings; на `ChatPage` — Generation: Repeat, New seed, Edit, Save, Copy prompt — действуют на последнюю выбранную карточку: `ChatPageModel.SelectedCard`, выставляется по нажатию на карточку). `KeyboardAccelerators` с `Modifiers="Cmd"` для Mac Catalyst и `Ctrl` для Windows (`OnPlatform`).
6. Тесты Avalonia `Views/KeyboardShortcutsTests.cs`: `CtrlN_CreatesSession`, `CtrlComma_OpensSettings`, `CtrlF_FocusesSearch`, `R_OnFocusedCard_Repeats`, `R_InInputBox_TypesLetter`, `PlatformKeys_Mac_UsesMeta`. Presentation: тест `SelectedCard_IsSetOnTap` (если добавлен `SelectedCard`).
7. `docs/knowledge/avalonia.md` и `maui.md` — что подтвердилось про `NativeMenu` и `MenuBarItems`; `docs/specs/chat-sessions.md` — раздел «Клавиатура» с таблицей сочетаний.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android -p:AndroidSdkDirectory=$ANDROID_HOME
dotnet format ImageGen/<каждый изменённый проект>/<проект>.csproj --verify-no-changes
scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Avalonia/{Services/PlatformKeys.cs,App.axaml,Views/MainWindow.*,Views/ChatView.axaml,Views/SessionsListView.*}`, `ImageGen/ImageGen.Maui/Pages/{SessionsPage,ChatPage}.xaml`, `ImageGen/ImageGen.Presentation/PageModels/ChatPageModel.cs` (только `SelectedCard`), тесты, `docs/knowledge/*`, `docs/specs/chat-sessions.md`

## Отчёт

## Комментарии

### 2026-10-28, владелец (в чате, записал claude-cloud-t9wd)
Сочетания: «Основные + карточка» (Cmd/Ctrl+N, Cmd/Ctrl+, , Cmd/Ctrl+F; R, Shift+R, E, Cmd/Ctrl+S, Cmd/Ctrl+C), строка меню macOS с теми же пунктами.
