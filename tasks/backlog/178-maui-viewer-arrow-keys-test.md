---
id: 178
status: todo
assignee:
claimed:
created: 2026-11-01
---

# Регрессионный тест на клавиши MAUI-просмотрщика под Windows

## Описание
Задача 148 добавила Left/Right/Escape в `OnWindowsKeyDown` (`ImageGen/ImageGen.Maui/Services/MauiImageViewerService.cs`, блок `#if WINDOWS`), но регрессионного теста нет: шаг 2 её плана (вынести сопоставление клавиш в простую функцию и проверять в Presentation.Tests) не выполнен. Решение владельца 2026-11-01: оформить отдельной задачей.

Зависит от: 148.
Среда: Linux (функция и тест в Presentation; Windows-цель MAUI собирает CI).

## Критерии готовности
- В Presentation есть чистая функция сопоставления клавиши просмотрщика с действием (закрыть, назад, вперёд, не обрабатывать) на своём типе клавиши, без `Windows.System.VirtualKey`.
- Тесты в Presentation.Tests: Escape → закрыть, Left → назад, Right → вперёд, любая другая клавиша → не обрабатывается.
- `OnWindowsKeyDown` переводит `VirtualKey` в свой тип и выполняет действие по функции; `Handled = true` только для обработанных клавиш. Поведение не меняется.
- Windows-цель MAUI собирается (CI), Android-цель собирается без WinUI-типов вне `#if WINDOWS`.

## Анализ
Сейчас (на `main` после PR #68) `OnWindowsKeyDown` — строки 138–155: `switch` по `VirtualKey` с Escape/Left/Right. Рядом в Presentation уже есть аналогичные вынесенные функции для просмотрщика: `ImageViewerHitTest` (задача 149) и `ImageViewerNavigationState` (задача 146), их тесты — `ImageGen/ImageGen.Presentation.Tests/Services/`. Новая функция ложится рядом в `ImageGen/ImageGen.Presentation/Services/`. Границы навигации без wrap держит `ImageViewerNavigationState`, их не дублировать.

Тест, который ищет строки в исходнике XAML/C#, не подходит (план 148, шаг 2).

## План
1. Падающий тест: `ImageViewerKeysTests` — четыре случая из «Критериев готовности».
2. Тип клавиши просмотрщика (Escape, Left, Right, Other) и действие (None, Close, Previous, Next); статическая функция «клавиша → действие».
3. В `#if WINDOWS` обработчике: `VirtualKey` → свой тип, действие по функции; `Close()`/`Navigate(-1)`/`Navigate(1)`; `Handled` только при действии ≠ None. Подписка/отписка в `OnHandlerChanged` не меняется.
4. Ручная проверка стрелок уже записана в 016 (из 148), новых ручных пунктов нет.

Файлы: `ImageGen/ImageGen.Presentation/Services/ImageViewerKeys.cs` (новый), `ImageGen/ImageGen.Presentation.Tests/Services/ImageViewerKeysTests.cs` (новый), `ImageGen/ImageGen.Maui/Services/MauiImageViewerService.cs`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj --verify-no-changes
bash scripts/check.sh
```
Windows-цель MAUI проверяет CI.

## Отчёт

## Комментарии

### 2026-11-01, claude-audit
Создано по решению владельца после закрытия 148 без регрессионного теста. План ждёт одобрения.
