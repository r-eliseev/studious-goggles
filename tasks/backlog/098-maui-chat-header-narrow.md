---
id: 098
status: approved
assignee:
claimed:
created: 2026-10-29
---

# MAUI: на узком экране заголовок чата скрывает кнопки

## Описание
Замечание владельца (2026-10-29, Android, скриншоты): в шапке чата длинное название сессии занимает всю ширину, чип «Models» обрезан у правого края, а чипы ассистента и «Parameters» не видны вовсе. Название к тому же повторяется: оно уже есть в верхней панели навигации.

Что известно: шапка — `HorizontalStackLayout` с названием и тремя чипами (`ImageGen.Maui/Pages/ChatPage.xaml`, «Header: title and the three chips»); он не переносит и не сжимает содержимое. Как сделать на узком экране (убрать дубль названия, перенос чипов на вторую строку, `FlexLayout` с `Wrap`, перенос действий в панель навигации), решить в «Анализе»; широкую раскладку (Windows, планшет) не ломать. Avalonia сверить: там такой же горизонтальный `StackPanel`.

Зависит от: 097 (общие обработчики и файл тестов разметки; 095 входит в зависимости 097).
Среда: Linux или Windows (Android; Windows-цель — CI и ручная проверка в 016).

## Критерии готовности
- На экране шириной 360 dp все три чипа видны при любой длине названия сессии; кнопки Models и Parameters нажимаются, чип ассистента остаётся информационным.
- Длинное название обрезается многоточием и не выталкивает кнопки; дубля названия на узком экране нет (или он обоснован в «Анализе»).
- Широкая раскладка не изменилась.

## Анализ
- **MAUI сейчас.** Шапка — `HorizontalStackLayout` (`HorizontalOptions="Start"`): `Label` названия, `Button` «Models» (`ModelsButtonText`), `Border` с `Label` ассистента (`AssistantText`, надпись, а не кнопка), `Button` «Parameters». Стековая раскладка не ограничивает ширину детей: длинное название не обрезается, всё лишнее уходит за правый край. Название уже показывает панель навигации `Shell` (`ContentPage Title="{Binding Title}"`), отсюда дубль. `ChatPage.ApplyWidth` по порогу 840 ставит `ChatPageModel.IsWide`.
- **Avalonia.** Та же шапка на горизонтальном `StackPanel`, та же проблема в узком окне (у окна нет `MinWidth`). Дубля нет: заголовок окна постоянный (`AppTitle`), поэтому название в шапке Avalonia остаётся.
- **Решения (приняты владельцем).** Шапка — раскладка с переносом: MAUI `FlexLayout` (`Wrap`), Avalonia `WrapPanel`. Название в одну строку с многоточием, не шире шапки; чипы переносятся на следующую строку и не уходят за край. В MAUI на узкой раскладке (`IsWide == false`) название скрыто — его показывает панель навигации. Ширина названия и надписи ассистента ограничивается из кода по событию `SizeChanged` шапки (одинаково в обеих оболочках; не полагаемся на то, как `FlexLayout` меряет `Label`). Зазор 8 — полями детей `0,0,8,8` и полем шапки `0,0,0,-8`, чтобы высота однострочной шапки не изменилась.
- **Почему так выполняются критерии.** 360 dp минус отступы 2×16 = 328 dp; три чипа в одну строку не помещаются и переносятся на две, все видны. При обычном названии на широком экране всё в одну строку слева, как сейчас.
- **TDD.** Новой логики в `Presentation` нет (`IsWide` уже покрыт тестами). Раскладку проверяют headless-тесты Avalonia, разметку MAUI — текстовые тесты, как `MauiStylesTests`.

