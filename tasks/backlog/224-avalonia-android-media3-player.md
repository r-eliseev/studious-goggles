---
id: 224
status: approved
assignee:
claimed:
created: 2026-11-07
---

# Avalonia Android: встроенный плеер Android (Media3) для видео и аудио

## Описание
ADR 0028, п. 3 (решение владельца 2026-11-07: «давай андроидовский встроенный плеер использовать»). Play в карточке видео или аудио Android-головы Avalonia открывает отдельную Android-активность с плеером `androidx.media3` (тот же движок ExoPlayer, что у MAUI `MediaElement`, который на телефоне владельца в полноэкранном окне работает). LibVLC в Android-голову не подключается.

Зависит от: 219 (`IMediaPlayerService`, `PlayMediaCommand`, Play в карточке); 223 — для аудио-карточки.
Среда: Linux с Android SDK и workload `android` (сборка); проверка на телефоне — 198.

## Критерии готовности
- Пакеты `Xamarin.AndroidX.Media3.ExoPlayer` и `Xamarin.AndroidX.Media3.Ui` 1.11.1 (MIT AND Apache-2.0, цель `net10.0-android36.0`) в `Directory.Packages.props` и `ImageGen.Avalonia.Android.csproj`. Если восстановление конфликтует с версиями AndroidX, которые тянет `Avalonia.Android`, — взять ближайшую совместимую версию и записать в «Отчёт».
- `MediaPlayerActivity` (`ImageGen.Avalonia.Android/Platform/`): обычная Android-активность (не Avalonia), тема без панели действий, чёрный фон; `PlayerView` на весь экран с управлением (`UseController = true`), `ExoPlayer` с `MediaItem.FromUri(file://…)` из extra `path`; для аудио `PlayerView` показывает только панель (по умолчанию так и есть при отсутствии видео); `PlayWhenReady = true`. Системный Back закрывает. `OnStop` — пауза, `OnDestroy` — `Release()`. Поворот экрана не пересоздаёт плеер (`ConfigurationChanges` как у `MainActivity` плюс `ScreenLayout | SmallestScreenSize`) — либо позиция сохраняется в `OnSaveInstanceState` (выбрать и записать).
- Объявление активности — атрибутом `[Activity(Exported = false, …)]`; файл передаётся путём внутри папки данных приложения (своя активность, `FileProvider` не нужен).
- `AndroidMediaPlayerService : IMediaPlayerService` (`IsSupported = true`): `ShowAsync` запускает `MediaPlayerActivity` через текущую `MainActivity` (`MainActivity.Current`, как в `AndroidLogSharer`) и завершается при её закрытии (результат через `StartActivityForResult`/`OnActivityResult` или статическое событие закрытия — выбрать один способ, записать); отмена токена закрывает активность.
- Регистрация в `RegisterAndroid` (`MobileApp.axaml.cs`) вместо заглушки 219.
- Размер Release APK до и после — в «Отчёт» и `docs/knowledge/avalonia-mobile.md`.
- Сборка Android-головы без новых предупреждений (в том числе без XA0141).
- Пункты 198: видео открывается на весь экран, видно изображение и звук, перемотка, пауза, Back закрывает и звук останавливается; аудио играет с панелью; поворот экрана не сбрасывает воспроизведение.

## Анализ
`ImageGen/ImageGen.Avalonia.Android/MainActivity.cs` (`Current`, атрибуты активности), `Platform/AndroidLogSharer.cs` (запуск активности от текущей), `MobileApp.axaml.cs` (`RegisterAndroid`), `ImageGen.Avalonia.Android.csproj`. Пространства имён привязок Media3 в .NET — `AndroidX.Media3.ExoPlayer`, `AndroidX.Media3.UI`, `AndroidX.Media3.Common` (сверить по пакету после восстановления). На Linux вручную проверить нельзя — только сборка.

## План
1. Пакеты; сборка.
2. `MediaPlayerActivity`.
3. `AndroidMediaPlayerService`, регистрация.
4. Размер APK, `docs/knowledge/avalonia-mobile.md`, пункты 198.

Файлы: `ImageGen/Directory.Packages.props`, `ImageGen/ImageGen.Avalonia.Android/ImageGen.Avalonia.Android.csproj`, `ImageGen/ImageGen.Avalonia.Android/Platform/MediaPlayerActivity.cs` (новый), `ImageGen/ImageGen.Avalonia.Android/Platform/AndroidMediaPlayerService.cs` (новый), `ImageGen/ImageGen.Avalonia.Android/MobileApp.axaml.cs`, `docs/knowledge/avalonia-mobile.md`, `tasks/backlog/198-avalonia-android-manual-check.md`.

### Проверка
```
dotnet workload install android
dotnet build ImageGen/ImageGen.Avalonia.Android/ImageGen.Avalonia.Android.csproj -c Release
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet format ImageGen/ImageGen.Avalonia.Android/ImageGen.Avalonia.Android.csproj --verify-no-changes
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
Создано по решению владельца («давай андроидовский встроенный плеер использовать»), ADR 0028. План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
План одобрен ранее данным поручением «заапрувь все таски которые будут».
