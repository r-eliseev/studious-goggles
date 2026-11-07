---
id: 220
status: done
assignee:
claimed:
created: 2026-11-07
---

# Видео-карточка: кадр-обложка и пропорции

## Описание
После 219 видео-карточка в ленте показывает значок. Нужна обложка — кадр из начала ролика, чтобы варианты одного запроса различались в ленте.

Отчёт владельца 2026-11-07 (скриншот): у видео «та же проблема с центровкой и пропорциями», что была у картинок (213) — область видео-карточки фиксирована 264×176.

Зависит от: 219.
Среда: Linux (Presentation, сборка MAUI Android); проверка — 140.

## Критерии готовности
- `IVideoPosterService.GetPosterAsync(string videoPath, CancellationToken)` → `PosterInfo?` (путь к JPEG-обложке, ширина и высота кадра) или `null` (в `ImageGen.Presentation/Services/`), с `IsSupported`.
- MAUI Android: реализация через `Android.Media.MediaMetadataRetriever` (`SetDataSource(path)`, `GetFrameAtTime(0, OptionClosestSync)`), кадр уменьшается до ширины 528 px (2× ширины карточки) и сохраняется JPEG (качество 80) рядом с видео как `<имя видео>.poster.jpg` атомарно (временный файл + `File.Move`); если файл уже есть — возвращается сразу. Работа — вне UI-потока. Ошибка извлечения → `null` и одна Warning (тип исключения, без пути).
- Остальные платформы MAUI и Avalonia — реализация `IsSupported = false` (значок вместо обложки).
- Пропорции видео-карточки: `ImageAreaHeight` для видео считается той же функцией `CardImageLayout.HeightFor` (213): пока генерация идёт или обложки нет — по запрошенному соотношению сторон прогона (`Run.VideoParameters.AspectRatio`, например `9:16` → высота по 9:16; `null`/`auto` — 16:9), после обложки — по фактическим ширине и высоте кадра (служба возвращает `PosterInfo(string Path, int Width, int Height)` вместо пути). Обложка — `Aspect="AspectFit"`; плейсхолдер «идёт генерация» — той же высоты.
- `GenerationItemViewModel.VideoPosterPath` заполняется после `HasMedia` для видео (фоновая загрузка по образцу чтения размеров картинки — найти, как карточка получает `ImageDimensions` через `TryReadDimensionsAsync`, и повторить подход).
- Удаление сессии удаляет и обложки (они лежат в папке сессии `images/<session>/` — проверить, что `DeleteSessionFilesAsync` удаляет папку целиком; иначе добавить).
- Тесты Presentation: для видео с поддерживаемой службой путь обложки появляется в модели; без поддержки — пусто; служба вызывается один раз на карточку; идущее видео 9:16 → `ImageAreaHeight = 352`, 16:9 → 148.5; обложка 1280×720 → 148.5.
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

### 2026-11-07, codex-gpt6
- Добавлены `IVideoPosterService`/`PosterInfo` и unsupported реализация; MAUI Android реализует извлечение кадра на фоне через `MediaMetadataRetriever`, уменьшение до 528 px и атомарный JPEG рядом с видео. Готовый постер переиспользуется; исключения логируют одну Warning без пути, без кадра возвращается `null`.
- `GenerationItemViewModel` кэширует один запрос постера на карточку, а `ChatPageModel` запускает чтение вне UI-потока и применяет результат на UI-потоке только если карточка и сессия актуальны. Отношения видео берутся из параметров прогона; отсутствующий/auto ratio — 16:9. Успешная обложка задаёт фактические пропорции. Удаление сессии уже рекурсивно удаляет всю папку файлов (`FileImageStore.DeleteSessionFilesAsync`), отдельный код не потребовался.
- TDD: новые тесты сначала упали — не было контракта и высота оставалась 176; после реализации проверены 9:16 → 352, 16:9/auto → 148.5, фактический постер 1280×720 → 148.5, одна загрузка на карточку и unsupported режим.
- `dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj` — 525/525; `dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj` — 180 пройдено, 2 пропущено для недоступных в Linux системных служб.
- `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android --no-restore` — 0 предупреждений и ошибок. Форматирование проверено для Presentation, Presentation.Tests и MAUI. `bash scripts/check.sh` выполняется перед коммитом.
- Ручная проверка кадра на Android не выполнялась в этой среде; добавлена в пункт 140.

## Комментарии

### 2026-11-07, claude-audit
Создано по отчёту владельца 2026-11-07 (лог MAUI Android `ce90792`: видео генерируется, звук есть, полноэкранный режим работает). План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
План одобрен ранее данным поручением «заапрувь все таски которые будут».

### 2026-11-07, claude-audit
Добавлены пропорции видео-карточки по отчёту владельца («у видео та же проблема с центровкой и пропорциями»); центровка карточек — задача 225.
