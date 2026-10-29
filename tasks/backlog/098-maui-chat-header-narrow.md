---
id: 098
status: planned
assignee:
claimed:
created: 2026-10-29
---

# MAUI: на узком экране заголовок чата скрывает кнопки

## Описание
Замечание владельца (2026-10-29, Android, скриншоты): в шапке чата длинное название сессии занимает всю ширину, чип «Models» обрезан у правого края, а чипы ассистента и «Parameters» не видны вовсе. Название к тому же повторяется: оно уже есть в верхней панели навигации.

Что известно: шапка — `HorizontalStackLayout` с названием и тремя чипами (`ImageGen.Maui/Pages/ChatPage.xaml`, «Header: title and the three chips»); он не переносит и не сжимает содержимое. Как сделать на узком экране (убрать дубль названия, перенос чипов на вторую строку, `FlexLayout` с `Wrap`, перенос действий в панель навигации), решить в «Анализе»; широкую раскладку (Windows, планшет) не ломать. Avalonia сверить: там такой же горизонтальный `StackPanel`.

Зависит от: —
Среда: Linux или Windows (Android; Windows-цель — CI и ручная проверка в 016).

## Критерии готовности
- На экране шириной 360 dp все три чипа видны и нажимаются при любой длине названия сессии.
- Длинное название обрезается многоточием и не выталкивает кнопки; дубля названия на узком экране нет (или он обоснован в «Анализе»).
- Широкая раскладка не изменилась.

## Анализ
- **MAUI сейчас.** Шапка — `HorizontalStackLayout` (`HorizontalOptions="Start"`) из `Label` названия и трёх чипов: `Button` «Models» (`ModelsButtonText`), `Border` с `Label` ассистента (`AssistantText`, это надпись, а не кнопка) и `Button` «Parameters». Стековая раскладка меряет детей без ограничения по ширине, поэтому длинное название не обрезается, а всё, что не влезло, уходит за правый край. Название страницы (`ContentPage Title="{Binding Title}"`) уже показывает панель навигации `Shell` (`AppShell`, `FlyoutBehavior="Disabled"`), отсюда дубль на телефоне. Узкая и широкая раскладки уже различаются: `ChatPage.ApplyWidth` по порогу 840 ставит `ChatPageModel.IsWide`.
- **Avalonia.** Та же шапка — горизонтальный `StackPanel`, та же проблема в узком окне (у окна нет `MinWidth`). Дубля в Avalonia нет: у `MainWindow` заголовок постоянный (`AppTitle`), панели навигации с названием сессии нет, поэтому название в шапке — единственное место, где оно видно, и его нельзя прятать. `IsWide` выставляет `ChatView.ApplyWidth` по тому же порогу.
- **Варианты.** (1) Перенести чипы в `ToolbarItems` панели навигации: на Android лишнее уходит в меню «⋮», а чип ассистента — надпись, не действие. Отклонено. (2) Сетка «название `*` | чипы `Auto`»: чипы прижимаются к правому краю, и широкая раскладка заметно меняется; на 360 dp три чипа всё равно не влезают в одну строку. Отклонено. (3) **Выбрано:** шапка — раскладка с переносом (MAUI `FlexLayout` с `Wrap`, `AlignItems=Center`; Avalonia `WrapPanel`). Название в одну строку с многоточием, не шире шапки; чипы не сжимаются. Если места не хватает, чипы переносятся на следующую строку и не уходят за край. В MAUI на узкой раскладке название скрыто (`IsVisible` привязан к `IsWide`): его показывает панель навигации.
- **Как это выполняет критерии.** 360 dp минус отступы страницы 2×16 = 328 dp. Чипы «N models × M», «Assistant: <id модели>» и «Parameters» в одну строку не помещаются, поэтому переносятся на две; все видны и нажимаются (высота чипа 28 по стилю `chip`, цель касания у `Button` по стилю — не трогаем). Очень длинный id модели в чипе ассистента обрезается многоточием по ширине шапки. Широкая раскладка с обычным названием выглядит как сейчас: всё в одну строку слева. Меняется только случай, когда название не помещается: тогда оно обрезается, а чипы уходят на вторую строку, а не за край.
- **Неизвестно (проверка на устройстве, `poc/` не нужен):** обрезает ли MAUI `FlexLayout` `Label` с `LineBreakMode=TailTruncation` по ширине шапки. Известно, что `FlexLayout` может мерить детей без ограничения. Запасной вариант: в `ChatPage.ApplyWidth` задавать названию и чипу ассистента `MaximumWidthRequest` по ширине колонки. В `FlexLayout` нет `Spacing`, поэтому зазор 8 даётся полями (`Margin`) детей. Показывает ли панель навигации `Shell` название на Windows, тоже неизвестно; это не важно, потому что на широкой раскладке название остаётся.
- **TDD.** Новой логики в `Presentation` нет: `IsWide` уже есть и покрыт тестами `ChatPageModelTests`. Раскладку проверяют headless-тесты Avalonia. MAUI проверяется сборкой и вручную.
- **Пересечения.** 095 (корень `ChatPage.xaml`, стили), 097 (лента в `ChatPage.xaml`, `ChatView.axaml.cs`, `ChatPage.xaml.cs`), 088 (действия карточки в `ChatPage.xaml`). Здесь меняется только блок «Header» в `ChatPage.xaml` и `ChatView.axaml` (и, возможно, `ApplyWidth`). Порядок: **095 → 097 → 098**; при конфликтах в `ChatPage.xaml.cs` сохранить обе правки.