## План
Выполнять после: 095 и 097 (правят `ChatPage.xaml`, `ChatPage.xaml.cs`, `ChatView.axaml.cs`; 097 создаёт `MauiChatPageMarkupTests.cs`, сюда добавляются тесты). Пересечения только текстовые: `ChatPage.xaml(.cs)` — 088, 090; при rebase сохранять обе стороны.

Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов, членов, ключей строк и тестов переноси дословно. При расхождении с текущим API адаптируй имена и разметку в рамках контракта, исправь ошибку минимально и проверь сборкой. `blocked` нужен для неизвестного продуктового решения или недоступной обязательной среды, а не для рутинной ошибки компиляции.

Прочитай перед началом:
- `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`: блок под комментарием `<!-- Header: title and the three chips. -->`; `ChatPage.xaml.cs` (`ApplyWidth`, конструктор);
- `ImageGen/ImageGen.Avalonia/Views/ChatView.axaml`: блок под комментарием `<!-- Header: title and the three chips (Models, Assistant, Parameters). -->`; `ChatView.axaml.cs` (конструктор, `ApplyWidth`);
- `ImageGen/ImageGen.Avalonia.Tests/Views/ChatLayoutTests.cs` (метод `OpenChatAsync`);
- `ImageGen/ImageGen.Presentation.Tests/Design/MauiChatPageMarkupTests.cs` (создан в 097; если файла нет — создать класс `MauiChatPageMarkupTests` с методом `MauiRoot()`, скопированным из `MauiStylesTests.cs`);
- `docs/design/implementation.md`, раздел «## 6. Ширина окна».

1. Avalonia, падающие тесты в `ImageGen/ImageGen.Avalonia.Tests/Views/ChatLayoutTests.cs`.
   - Метод `OpenChatAsync(double width)` получает второй параметр `string? title = null`: если `title` не `null`, после создания сессии вызвать `SessionService.RenameAsync(session.Id, title, TestContext.Current.CancellationToken)` до перехода в чат. После перехода дождаться загрузки: `shell.WaitUntilAsync(() => model.Title == (title ?? model.Title) && model.ModelsButtonText.Length > 0, "шапка загружена")`, где `model` — `ChatPageModel` из `DataContext` чата; затем `shell.Window.UpdateLayout()`.
   - Длинное название в тестах: `new string('W', Session.MaxTitleLength)`.
   - Элементы: шапка `chat.FindControl<WrapPanel>("ChatHeader")`, название `chat.FindControl<TextBlock>("ChatTitle")`, надпись ассистента `chat.FindControl<TextBlock>("AssistantChipText")`; кнопки «Models» и «Parameters» — потомки шапки типа `Button` с `AutomationProperties.GetName(b)` равным `model.ModelsButtonText` и `AppResources.ChatParametersButton`.
   - `NarrowChat_LongTitle_AllChipsInside` — ширина 360, длинное название → у каждого из трёх чипов (`Button` Models, внешний `Border` над `AssistantChipText`, `Button` Parameters) `IsEffectivelyVisible == true` и правый край `c.TranslatePoint(new Point(c.Bounds.Width, 0), chat)!.Value.X` ≤ `chat.Bounds.Width + 0.5`.
   - `NarrowChat_LongAssistant_AllChipsInside`: 360 dp и длинная модель ассистента; проверять bounds внешнего `Border` чипа, а не только текста, включая левую/правую/нижнюю границы.
   - `NarrowChat_LongTitle_TitleTrimmed` — ширина 360, длинное название → `ChatTitle.IsEffectivelyVisible == true` и `ChatTitle.Bounds.Width` ≤ `ChatHeader.Bounds.Width`.
   - `WideChat_ShortTitle_HeaderInOneRow` — ширина 1100, название `"Fox"` → вертикальные центры (`TranslatePoint(new Point(0, c.Bounds.Height / 2), chat)!.Value.Y`) названия и трёх чипов отличаются не больше чем на 1; левый край каждого чипа правее правого края названия.
   Сейчас падают все три (нет элементов с этими именами).
