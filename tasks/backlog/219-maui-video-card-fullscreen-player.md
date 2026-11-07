---
id: 219
status: done
assignee:
claimed:
created: 2026-11-07
---

# MAUI: видео в ленте — Play открывает полноэкранный плеер

## Описание
Отчёт владельца 2026-11-07 (Android, MAUI): при воспроизведении в чате поверх видео постоянно моргает слой управления, само видео не видно (чёрная картинка); звук есть, во встроенном полноэкранном режиме `MediaElement` всё хорошо. Встроенный `MediaElement` внутри `CollectionView` (RecyclerView) на Android рисуется через поверхность видео, которая плохо уживается с прокручиваемым списком; в полноэкранном окне проблемы нет.

Решение: в ленте у видео-карточки нет встроенного плеера; карточка показывает кадр-обложку (задача 220; до неё — значок видео) с длительностью и кнопкой Play, которая открывает отдельную модальную страницу с одним `MediaElement` на весь экран. Аудио-плеер в ленте остаётся (у него нет видеоповерхности).

Зависит от: 218 (тот же шаблон карточки).
Среда: Linux (Presentation, тест разметки, сборка MAUI Android); проверка — 140 (Android), 016 (Windows).

## Критерии готовности
- `IMediaPlayerService.ShowAsync(string filePath, string accessibleLabel, CancellationToken)` в `ImageGen.Presentation/Services/` (по образцу `IImageViewerService`); `GenerationItemViewModel.PlayMediaCommand` (доступна при `(IsVideo || IsAudio) && HasMedia`) вызывает его — через делегат или службу так же, как карточка открывает картинку (`OpenImageCommand` в `ChatPageModel`), выбрать один способ и записать в «Отчёт».
- MAUI `MauiMediaPlayerService` + страница `MauiMediaPlayerPage` (по образцу `MauiImageViewerService`/`MauiImageViewerPage`): чёрный фон, `MediaElement` (`Aspect="AspectFit"`, `ShouldAutoPlay="True"`, `ShouldShowPlaybackControls="True"`), кнопка Close ≥ 48×48, системный Back и Escape (Windows) закрывают; при закрытии `Stop()` и освобождение плеера (`Handler?.DisconnectHandler()` — так, как рекомендует README `CommunityToolkit.Maui.MediaElement` 10.0.0; сверить и записать в `docs/knowledge/maui.md`). Регистрация в `MauiProgram`.
- `ChatPage.xaml`: встроенный видео-`MediaElement` удалён; область видео-карточки — обложка (`Image`, источник `VideoPosterPath` из 220 — до 220 свойство пустое и показывается значок) + значок Play по центру + длительность; вся область — кнопка `PlayMediaCommand` с `SemanticProperties.Description` «Play video» (ресурс).
- `IMediaPlayerService` — общий для видео и аудио (ADR 0028): `bool IsSupported`, `ShowAsync(...)`. Avalonia регистрирует заглушку «not supported» до задач 207 (desktop) и 224 (Android); у карточки Play скрыт, пока `IsSupported = false` (`CanPlayMedia`). `GenerationItemViewModel.PlayMediaCommand` доступна для готового видео и аудио; в MAUI-разметке Play показывается только у видео (аудио играет встроенный плеер ленты).
- Тесты: команда доступна только для готового видео и вызывает службу с путём; тест разметки — в шаблоне карточки нет видео-`MediaElement`, есть кнопка Play.
- Сборка MAUI Android; пункт 140: Play открывает плеер на весь экран, видео видно, Back закрывает, звук останавливается.

## Анализ
`ImageGen/ImageGen.Maui/Pages/ChatPage.xaml` (видео-`MediaElement` ≈175–189, обработчики `OnMediaElementLoaded/Unloaded` в `ChatPage.xaml.cs` — оставить для аудио), `ImageGen/ImageGen.Maui/Services/MauiImageViewerService.cs` (модальная страница, `Shell.Current.Navigation.PushModalAsync`, закрытие одним `TaskCompletionSource`), `ImageGen/ImageGen.Presentation/PageModels/GenerationItemViewModel.cs`, `ChatPageModel.cs` (`OpenImageAsync`). Avalonia-регистрация — `ImageGen/ImageGen.Avalonia/Composition/AppServices.Desktop.cs` и `ImageGen/ImageGen.Avalonia.Android/MobileApp.axaml.cs` (`RegisterAndroid`).

