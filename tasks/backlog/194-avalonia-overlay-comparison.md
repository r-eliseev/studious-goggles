---
id: 194
status: done
assignee:
claimed:
created: 2026-11-06
---

# Avalonia: сравнение картинок поверх страницы (для Android)

## Описание
ADR 0026, п. 4. Как в задаче 193, но для сравнения: `AvaloniaImageComparisonService` показывает `ComparisonWindow` (`ImageGen/ImageGen.Avalonia/Services/AvaloniaImageComparisonService.cs`, 335 строк). Содержимое выносится в `ComparisonPanel`, окно его размещает, а `OverlayImageComparisonService` показывает его слоем. Поведение десктопа не меняется.

Зависит от: 193 (тот же приём; делать после её слияния, чтобы повторить решение).
Среда: Linux или Windows.

## Критерии готовности
- `ComparisonPanel : UserControl` содержит всё содержимое `ComparisonWindow` (обе картинки, режимы, синхронный масштаб/сдвиг, кнопки, загрузку и освобождение картинок) и поднимает `CloseRequested` вместо `Close()`.
- `ComparisonWindow` только размещает панель; `ComparisonWindowTests` зелёные без изменения ожиданий (кроме доступа к внутренностям через панель).
- `OverlayImageComparisonService : IImageComparisonService` с хостом `Func<Panel?>`: показ на весь хост, закрытие по `CloseRequested`/отмене, освобождение картинок, `TryCloseTop()`.
- Сдвиг и масштаб работают от указателя (Avalonia даёт касания как `Pointer`-события); отдельных жестов «щипок» не требуется.
- Headless-тесты слоя: открытие, Close, `TryCloseTop`, отмена токена, отсутствующий файл.

## Анализ
Сделать по образцу, принятому в 193 (прочитать её «Отчёт» перед началом). Служба окна: конструктор `(Func<Window?> owner, IDialogService dialogs)`, методы окна `DisposeImages` и др.; модель — `ComparisonPageModel` (Presentation).

## План
1. Тесты `ImageGen/ImageGen.Avalonia.Tests/Services/OverlayImageComparisonServiceTests.cs` по образцу `OverlayImageViewerServiceTests` (193) и `ComparisonWindowTests`.
2. Выделить `ComparisonPanel` из `ComparisonWindow`; окно размещает панель.
3. Прогнать `ComparisonWindowTests`.
4. `OverlayImageComparisonService` (конструктор `(Func<Panel?> host, IDialogService dialogs)`).
5. В DI не регистрировать (195).

Файлы: `ImageGen/ImageGen.Avalonia/Services/AvaloniaImageComparisonService.cs`, `ImageGen/ImageGen.Avalonia/Services/ComparisonPanel.cs` (новый), `ImageGen/ImageGen.Avalonia/Services/OverlayImageComparisonService.cs` (новый), `ImageGen/ImageGen.Avalonia.Tests/Services/ComparisonWindowTests.cs`, `ImageGen/ImageGen.Avalonia.Tests/Services/OverlayImageComparisonServiceTests.cs` (новый).

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

Вынес содержимое сравнения из `ComparisonWindow` в `ComparisonPanel`; окно теперь только размещает панель. Добавил `OverlayImageComparisonService` с закрытием через Escape, системный Back и токен отмены, снятием панели с хоста и освобождением обеих картинок. Сохранены side-by-side/overlay режимы, общий zoom/pan и обработка указателя. Оконные тесты остались без изменения ожиданий; добавил headless-тесты слоя на загрузку, переключение режима, закрытие, отмену, Back и отсутствующий файл с локализованным alert.

Проверки: Avalonia.Tests — 180 всего, 178 пройдены, 2 пропущены (D-Bus и Windows DPAPI недоступны в Linux-среде); `dotnet format` для Avalonia и Avalonia.Tests — без изменений; `bash scripts/check.sh` — пройден.

От плана: зависимость 193 ещё не влита в `main`; продолжил в той же ветке и PR по прямому поручению владельца не ждать слияний.

## Комментарии

### 2026-11-06, codex-gpt6
Выполнено поверх задач 191–193 в общем PR, как поручил владелец; 193 находится в том же PR.

### 2026-11-06, claude-audit
Создано по поручению владельца («распланируй андроид на авалонии, с билдами»; выбрано: второе приложение рядом с MAUI, APK в каждом релизе). ADR 0026. План для Luna, ждёт одобрения.

### 2026-11-06, владелец (в чате, записал claude-audit)
План одобрен («Да» на вопрос об одобрении 191–198 и PR).