2. Avalonia `ImageGen/ImageGen.Avalonia/Views/ChatView.axaml`, блок шапки:
   - `StackPanel Orientation="Horizontal" Spacing="8"` заменить на `WrapPanel` с `x:Name="ChatHeader"`, `Orientation="Horizontal"`, `Margin="0,0,0,-8"`; `HorizontalAlignment` не задавать (по умолчанию растягивается на колонку);
   - каждому из четырёх прямых детей (название, кнопка Models, `Border` ассистента, кнопка Parameters) — `Margin="0,0,8,8"`;
   - `TextBlock` названия: `x:Name="ChatTitle"`, `TextTrimming="CharacterEllipsis"`, `TextWrapping="NoWrap"`; привязка `Text="{Binding Title}"` и класс `title` прежние;
   - `TextBlock` внутри `Border` ассистента: `x:Name="AssistantChipText"`, `TextTrimming="CharacterEllipsis"`, `TextWrapping="NoWrap"`; остальное прежнее.
3. Avalonia `ImageGen/ImageGen.Avalonia/Views/ChatView.axaml.cs`: в конструкторе подписать `SizeChanged` у `this.FindControl<WrapPanel>("ChatHeader")` на новый метод `private void OnHeaderSizeChanged(object? sender, SizeChangedEventArgs e)`: `ChatTitle.MaxWidth = Math.Max(0, e.NewSize.Width - 8)`, `AssistantChipText.MaxWidth = Math.Max(0, e.NewSize.Width - 40)` (8 — поле справа, 40 — поле, отступы и рамка чипа). Тесты шага 1 проходят; прогнать все `ImageGen.Avalonia.Tests`.
4. MAUI, падающие текстовые тесты в `ImageGen/ImageGen.Presentation.Tests/Design/MauiChatPageMarkupTests.cs` (XML через `XDocument`, `x:Name` — атрибут в пространстве `http://schemas.microsoft.com/winfx/2009/xaml`, файл `Pages/ChatPage.xaml`):
   - `ChatHeader_IsWrappingFlexLayout` — есть элемент `FlexLayout` с `x:Name="ChatHeader"`, `Wrap="Wrap"`, `Direction="Row"`, `AlignItems="Center"`, без атрибута `HorizontalOptions`; в файле нет `HorizontalStackLayout`, у которого первый дочерний элемент — `Label` с `Text="{Binding Title}"`;
   - `ChatTitle_HiddenOnNarrowAndTruncated` — `Label` с `x:Name="ChatTitle"` имеет `Text="{Binding Title}"`, `IsVisible="{Binding IsWide}"`, `MaxLines="1"`, `LineBreakMode="TailTruncation"`;
   - `AssistantChip_Truncated` — `Label` с `x:Name="AssistantChipText"` имеет `Text="{Binding AssistantText}"`, `MaxLines="1"`, `LineBreakMode="TailTruncation"`.
