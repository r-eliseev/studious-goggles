---
id: 204
status: planned
assignee:
claimed:
created: 2026-11-06
---

# Видео в интерфейсе: выбор моделей, параметры, карточка без плеера

## Описание
ADR 0027, п. 6. Общий слой Presentation и разметка обеих оболочек: выбор видео-моделей и параметров сессии, видео-карточка в ленте (статус, модель, длительность, цена, действия, предупреждение об отмене). Сам плеер — 205 (MAUI) и 207 (Avalonia); до них у готовой видео-карточки есть Save file, но нет воспроизведения (см. «Анализ»).

Зависит от: 203, 209.
Среда: Linux или Windows (Presentation, Avalonia headless, сборка MAUI Android на Linux).

## Критерии готовности
- `SessionModelsPageModel`: раздел «Video models» со списком из `VideoModelCatalogService` (имя, цена за 5 с или «price unknown», значок звука), выбор нескольких моделей; сохранение через `UpdateVideoSettingsAsync`.
- `SessionParametersPageModel`: раздел «Video» — длительность (только общие для выбранных моделей значения `supported_durations`), разрешение и соотношение сторон (пересечение поддерживаемых; «auto» = не задано), переключатель звука (виден, если хотя бы одна модель поддерживает звук).
- `GenerationItemViewModel`: `IsVideo`, `IsAudio`, `MediaFilePath` (полный путь файла при успехе), `DurationText` («8 s»); у видео-карточки нет картинки и просмотрщика (`OpenImageCommand` не срабатывает); действия Repeat, New seed, Edit, Save file, Copy prompt; Run on model, Describe, Compare — скрыты.
- Отмена видео: после результата `StoppedWaiting` (209) карточка показывает «Stopped waiting. The provider may still charge for this video.» (ресурс).
- Save file для видео: существующий `IImageSaver` получает тип файла и расширение из `MediaType` (если интерфейс рассчитан только на картинки — расширить параметром типа; в Avalonia `AvaloniaImageSaver` — фильтр по типу; в MAUI — сохранение через тот же механизм, что картинки, с типом видео).
- Строка «Next run ≈ $X» в чате учитывает видео-модели (`EstimateVideoAsync`), предупреждения о лимите — как для картинок.
- Разметка: MAUI (`SessionModelsPage`, `SessionParametersPage`, `ChatPage` — шаблон видео-карточки с иконкой и длительностью вместо картинки) и Avalonia (`SessionModelsView`, `SessionParametersView`, `ChatView`). Доступность: `SemanticProperties`/`AutomationProperties` у новых элементов.
- Тесты Presentation; Avalonia headless тест разметки карточки (по образцу существующих `Views/*Tests`); сборка MAUI Android.

## Анализ
Модели страниц: `ImageGen/ImageGen.Presentation/PageModels/SessionModelsPageModel.cs`, `SessionParametersPageModel.cs`, `GenerationItemViewModel.cs`, `RunItemViewModel.cs`, `ChatPageModel.cs` (`UpdateNextRunEstimateAsync`, `CancelCardAsync`). Ресурсы — `AppResources.resx` + `AppResources.cs`. «Open» до плеера не делается: на телефоне нет общей службы «открыть файл», а 205/207 дают плеер; карточка до них показывает только Save file.

Объём большой: если реализация одного шага превышает разумный размер (например, обе разметки параметров), остановиться после Presentation-части и тестов, записать в «Комментарии» и предложить выделить разметку отдельной задачей.

## План
1. Presentation, падающие тесты:
   - `SessionModelsPageModelTests`: видео-модели из каталога показаны с ценой; выбор двух и сохранение → `UpdateVideoSettingsAsync` с ними.
   - `SessionParametersPageModelTests`: две модели с длительностями {4,6,8} и {5,6,8,10} → варианты {6,8}; звук скрыт, если ни одна модель его не поддерживает.
   - `GenerationItemViewModelTests`: видео-генерация `Succeeded` → `IsVideo`, `MediaFilePath`, `DurationText` «8 s», команды Describe/Compare/Run on model недоступны.
   - `ChatPageModelTests`: отмена видео с `StoppedWaiting` → текст предупреждения на карточке; оценка «Next run» с видео-моделями.
2. Реализация моделей страниц и ресурсов.
3. Разметка MAUI и Avalonia; тест разметки Avalonia; сборка MAUI Android.

Файлы: модели страниц и тесты из «Анализа», `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.cs`, `ImageGen/ImageGen.Presentation/Services/IImageSaver.cs` (при расширении), `ImageGen/ImageGen.Maui/Pages/SessionModelsPage.xaml`, `ImageGen/ImageGen.Maui/Pages/SessionParametersPage.xaml`, `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, `ImageGen/ImageGen.Avalonia/Views/SessionModelsView.axaml`, `ImageGen/ImageGen.Avalonia/Views/SessionParametersView.axaml`, `ImageGen/ImageGen.Avalonia/Views/ChatView.axaml`, `ImageGen/ImageGen.Avalonia/Services/AvaloniaImageSaver.cs`, `ImageGen/ImageGen.Avalonia.Tests/Views/`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj --verify-no-changes
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

### 2026-11-06, claude-audit
Создано по поручению владельца («распланируй звук и видео»; выбрано: всё через OpenRouter, LibVLC в Avalonia сразу). ADR 0027, исследование — `docs/knowledge/media-generation.md`. План для Luna, ждёт одобрения.
