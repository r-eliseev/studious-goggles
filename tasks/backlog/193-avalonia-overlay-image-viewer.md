---
id: 193
status: planned
assignee:
claimed:
created: 2026-11-06
---

# Avalonia: просмотр картинки поверх страницы (для Android)

## Описание
ADR 0026, п. 4. `AvaloniaImageViewerService` показывает картинку в отдельном модальном окне `ImageViewerWindow` (`ImageGen/ImageGen.Avalonia/Services/AvaloniaImageViewerService.cs`). На Android окно одно, поэтому содержимое просмотрщика нужно вынести в элемент управления, который размещает либо окно (десктоп, как сейчас), либо слой поверх страницы (Android). Поведение десктопа не меняется.

Зависит от: 192 (тот же приём слоя и хоста `Func<Panel?>`; делать после её слияния).
Среда: Linux или Windows.

## Критерии готовности
- Новый `ImageViewerPanel : UserControl` (в том же каталоге `Services/` или `Views/` — где удобнее, но в `ImageGen.Avalonia`) содержит всё, что сейчас строит `ImageViewerWindow`: картинку, позицию, кнопки Close/Previous/Next, фоновую загрузку `Bitmap`, `ImageViewerNavigationState`, клавиши Escape/Left/Right, закрытие по клику на фон, освобождение `Bitmap`. Вместо `Close()` окна панель поднимает событие `CloseRequested`.
- `ImageViewerWindow` только размещает `ImageViewerPanel` и закрывается по `CloseRequested`; существующие тесты `ImageViewerWindowTests` зелёные без изменения ожиданий (допустимо поменять доступ к внутренностям через панель: `window.Panel.DisplayedIndex` и т. п.).
- Новый `OverlayImageViewerService : IImageViewerService` с хостом `Func<Panel?>`: добавляет `ImageViewerPanel` на весь хост (чёрный фон), ждёт `CloseRequested` или отмены, убирает панель и освобождает картинку. Повторный `ShowAsync` при открытом просмотрщике игнорируется (как у окна). Метод `TryCloseTop()` для системного Back.
- Кнопки не меньше 48×48 (уже так в окне).
- Headless-тесты слоя: открытие и Close, Escape, `TryCloseTop`, отмена токена, отсутствующий файл → локализованное сообщение через `IDialogService` и закрытие (как в оконном тесте).

## Анализ
Окно сейчас: конструктор `(items, initialIndex, Window owner, IDialogService dialogs)` задаёт размер по владельцу, строит `root`, подписывается на `KeyDown` и `Closed`; методы `StartLoading`, `DisposeImage`, `Navigate`, `LoadAsync`, `IsRecognizedImage`; внутренние свойства для тестов `DisplayedIndex`, `DisplayedBitmap`, `PositionText`. Служба окна использует `AvaloniaFullScreenController` — у слоя полноэкранного режима ОС нет (Android-приложение и так на весь экран), контроллер слою не нужен.

Перенос: всё, кроме размеров окна, владельца, `Title`, `Closed` и полноэкранного режима, переходит в панель. Клавиши: панель обрабатывает `KeyDown` сама, ей нужен фокус — при показе вызвать `Focus()`.

## План
1. Тесты `ImageGen/ImageGen.Avalonia.Tests/Services/OverlayImageViewerServiceTests.cs` (headless, хост — `Grid` в тестовом окне, картинки — временные PNG, как в `ImageViewerWindowTests`): показ двух картинок с индексом 0 → в хосте `ImageViewerPanel`, позиция «1 / 2»; Next → «2 / 2»; Close → панель убрана, задача `ShowAsync` завершена; Escape → то же; `TryCloseTop()` → `true` и закрыто, без просмотрщика → `false`; отмена токена → закрыто; первый файл отсутствует → `IDialogService.AlertAsync` вызван, панель закрыта.
2. Выделить `ImageViewerPanel` из `ImageViewerWindow` (см. «Анализ»). Окно: `Content = panel`, `panel.CloseRequested += (_, _) => Close()`, `Closed += (_, _) => panel.Dispose…` (как сейчас `_navigation.Close()`/`DisposeImage`).
3. Прогнать `ImageViewerWindowTests` — должны пройти; поправить только доступ к внутренним свойствам.
4. `OverlayImageViewerService` (конструктор `(Func<Panel?> host, IDialogService dialogs)`), логика — как в `AvaloniaImageViewerService.ShowAsync` без окна и полноэкранного режима.
5. В DI не регистрировать: регистрирует Android-голова (195).

Файлы: `ImageGen/ImageGen.Avalonia/Services/AvaloniaImageViewerService.cs`, `ImageGen/ImageGen.Avalonia/Services/ImageViewerPanel.cs` (новый), `ImageGen/ImageGen.Avalonia/Services/OverlayImageViewerService.cs` (новый), `ImageGen/ImageGen.Avalonia.Tests/Services/ImageViewerWindowTests.cs`, `ImageGen/ImageGen.Avalonia.Tests/Services/OverlayImageViewerServiceTests.cs` (новый).

### Проверка
```
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet format ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj --verify-no-changes
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

### 2026-11-06, claude-audit
Создано по поручению владельца («распланируй андроид на авалонии, с билдами»; выбрано: второе приложение рядом с MAUI, APK в каждом релизе). ADR 0026. План для Luna, ждёт одобрения.
