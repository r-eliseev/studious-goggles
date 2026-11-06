---
id: 207
status: in-progress
assignee: codex-gpt6
claimed: 2026-11-06T23:15:05Z
created: 2026-11-06
---

# Avalonia: плеер видео и аудио в карточке (LibVLC)

## Описание
ADR 0027, п. 7. Плеер в карточке Avalonia (десктоп и Android-голова ADR 0026) на LibVLC — по пути, который рекомендовал пробник 206.

Зависит от: 204, 206 (и 195 — для Android-части). **Перед началом прочитать «Отчёт» 206**: если он рекомендует путь, отличный от `VideoView`, или отказ от LibVLC, этот план не исполнять, а записать в «Комментарии», что план нужно переписать.
Среда: Linux (сборка, headless-тесты); Android SDK; Windows/macOS — CI.

## Критерии готовности
- `MediaPlayerView : UserControl` в `ImageGen/ImageGen.Avalonia/Views/` (подключается в Android-голову ссылкой, как остальные `Views/**`): кнопки Play/Pause (≥ 48×48), полоса позиции, время «0:03 / 0:08», для видео — область изображения 264×176 (`VideoView` или запасной путь из 206), для аудио — только панель. Свойство `FilePath`; воспроизведение не начинается само.
- Один общий `LibVLC` на приложение (служба `AvaloniaMediaEngine` в `ImageGen.Avalonia/Services/`, создаётся лениво при первом воспроизведении, освобождается при закрытии приложения); у каждой карточки свой `MediaPlayer`, освобождается при удалении карточки из дерева (`DetachedFromVisualTree`). Одновременно играет одна карточка: запуск новой ставит предыдущую на паузу.
- Ошибка инициализации libvlc (нет нативной библиотеки) — карточка показывает «Player unavailable» (ресурс) и одна Warning в журнале; приложение не падает.
- Пакеты: `LibVLCSharp` и (если путь 206 — `VideoView`) `LibVLCSharp.Avalonia` в `ImageGen.Avalonia.csproj`; нативные — `VideoLAN.LibVLC.Windows` (Windows), `VideoLAN.LibVLC.Mac` (macOS) с условием по RID/ОС; Android-голова — `VideoLAN.LibVLC.Android`. Версии — как в пробнике 206.
- Linux: системная libvlc; `README.md` (таблица пакетов) и `docs/knowledge/dev-environment.md` называют `apt`-пакеты из отчёта 206. Лицензия LibVLC (LGPL-2.1+) — в папке лицензий пакета (`Assets/Licenses/`, как шрифты).
- Headless-тесты: `MediaPlayerView` без файла показывает выключенный Play; с недоступным движком — «Player unavailable»; с движком-заглушкой (интерфейс над `MediaPlayer`) Play → состояние «играет», вторая карточка ставит первую на паузу.
- Пункты ручной проверки: 046 (Linux/Windows desktop) и 198 (Android): видео и аудио играют, пауза, перемотка, звук.

## Анализ
Карточка Avalonia — `ChatView.axaml` (шаблон генерации; видео-шаблон из 204). Ссылки Android-головы — `ImageGen.Avalonia.Android.csproj` (195): `MediaPlayerView` попадёт туда через `Views/**`, `AvaloniaMediaEngine` нужно добавить в список ссылок явно. Пробник 206 — `poc/backlog/206-libvlc-avalonia12/`.

## План
1. Интерфейс `IMediaPlayback` над `MediaPlayer` (Play, Pause, Seek, Length, Time, события) и реализация на LibVLC; заглушка для тестов.
2. Падающие headless-тесты `MediaPlayerViewTests`.
3. `AvaloniaMediaEngine`, `MediaPlayerView`, вставка в шаблоны видео/аудио `ChatView.axaml`.
4. Пакеты и условия по ОС; сборка desktop и Android-головы.
5. README, dev-environment, лицензия, пункты 046 и 198. После — удалить папку пробника 206.

Файлы: `ImageGen/Directory.Packages.props`, `ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj`, `ImageGen/ImageGen.Avalonia/Services/AvaloniaMediaEngine.cs`, `ImageGen/ImageGen.Avalonia/Services/IMediaPlayback.cs`, `ImageGen/ImageGen.Avalonia/Views/MediaPlayerView.axaml(.cs)`, `ImageGen/ImageGen.Avalonia/Views/ChatView.axaml`, `ImageGen/ImageGen.Avalonia/Assets/Licenses/` (LibVLC), `ImageGen/ImageGen.Avalonia.Android/ImageGen.Avalonia.Android.csproj`, `ImageGen/ImageGen.Avalonia.Tests/Views/MediaPlayerViewTests.cs`, `README.md`, `docs/knowledge/dev-environment.md`, `tasks/backlog/046-desktop-manual-check.md`, `tasks/backlog/198-avalonia-android-manual-check.md`, удаление `poc/backlog/206-libvlc-avalonia12/`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj
dotnet build ImageGen/ImageGen.Avalonia.Android/ImageGen.Avalonia.Android.csproj -c Release
dotnet format ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj --verify-no-changes
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

### 2026-11-06, владелец (в чате, записал claude-audit)
План одобрен: «заапрувь все таски которые будут».