5. MAUI `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, блок под `<!-- Header: title and the three chips. -->`:
   - `HorizontalStackLayout Spacing="8" HorizontalOptions="Start"` заменить на `FlexLayout` с `x:Name="ChatHeader"`, `Wrap="Wrap"`, `Direction="Row"`, `AlignItems="Center"`, `JustifyContent="Start"`, `AlignContent="Start"`, `Margin="0,0,0,-8"`, без `HorizontalOptions`;
   - каждому из четырёх прямых детей — `Margin="0,0,8,8"`;
   - `Label` названия: добавить `x:Name="ChatTitle"`, `IsVisible="{Binding IsWide}"`, `MaxLines="1"`, `LineBreakMode="TailTruncation"`; `StyleClass`, `Text`, `VerticalOptions` прежние;
   - `Label` внутри `Border StyleClass="chip"`: добавить `x:Name="AssistantChipText"`, `MaxLines="1"`, `LineBreakMode="TailTruncation"`; остальное прежнее, `SemanticProperties` не трогать.
   Тесты шага 4 проходят.
6. MAUI `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml.cs`: в конструкторе подписать `ChatHeader.SizeChanged` на `private void OnHeaderSizeChanged(object? sender, EventArgs e)`: если `ChatHeader.Width <= 0` — выйти; `ChatTitle.MaximumWidthRequest = Math.Max(0, ChatHeader.Width - 8)`, `AssistantChipText.MaximumWidthRequest = Math.Max(0, ChatHeader.Width - 40)`. Сборка Android и формат (см. «Проверку»). Если имя свойства отличается в текущей версии MAUI, сверить API и исправить минимально; работа не останавливается на синтаксической ошибке XAML.
7. Документация:
   - `docs/design/implementation.md`, раздел «## 6. Ширина окна», после пункта «Узкий — < 840…» — пункт: «Шапка чата: название в одну строку с многоточием, чипы переносятся на следующую строку; на узком экране MAUI название в шапке скрыто — его показывает панель навигации (задача 098). В Avalonia название остаётся всегда.»
   - `docs/knowledge/maui.md`, раздел «Разметка и списки»: пункт «**Перенос чипов (задача 098):**» — у `FlexLayout` нет `Spacing`, зазор полями детей и отрицательным полем контейнера; ширина `Label` ограничивается `MaximumWidthRequest` из `SizeChanged` контейнера; проверено ли на устройстве.

Завершение: заполни «Отчёт» с изменениями, командами и результатами проверок, передай ручные сценарии в 016 (Windows), 046 (Avalonia), 140 (Android) по применимости. Выполни `scripts/release-task.sh 098`, затем `bash scripts/check.sh`. Недоступная ручная проверка не заменяет сборку и не оставляет готовую задачу в `in-progress`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
bash scripts/check.sh
```
Android-сборка и формат изменённого MAUI-проекта обязательны в среде с Android SDK; при отсутствии SDK подготовить инструменты по `docs/knowledge/`. Windows-цель собирает CI. Отсутствие телефона не блокирует завершение: ручные сценарии в 140.

### Ручная проверка
В задачу 140 (Android), телефон 360–412 dp, портрет и альбом; в «Отчёт» — результат или причина отсутствия проверки:
1. Сессия с коротким и с 40-символьным названием (переименовать в списке) → названия в шапке нет, оно в панели навигации; три чипа видны; кнопки «Models» и «Parameters» нажимаются и открывают свои экраны, ассистент остаётся информационным чипом.
2. Длинный id модели ассистента → чип обрезан многоточием, не за краем.
3. Планшет или ширина ≥ 840 → название слева и чипы в одну строку, как раньше.
MAUI под Windows — дописать в `tasks/v0.1/016-skeleton-manual-check.md`: широкое окно — шапка как раньше; длинное название обрезается, чипы на второй строке. Avalonia — дописать в `tasks/backlog/046-desktop-manual-check.md`: сузить окно до ~400 → чипы переносятся, название с многоточием.

Файлы: `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml.cs`, `ImageGen/ImageGen.Avalonia/Views/ChatView.axaml`, `ImageGen/ImageGen.Avalonia/Views/ChatView.axaml.cs`, `ImageGen/ImageGen.Avalonia.Tests/Views/ChatLayoutTests.cs`, `ImageGen/ImageGen.Presentation.Tests/Design/MauiChatPageMarkupTests.cs`, `docs/design/implementation.md`, `docs/knowledge/maui.md`, `tasks/backlog/140-*.md`, `tasks/v0.1/016-skeleton-manual-check.md`, `tasks/backlog/046-desktop-manual-check.md`.

## Отчёт

## Комментарии
### 2026-10-29, claude-plan
- Вопрос владельцу: на узком экране MAUI название сессии убираем из шапки (оно есть в панели навигации), а чипы переносим на вторую строку. Подходит ли это или нужно оставить название в шапке? Допущение в плане: убираем.
- Допущение: в Avalonia делаем тот же перенос и обрезание (в узком окне проблема та же), но название не скрываем: другого места, где оно видно, в Avalonia нет.

### 2026-10-29, владелец (в чате, записал claude-plan)
План одобрен; допущения из комментариев claude-plan приняты как решения. План переписан под небольшую модель.