## План
1. Падающие тесты модели и разметки.
2. Интерфейс, команда, ресурсы.
3. MAUI-служба и страница; регистрация; заглушки Avalonia.
4. Шаблон карточки; сборка Android; `docs/knowledge/maui.md`; пункты 140 и 016.

Файлы: `ImageGen/ImageGen.Presentation/Services/IMediaPlayerService.cs` (новый), `ImageGen/ImageGen.Presentation/Services/UnsupportedMediaPlayerService.cs` (новый), `ImageGen/ImageGen.Presentation/PageModels/GenerationItemViewModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/ChatPageModel.cs`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.cs`, `ImageGen/ImageGen.Maui/Services/MauiMediaPlayerService.cs` (новый), `ImageGen/ImageGen.Maui/MauiProgram.cs`, `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, `ImageGen/ImageGen.Avalonia/Composition/AppServices.Desktop.cs`, `ImageGen/ImageGen.Avalonia.Android/MobileApp.axaml.cs`, тесты `ImageGen/ImageGen.Presentation.Tests/` (модели и `Design/MauiChatPageMarkupTests.cs`), `docs/knowledge/maui.md`, `tasks/backlog/140-android-manual-check.md`, `tasks/v0.1/016-skeleton-manual-check.md`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet build ImageGen/ImageGen.Avalonia.Android/ImageGen.Avalonia.Android.csproj -c Release
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
- Добавлены общий `IMediaPlayerService` и неподдерживаемая заглушка для Avalonia; MAUI реализует модальную чёрную страницу с одним `MediaElement`. Закрытие вызывает `Stop()` и `Handler?.DisconnectHandler()`, Back и Escape закрывают страницу. Выбран прямой вызов сервиса из `GenerationItemViewModel`, инъекция сервиса проходит из `ChatPageModel`.
- Видеокарточка теперь показывает постер (путь пока пустой до задачи 220), значок Play и длительность; кнопка доступна только для готового видео при поддерживаемом плеере. Встроенный аудиоплеер сохранён.
- TDD: `VideoCard_UsesPosterAndPlayButtonInsteadOfInlinePlayer` сначала упал из-за встроенного видео MediaElement, затем стал проходить. Добавлены тесты команды: только готовое доступное медиа вызывает сервис с локальным путём.
- `dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj` — 520/520; `dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj` — 180 пройдено, 2 ожидаемо пропущено (Windows DPAPI и отсутствующая D-Bus Secret Service).
- `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android` — 0 предупреждений и ошибок. `dotnet build ImageGen/ImageGen.Avalonia.Android/ImageGen.Avalonia.Android.csproj -c Release --no-restore` — 0 предупреждений и ошибок.
- Форматирование проверено для Presentation, Presentation.Tests и MAUI. Обновлены `docs/knowledge/maui.md`, ручные пункты 140 и 016. README из локального NuGet пакета 10.0.0 не содержит инструкций очистки; это отмечено в knowledge, а вызовы `Stop()` и `DisconnectHandler()` реализованы по плану задачи.
- Ручной прогон Android/Windows недоступен в этой среде и оставлен в задачах 140/016.

## Комментарии

### 2026-11-07, claude-audit
Создано по отчёту владельца 2026-11-07 (лог MAUI Android `ce90792`: видео генерируется, звук есть, полноэкранный режим работает). План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
План одобрен ранее данным поручением «заапрувь все таски которые будут».

### 2026-11-07, claude-audit
По ADR 0028 служба переименована в общую `IMediaPlayerService` (видео и аудио), команда — `PlayMediaCommand`; Avalonia-реализации — 207 (desktop) и 224 (Android).
