---
id: 218
status: in-progress
assignee: codex-gpt6
claimed: 2026-11-07T10:24:44Z
created: 2026-11-07
---

# MAUI: плеер только у карточек видео и аудио

## Описание
Лог владельца 2026-11-07: при каждом открытии чата и после каждой готовой картинки — пары записей `[ERR] MediaManager Error codename: ERROR_CODE_PARSING_CONTAINER_UNSUPPORTED, Error code: 3003`. Причина: в шаблоне карточки `ChatPage.xaml` два `toolkit:MediaElement` (видео и аудио) есть у **каждой** карточки, `Source` привязан к `MediaFilePath`, а `GenerationItemViewModel` заполняет `MediaFilePath` для любой успешной генерации, включая картинки. ExoPlayer пытается открыть PNG как медиафайл. Скрытие через `IsVisible` не мешает загрузке источника, а каждый `MediaElement` — отдельный плеер в памяти.

Зависит от: —
Среда: Linux (Presentation, тест разметки, сборка MAUI Android); проверка — 140.

## Критерии готовности
- `GenerationItemViewModel`: `VideoFilePath` (путь только при `IsVideo && HasMedia`, иначе `""`) и `AudioFilePath` (только при `IsAudio && HasMedia`); `MediaFilePath` остаётся для Save file.
- `ChatPage.xaml`: видео-`MediaElement` привязан к `VideoFilePath`, аудио — к `AudioFilePath`; для картинок оба источника `null`.
- Тесты: картинка `Succeeded` → `VideoFilePath` и `AudioFilePath` пусты; видео → `VideoFilePath` = путь, `AudioFilePath` пуст; аудио — наоборот. Тест разметки: `Source` видео-плеера привязан к `VideoFilePath`, аудио — к `AudioFilePath`.
- Сборка MAUI Android; пункт 140: в журнале при открытии чата с картинками нет `ERROR_CODE_PARSING_CONTAINER_UNSUPPORTED`.

## Анализ
`ImageGen/ImageGen.Presentation/PageModels/GenerationItemViewModel.cs` (≈419–425: `HasMedia`, `MediaFilePath`, `HasImage`, `ImageFilePath`). `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml` (≈175 и ≈253). Задача 219 заменяет встроенный видео-плеер в ленте — эта задача независима и нужна в любом случае (аудио-плеер остаётся в ленте).

## План
1. Падающие тесты модели и разметки.
2. Свойства и привязки.
3. Сборка Android, пункт 140.

Файлы: `ImageGen/ImageGen.Presentation/PageModels/GenerationItemViewModel.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/GenerationItemViewModelTests.cs`, `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, `ImageGen/ImageGen.Presentation.Tests/Design/MauiChatPageMarkupTests.cs`, `tasks/backlog/140-android-manual-check.md`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
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
