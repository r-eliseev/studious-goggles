---
id: 220
status: approved
assignee:
claimed:
created: 2026-11-07
---

# Видео-карточка: кадр-обложка

## Описание
После 219 видео-карточка в ленте показывает значок. Нужна обложка — кадр из начала ролика, чтобы варианты одного запроса различались в ленте.

Зависит от: 219.
Среда: Linux (Presentation, сборка MAUI Android); проверка — 140.

## Критерии готовности
- `IVideoPosterService.GetPosterPathAsync(string videoPath, CancellationToken)` → путь к JPEG-обложке или `null` (в `ImageGen.Presentation/Services/`), с `IsSupported`.
- MAUI Android: реализация через `Android.Media.MediaMetadataRetriever` (`SetDataSource(path)`, `GetFrameAtTime(0, OptionClosestSync)`), кадр уменьшается до ширины 528 px (2× ширины карточки) и сохраняется JPEG (качество 80) рядом с видео как `<имя видео>.poster.jpg` атомарно (временный файл + `File.Move`); если файл уже есть — возвращается сразу. Работа — вне UI-потока. Ошибка извлечения → `null` и одна Warning (тип исключения, без пути).
- Остальные платформы MAUI и Avalonia — реализация `IsSupported = false` (значок вместо обложки).
- `GenerationItemViewModel.VideoPosterPath` заполняется после `HasMedia` для видео (фоновая загрузка по образцу чтения размеров картинки — найти, как карточка получает `ImageDimensions` через `TryReadDimensionsAsync`, и повторить подход).
- Удаление сессии удаляет и обложки (они лежат в папке сессии `images/<session>/` — проверить, что `DeleteSessionFilesAsync` удаляет папку целиком; иначе добавить).
- Тесты Presentation: для видео с поддерживаемой службой путь обложки появляется в модели; без поддержки — пусто; служба вызывается один раз на карточку.
- Сборка MAUI Android; пункт 140: у видео в ленте виден кадр.

## Анализ
Чтение размеров картинки — `IImageFileStore.TryReadDimensionsAsync` и то, как `ChatPageModel`/`GenerationItemViewModel` вызывают `ApplyImageDimensions`. Папка файлов — `FileImageStore` (`images/<session>/<generation>.<ext>`). Платформенный код Android — по образцу `ImageGen/ImageGen.Maui/Platforms/Android/`.

## План
1. Падающие тесты модели.
2. Интерфейс и заглушка; свойство и загрузка в модели.
3. Android-реализация в `Platforms/Android/`, регистрация под `#if ANDROID` в `MauiProgram`.
4. Привязка обложки в шаблоне 219; сборка; пункт 140.

Файлы: `ImageGen/ImageGen.Presentation/Services/IVideoPosterService.cs` (новый), `ImageGen/ImageGen.Presentation/Services/UnsupportedVideoPosterService.cs` (новый), `ImageGen/ImageGen.Presentation/PageModels/GenerationItemViewModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/ChatPageModel.cs`, `ImageGen/ImageGen.Maui/Platforms/Android/AndroidVideoPosterService.cs` (новый), `ImageGen/ImageGen.Maui/MauiProgram.cs`, `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, `ImageGen/ImageGen.Avalonia/Composition/AppServices.Desktop.cs`, `ImageGen/ImageGen.Avalonia.Android/MobileApp.axaml.cs`, тесты `ImageGen/ImageGen.Presentation.Tests/PageModels/`, `tasks/backlog/140-android-manual-check.md`.

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
Создано по отчёту владельца 2026-11-07 (лог MAUI Android `ce90792`: видео генерируется, звук есть, полноэкранный режим работает). План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
План одобрен ранее данным поручением «заапрувь все таски которые будут».
