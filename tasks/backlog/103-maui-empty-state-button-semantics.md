---
id: 103
status: approved
assignee:
claimed:
created: 2026-10-29
---

# MAUI: у кнопки «New session» на пустом экране нет описания для экранного диктора

## Описание
Найдено аудитом 2026-10-29 (`docs/log/2026-10-29-audit.md`): `ImageGen/ImageGen.Maui/Pages/SessionsPage.xaml` (блок `EmptyState`, кнопка с `Command="{Binding NewSessionCommand}"`) не задаёт `SemanticProperties.Description`, хотя `docs/conventions.md` требует его у всех кнопок. Остальные кнопки MAUI-разметки выборочно проверены — описание есть. Сверить Avalonia (`AutomationProperties.Name` у той же кнопки в `SessionsView.axaml`).

Зависит от: —
Среда: Linux или Windows.

## Критерии готовности
- У кнопки задано описание из `AppResources` (та же строка, что и текст кнопки).
- Grep по `ImageGen.Maui/**/*.xaml` не находит других `Button`/`ImageButton` без `SemanticProperties.Description` (или исключения перечислены в «Отчёте» с причиной).
- То же в Avalonia для `AutomationProperties.Name`.

## Анализ
- Проверено скриптом (2026-10-29): во всей MAUI-разметке (`ImageGen.Maui/**/*.xaml`) единственный элемент `<Button`/`<ImageButton` без `SemanticProperties.Description` — кнопка «New session» в `Pages/SessionsPage.xaml` (около строки 39, блок `EmptyState`). Совпадения `<Button.Triggers>` и `<Button.ImageSource>` — это вложенные свойства, не кнопки.
- В Avalonia та же кнопка (`Views/SessionsView.axaml`, около строки 18) уже имеет `AutomationProperties.Name="{markup:Res NewSession}"`: правка не нужна.
- Пересечение: 095 (safe area Android) меняет тот же `ImageGen/ImageGen.Maui/Pages/SessionsPage.xaml` — атрибут `SafeAreaEdges` корневого `ContentPage`; здесь меняется только кнопка в блоке `EmptyState`. Участки разные, порядок любой, при `rebase` сохранять обе стороны. 095 также добавляет свой тест `MauiSafeAreaTests.cs` в ту же папку `Design/` — файлы разные.
- Почему не построчный grep: открывающий тег кнопки в разметке часто занимает несколько строк (атрибуты на отдельных строках), поэтому `grep` по строке с `<Button` не видит `SemanticProperties.Description` на следующих строках и даёт ложные срабатывания (или пропуски). Надёжно — разобрать XAML как XML и проверять атрибуты элемента целиком. Проверено так (разбор XML, 2026-10-29): во всех `ImageGen/ImageGen.Maui/**/*.xaml` без `bin/` и `obj/` единственный `Button`/`ImageButton` без атрибута `SemanticProperties.Description` — кнопка «New session» в `SessionsPage.xaml`.

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, после каждого шага — сборка и проверки из «Проверки». Имена файлов, ключей строк и свойств переноси дословно. Если шаг не получается так, как написано, не придумывай обход: опиши в «Комментариях», что пробовал, поставь `blocked` и остановись.

Прочитай перед началом: `docs/conventions.md` (раздел про доступность), `ImageGen/ImageGen.Maui/Pages/SessionsPage.xaml`.

1. Падающий тест-сторож (вместо построчного grep, см. «Анализ»). Новый файл `ImageGen/ImageGen.Presentation.Tests/Design/MauiButtonSemanticsTests.cs`, класс `MauiButtonSemanticsTests` (пространство `ImageGen.Presentation.Tests.Design`), со своим закрытым методом `MauiRoot()`, скопированным из `ImageGen/ImageGen.Presentation.Tests/Design/MauiStylesTests.cs`. Тест `Buttons_HaveSemanticDescription`, словами:
   - перебрать все файлы `*.xaml` в папке MAUI рекурсивно, пропуская пути с папками `bin` и `obj`;
   - каждый файл загрузить как XML (`XDocument.Load`) и обойти все элементы (`Descendants()` корня вместе с самим корнем);
   - кнопка — элемент, у которого локальное имя (`Name.LocalName`) ровно `Button` или `ImageButton`. Пространство имён не сравнивать. Элементы-свойства вроде `Button.Triggers` и `Button.ImageSource` имеют другое локальное имя и сами отпадают;
   - у кнопки должен быть атрибут с локальным именем `SemanticProperties.Description` (у прикреплённого свойства в XAML пространства имён у атрибута нет);
   - собрать список нарушений «файл относительно папки MAUI + значение `Text` или `Command` кнопки» и в конце проверить, что он пуст; список — в сообщении ошибки.
   Сейчас тест падает ровно на одной кнопке: `Pages/SessionsPage.xaml`, `NewSessionCommand`. Если он находит другие кнопки — см. шаг 3.
2. В `ImageGen/ImageGen.Maui/Pages/SessionsPage.xaml`, в `Border x:Name="EmptyState"`, у `Button` с `Command="{Binding NewSessionCommand}"` добавить атрибут `SemanticProperties.Description="{x:Static strings:AppResources.NewSession}"` (тот же ключ, что у `Text`). Других правок в этом файле нет. Тест шага 1 проходит.
3. Если тест шага 1 нашёл ещё кнопки без описания (например, их добавила другая задача, влитая раньше) — добавить описание тем же способом (ключ строки — тот же, что у `Text`; если `Text` нет — ключ из соседнего `ToolTipProperties.Text` или `AutomationProperties`; если подходящего ключа нет — поставить `blocked` и описать в «Комментариях»). Добавленные файлы дописать в строку `Файлы:` в «Отчёте».
4. «Отчёт»: перечислить изменённые кнопки и что тест шага 1 зелёный (это и есть проверка второго критерия готовности вместо grep).

### Проверка
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj --verify-no-changes
bash scripts/check.sh
```
(Если Android SDK в среде нет, сборку Android пропустить и записать в «Отчёт»; Windows-цель собирает CI.)

### Ручная проверка
Владельцу (дописать в задачу 016): на пустом списке сессий в MAUI под Windows экранный диктор читает у кнопки «New session».

Файлы: `ImageGen/ImageGen.Maui/Pages/SessionsPage.xaml`, `ImageGen/ImageGen.Presentation.Tests/Design/MauiButtonSemanticsTests.cs` (новый), `tasks/v0.1/016-skeleton-manual-check.md` (пункт ручной проверки)

## Отчёт

## Комментарии

### 2026-10-29, владелец (в чате, записал claude-plan)
План одобрен; допущения из комментариев claude-plan приняты как решения. План переписан под небольшую модель.
### 2026-10-29, claude-review
Владелец попросил сделать задачу исполнимой небольшой моделью. Изменено:
- «пересечений нет» неверно: 095 меняет тот же `SessionsPage.xaml` (корневой `ContentPage`, атрибут `SafeAreaEdges`); участки разные, при `rebase` сохранять обе стороны;
- построчный grep шага 2 пропускает многострочные теги; заменён тестом-сторожем `MauiButtonSemanticsTests` (разбор XAML через `XDocument`, кнопки по локальному имени `Button`/`ImageButton` без атрибута `SemanticProperties.Description`), он пишется первым и падает (TDD). Тест внесён в `Файлы:`, в «Проверку» добавлены `dotnet test` и `dotnet format` для `Presentation.Tests`. Вывод «нарушение одно — New session» перепроверен разбором XML.