## План
TDD для Avalonia: сначала падающий headless-тест, затем разметка.

1. Avalonia, тесты (`ImageGen.Avalonia.Tests/Views/ChatLayoutTests`, на `OpenChatAsync`). Сессия переименована через `SessionService.RenameAsync` в длинное название (до `Session.MaxTitleLength`):
   - окно 400 → три чипа (кнопки «Models», «Parameters» и надпись ассистента) видимы, и их границы (`TranslatePoint`/`Bounds` относительно `ChatView`) целиком внутри ширины представления;
   - окно 400 → `TextBlock` названия не шире шапки (текст обрезан);
   - окно 1100, короткое название → название и три чипа на одной строке (одинаковый центр по вертикали), чипы правее названия (широкая раскладка не изменилась).
   Найти элементы проще с именами: `x:Name` у шапки и названия (`ChatHeader`, `ChatTitle`).
2. Avalonia `ChatView.axaml`: горизонтальный `StackPanel` шапки заменить на `WrapPanel` (зазор 8 по горизонтали и вертикали); у названия `TextTrimming` с многоточием и `MaxWidth`, равная ширине шапки (привязка к ширине `ChatHeader` или установка в `ApplyWidth`); у текста чипа ассистента — то же обрезание. Название в Avalonia не скрывается.
3. MAUI `ChatPage.xaml`: шапка — `FlexLayout` (`Wrap`, `Direction=Row`, `AlignItems=Center`, `JustifyContent=Start`), зазоры через `Margin`. Название: `MaxLines=1`, `LineBreakMode=TailTruncation`, `IsVisible="{Binding IsWide}"`; надпись в чипе ассистента обрезается так же. `SemanticProperties` прежние; на узкой раскладке название озвучивает панель навигации.
4. Если на устройстве обрезание в `FlexLayout` не работает: в `ChatPage.ApplyWidth` ставить названию и чипу ассистента `MaximumWidthRequest` = ширина колонки чата минус отступы. Это код оболочки, без новой логики в модели.
5. Ручная проверка на Android (телефон 360–412 dp, портрет и альбом): короткое и максимально длинное название (переименовать в списке сессий) → названия в шапке нет, оно в панели навигации с многоточием; три чипа видны и нажимаются: «Models» и «Parameters» открывают свои экраны. Длинный id модели ассистента → чип обрезан, не за краем. Планшет или окно ≥ 840 → название слева и чипы в строку, как раньше. Windows (CI сборка, ручная — 016): широкая раскладка без изменений. Avalonia: сузить окно до ~400 → чипы переносятся, название с многоточием. Если устройства или рабочего стола нет — пункты в `tasks/v0.1/016-skeleton-manual-check.md` и `tasks/backlog/046-desktop-manual-check.md`.
6. Документация: `docs/design/implementation.md` — строка про шапку чата на узком экране (название только в панели навигации MAUI, чипы с переносом), если там описана шапка; иначе `docs/specs/chat-sessions.md` не трогать. `docs/knowledge/maui.md` — что выяснилось про `FlexLayout` и обрезание `Label`.

Проверка:
- `dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj`
- `dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj` (регрессия)
- `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android` (если есть Android SDK); Windows-цель — CI
- `dotnet format --verify-no-changes` для `ImageGen.Avalonia`, `ImageGen.Avalonia.Tests`, `ImageGen.Maui` (последний — при Android SDK)
- `scripts/check.sh`

Файлы: `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, при необходимости `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml.cs`; `ImageGen/ImageGen.Avalonia/Views/ChatView.axaml`, при необходимости `ImageGen/ImageGen.Avalonia/Views/ChatView.axaml.cs`; `ImageGen/ImageGen.Avalonia.Tests/Views/ChatLayoutTests.cs`; `docs/knowledge/maui.md`, при необходимости `docs/design/implementation.md`, `tasks/v0.1/016-skeleton-manual-check.md`, `tasks/backlog/046-desktop-manual-check.md`.

## Отчёт

## Комментарии
### 2026-10-29, claude-plan
- Вопрос владельцу: на узком экране MAUI название сессии убираем из шапки (оно есть в панели навигации), а чипы переносим на вторую строку. Подходит ли это или нужно оставить название в шапке? Допущение в плане: убираем.
- Допущение: в Avalonia делаем тот же перенос и обрезание (в узком окне проблема та же), но название не скрываем: другого места, где оно видно, в Avalonia нет.
