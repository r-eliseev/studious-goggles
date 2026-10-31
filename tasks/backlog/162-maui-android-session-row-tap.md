---
id: 162
status: planned
assignee:
claimed:
created: 2026-10-31
---

# MAUI Android: нажатие на сессию в списке открывает кнопки вместо сессии

## Описание
Наблюдение владельца 2026-10-31 (MAUI Android, релиз v0.1.5): в списке сессий нажатие на сессию работает плохо — появляются кнопки удаления и вторая (переименование), а сессия не открывается.

Зависит от: —
Среда: Linux или Windows с Android SDK (сборка); проверка — на устройстве владельца.

## Критерии готовности
- На Android короткое нажатие на строку сессии открывает сессию с первого раза.
- Переименование и удаление доступны отдельным действием (свайп влево по строке или явная кнопка) и не появляются от обычного нажатия.
- Поведение на Windows и Mac Catalyst (контекстное меню, кнопка удаления) не меняется; доступность: у строки есть описание для TalkBack, у действий — свои описания.
- Проверено владельцем на устройстве; ручной пункт в 140.

## Анализ
- Строка сессии в MAUI (`ImageGen.Maui/Controls/SessionsListView.xaml`, шаблоны `Today`, `Earlier`): `SwipeView` с `RightItems` (Rename, Delete) → `Grid` с `FlyoutBase.ContextFlyout` (Rename, Delete) → `Button` «ghost» на всю ширину с `OpenCommand` и подпись. Кнопка удаления справа видна только на десктопе (`OnIdiom Desktop=True`).
- «Кнопки удаления и вторая» — это ровно `SwipeItems` Rename и Delete. Значит, на Android `SwipeView` воспринимает касание с небольшим горизонтальным смещением как начало свайпа и открывает действия, а `Button` внутри не получает клик. Известная особенность `SwipeView` в MAUI на Android: дочерний `Button` конкурирует со свайпом за жест; повторное нажатие закрывает раскрытые действия, а не открывает сессию.
- `ContextFlyout` на Android не показывается (это меню Windows/Mac), но длинное нажатие не должно мешать — проверить у владельца.
- Тот же шаблон дублирован для `Today` и `Earlier`; результаты поиска без `SwipeView` — там открытие должно работать (проверить у владельца для сравнения: если в поиске нажатие работает, гипотеза подтверждена).
- Avalonia: свой список (`Views/SessionsListView.axaml`), не затронут.

## План
1. Открытие сессии — жест всей строки, а не `Button` внутри `SwipeView`: `TapGestureRecognizer` с `OpenCommand` на содержимом строки (`Grid`), подпись и мета — обычные `Label`; строка получает `SemanticProperties.Description` = название и подсказку «открыть». На десктопе кнопка удаления справа остаётся.
2. `SwipeView`: оставить `RightItems` для Rename/Delete, добавить `Threshold`, чтобы мелкое смещение пальца при нажатии не раскрывало действия (значение подобрать с владельцем; начальное — около ширины одного действия), и `SwipeItems.SwipeBehaviorOnInvoked="Close"`.
3. Убрать дублирование: общий `DataTemplate` строки в ресурсах `SessionsListView` для `Today` и `Earlier` (одинаковые сейчас), чтобы правка не расходилась.
4. Высота строки — не меньше `IgControlHeight` (Android 48).
5. Если после шагов 1–2 нажатие всё ещё раскрывает действия — альтернатива по согласованию: убрать `SwipeView` на Android и показывать Rename/Delete кнопкой «⋯» в строке (как карточка генерации).
6. Документы: `docs/knowledge/maui.md` — факт про `Button` внутри `SwipeView` на Android после подтверждения; пункт в 140.

Тест-кейсы (ручные у владельца): короткое нажатие открывает сессию в `Today` и `Earlier`; свайп влево показывает Rename/Delete, они работают; нажатие при раскрытых действиях закрывает их; TalkBack читает название; Windows — контекстное меню и кнопка удаления (CI-сборка, 016). Автоматическая проверка: разметочный тест в `ImageGen.Presentation.Tests/Design` (как `MauiProviderKeysMarkupTests`): в шаблоне строки нет `Button` с `OpenCommand` внутри `SwipeView`, есть жест с `OpenCommand`.

Проверка: `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android`, `dotnet format` MAUI (с Android SDK), тесты Presentation, `scripts/check.sh`; Windows — CI.

Файлы: `ImageGen/ImageGen.Maui/Controls/SessionsListView.xaml`, `ImageGen/ImageGen.Presentation.Tests/Design/MauiSessionsListMarkupTests.cs` (новый), `docs/knowledge/maui.md`, `tasks/backlog/140-android-manual-check.md`.

## Отчёт

## Комментарии

### 2026-10-31, claude-audit
Создано по отчёту владельца. Анализ и план — ниже, статус planned.
