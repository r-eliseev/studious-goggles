---
id: 212
status: done
assignee:
claimed:
created: 2026-11-07
---

# MAUI: компактная шапка чата на телефоне

## Описание
Отчёт владельца (v0.1.11, Android): кнопки «Models: 1 × 1», «Assistant: …» и «Parameters» занимают почти полэкрана (скриншоты: три строки высоких «чипов» над лентой). На пустой сессии строка «Assistant is thinking…» налезает на кнопку «Parameters».

Шапка — `FlexLayout x:Name="ChatHeader"` с `Wrap="Wrap"` в строке `Auto` сетки `ChatPage.xaml`; при переносе по строкам `FlexLayout` в `Auto`-строке `Grid` неверно сообщает высоту, и следующая строка (индикатор ассистента) накладывается.

Зависит от: 211 (тот же `ChatPage.xaml`; делать после её слияния).
Среда: Linux (тест разметки, сборка MAUI Android); проверка на телефоне — 140.

## Критерии готовности
- Шапка — одна строка: `ScrollView Orientation="Horizontal"` (без полосы прокрутки) с `HorizontalStackLayout Spacing="8"`; на телефоне её высота не больше одной кнопки-чипа.
- Чипы в узкой раскладке — компактный стиль: новый класс стиля `chip-compact` в `Resources/Styles/Styles.xaml` (высота около 32, отступы `12,4`, шрифт `IgFontSizeMeta` или ближайший малый из ресурсов — найти в темах), применяется вместе с `chip`, когда `IsWide = false`. В широкой раскладке (`IsWide`) вид прежний.
- Текст ассистента в чипе обрезается многоточием по ширине (уже `TailTruncation`) и не растягивает строку.
- Индикатор «Assistant is thinking…» не перекрывает шапку ни при пустой, ни при длинной ленте.
- Тест разметки: у шапки нет `FlexLayout` с `Wrap`; чипы внутри горизонтального `ScrollView`.
- Сборка MAUI Android без предупреждений; пункт 140 со скриншотом-ожиданием: шапка в одну строку, лента начинается сразу под ней.

## Анализ
`ChatPage.xaml` (строки около 352–390 на `07ff84a`): `Grid ChatLayout`, внутренняя сетка `RowDefinitions="Auto,*,Auto,Auto"`, шапка, `CollectionView Transcript`, индикатор (`Grid.Row="2"`), поле ввода. `IsWide` и переключение раскладки — `ChatPage.xaml.cs` (поиск `IsWide`). Стили `chip` — `Resources/Styles/Styles.xaml`. Как переключать класс стиля по `IsWide` — по образцу других мест страницы (`DataTrigger` или `VisualStateManager`), без кода, если возможно.

## План
1. Падающий тест разметки.
2. Шапка в `ScrollView` + `HorizontalStackLayout`, стиль `chip-compact`, переключение по `IsWide`.
3. Проверить строку индикатора: её `Grid.Row` и отступы.
4. Сборка Android, пункт 140.

Файлы: `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, `ImageGen/ImageGen.Maui/Resources/Styles/Styles.xaml`, `ImageGen/ImageGen.Presentation.Tests/Design/MauiChatPageMarkupTests.cs`, `tasks/backlog/140-android-manual-check.md`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
bash scripts/check.sh
```

### Порядок исполнения (для Luna)
- Сначала прочитай только файлы из строки «Файлы» и названные тесты; сигнатуры сверяй с текущим `main`.
- Шаги строго по порядку (ub-mode): падающий тест → запуск, убедиться, что он падает именно по описанной причине → минимальный фикс → тест зелёный.
- Не меняй файлы вне строки «Файлы», не переименовывай существующие члены сверх плана, не чини соседние находки — они в отдельных задачах.
- Если код расходится с планом (нет такого метода, другая сигнатура, тест уже есть), не импровизируй: остановись и запиши вопрос в «Комментарии».
- Команды `dotnet` выполняй по одной, не параллельно. После каждого изменённого проекта — `dotnet format <проект> --verify-no-changes`.
- В «Отчёте»: что изменено, команды и результаты, отличия от плана.

## Отчёт

Шапка заменена на горизонтальный `ScrollView` с одной строкой `HorizontalStackLayout`; полосы прокрутки скрыты, `Spacing=8`. Добавлен `chip-compact` для кнопок и Border, на узкой раскладке стиль задаёт высоту 32, отступы `12,4` и малый шрифт; широкая раскладка сохраняет прежние значения. `AssistantChipText` остаётся однострочным с `TailTruncation`; индикатор занимает отдельную строку Grid. В задачу 140 добавлена ручная проверка телефона.

Отличие от плана: SourceGen не позволяет менять `StyleClass` в `DataTrigger` (MAUIX2002). Поэтому пара классов `chip,chip-compact` задана постоянно, а новый класс активирует компактные setters только при `IsWide=False`; в широкой раскладке они не применяются.

Проверено: тест разметки сначала падал, затем прошёл; Presentation.Tests — 500/500; MAUI Android build — 0 предупреждений и ошибок; форматирование Presentation.Tests и MAUI прошло. `scripts/check.sh` запускается перед коммитом. Реальную геометрию и отсутствие наложения на устройстве проверяет задача 140.

## Комментарии

### 2026-11-07, claude-audit
Создано по отчёту владельца о сборке v0.1.11 на Android (MAUI, логи 2026-11-05 и 2026-11-06, скриншоты). План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
План одобрен ранее данным поручением «заапрувь все таски которые будут».
