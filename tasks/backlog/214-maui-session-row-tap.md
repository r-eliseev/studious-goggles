---
id: 214
status: in-progress
assignee: codex-gpt6
claimed: 2026-11-07T07:51:03Z
created: 2026-11-07
---

# MAUI: нажатие на сессию открывает её, а не кнопки Rename/Delete

## Описание
Отчёт владельца (v0.1.11, Android): «до сих пор не пофикшен клик на сессии, он всё ещё показывает кнопки». Задача 162 сделала нажатием всю строку (`TapGestureRecognizer` на `Grid` внутри `SwipeView Threshold="96"`), но на Android нажатие по-прежнему открывает правые элементы `SwipeView` (Rename, Delete) вместо сессии.

Решение без попыток настроить `SwipeView`: на телефонах строка сессии без `SwipeView`; нажатие открывает сессию, долгое нажатие — лист действий (Rename, Delete). На десктопе (`DeviceIdiom.Desktop`) — как сейчас (нажатие, контекстное меню, кнопка удаления).

Зависит от: 211 (способ задать контекст поведения и запись в `docs/knowledge/maui.md`).
Среда: Linux (Presentation, тест разметки, сборка MAUI Android); проверка на телефоне — 140.

## Критерии готовности
- `SessionItemViewModel.ShowActionsCommand`: через `IDialogService.ChooseAsync(Title, Cancel, [Rename, Delete session])` вызывает те же делегаты, что `RenameCommand`/`DeleteCommand`; отмена — ничего. Делегат показа передаётся из `SessionsPageModel` (по образцу `OpenSessionAsync`/`RenameSessionAsync`/`DeleteSessionAsync` — новый аргумент конструктора `showActions`, обе точки создания в `SessionsPageModel` обновить).
- `SessionsListView.xaml`: два шаблона — `SessionRowTemplateDesktop` (нынешний, со `SwipeView` — его можно оставить или убрать, решить по факту: на десктопе свайп мышью не нужен; если убираете — записать в «Отчёт») и `SessionRowTemplatePhone`: `Grid x:Name="RowRoot"` строки без `SwipeView` с `toolkit:TouchBehavior Command="{Binding OpenCommand}" LongPressCommand="{Binding ShowActionsCommand}"` и явным контекстом `BindingContext="{Binding Source={x:Reference RowRoot}, Path=BindingContext}"` — поведения не наследуют `BindingContext` (см. 211; запись привязки — та же, что выбрана в 211). Дочерние элементы здесь без кнопок, поэтому `ShouldMakeChildrenInputTransparent` по умолчанию допустим. Выбор шаблона в обоих `BindableLayout.ItemTemplate` — `{OnIdiom Desktop={StaticResource SessionRowTemplateDesktop}, Default={StaticResource SessionRowTemplatePhone}}`.
- Доступность: у строки `SemanticProperties.Description` = название, `SemanticProperties.Hint` — «Long press for actions» (ресурс).
- Тесты: `SessionItemViewModelTests` (или `SessionsPageModelTests`): выбор Rename → вызван делегат переименования; Delete → удаления; отмена → ничего; тест разметки (XML): в телефонном шаблоне нет `SwipeView`, есть `TouchBehavior` с обеими командами и явным источником привязки.
- Сборка MAUI Android; пункт 140: нажатие открывает сессию, долгое нажатие — лист Rename/Delete.

## Анализ
`ImageGen/ImageGen.Maui/Controls/SessionsListView.xaml` (шаблон `SessionRowTemplate`, строки 13–60 на `07ff84a`; используется в двух `BindableLayout` около строк 103 и 107). `ImageGen/ImageGen.Presentation/PageModels/SessionItemViewModel.cs` (команды `Open/Rename/Delete`), `SessionsPageModel.cs` (создание элементов около строк 217 и 309). Строки `Rename`, `DeleteSession`, `Cancel` в ресурсах уже есть. Пространство имён `toolkit` — как в `ChatPage.xaml`.

## План
1. Падающие тесты модели и разметки.
2. `ShowActionsCommand` и делегат.
3. Два шаблона и выбор по устройству; ресурс подсказки.
4. Сборка Android, пункт 140.

Файлы: `ImageGen/ImageGen.Presentation/PageModels/SessionItemViewModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/SessionsPageModel.cs`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/SessionsPageModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/Design/` (тест разметки списка сессий; найти существующий файл для `SessionsListView` или создать `MauiSessionsListMarkupTests.cs`), `ImageGen/ImageGen.Maui/Controls/SessionsListView.xaml`, `tasks/backlog/140-android-manual-check.md`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
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

## Комментарии

### 2026-11-07, claude-audit
Создано по отчёту владельца о сборке v0.1.11 на Android (MAUI, логи 2026-11-05 и 2026-11-06, скриншоты). План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
План одобрен ранее данным поручением «заапрувь все таски которые будут».

### 2026-11-07, claude-audit
Добавлен явный `BindingContext` поведения (отчёт владельца: долгое нажатие не работает — причина в 211) и зависимость от 211.
