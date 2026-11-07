---
id: 207
status: approved
assignee:
claimed:
created: 2026-11-06
---

# Avalonia desktop: плеер видео и аудио в отдельном окне (LibVLC)

## Описание
ADR 0028, п. 2 (решение владельца 2026-11-07; план переписан после блокировки). Play в карточке видео или аудио Avalonia на Linux, Windows, macOS открывает отдельное окно плеера на LibVLC. В ленте плеера нет: нативный `VideoView` в прокручиваемом списке может обрезаться и мигать. Android-голова — отдельно, задача 224.

Пробник 206 (`poc/backlog/206-libvlc-avalonia12/`): `LibVLCSharp.Avalonia` 3.10.2 с `VideoView` собирается на Avalonia 12.1.4, файл проигрывается (`Playing`, `EndReached`, `Length=2000 ms`); на Linux нужны `libvlc5` и `vlc-plugin-base`; `VideoLAN.LibVLC.Windows` 3.0.24 — ~112 МБ для `win-x64`.

Зависит от: 219 (`IMediaPlayerService`, `PlayMediaCommand`, Play в карточке), 223 (аудио-карточка) — для аудио-части; видео-часть можно делать после 219.
Среда: Linux (сборка, headless-тесты); Windows/macOS — CI; визуальная проверка — 046.

## Критерии готовности
- `AvaloniaMediaPlayerService : IMediaPlayerService` (`ImageGen.Avalonia/Services/`, только desktop — не подключать в Android-голову): `IsSupported` = удалось инициализировать LibVLC (`Core.Initialize()` один раз, лениво, при первом `ShowAsync`; при ошибке — `false`, одна Warning, карточка прячет Play). Один открытый плеер: повторный `ShowAsync` закрывает предыдущее окно.
- `MediaPlayerWindow` (по образцу `ImageViewerWindow`: владелец — главное окно, размер по владельцу): `VideoView` на всё окно для видео, для аудио — без `VideoView`, только панель; панель снизу — Play/Pause (≥ 48×48), полоса позиции (перемотка), время «0:03 / 0:08», громкость; Escape и Close закрывают; Space — пауза. Воспроизведение начинается сразу. При закрытии — `Stop()`, освобождение `MediaPlayer` и `Media`. Один общий `LibVLC` на приложение, освобождается при выходе.
- Пакеты: `LibVLCSharp`, `LibVLCSharp.Avalonia` (3.10.2, как в 206) в `ImageGen.Avalonia.csproj`; нативные — `VideoLAN.LibVLC.Windows` (условие `RuntimeIdentifier` начинается с `win` или ОС Windows) и `VideoLAN.LibVLC.Mac` (для `osx-*`; версию взять с nuget.org); Linux — системная libvlc. В `release.yml` пакеты Avalonia желательно собирать с одной нужной архитектурой libvlc (если пакет кладёт все — записать размер в «Отчёт» и в README).
- Лицензия LibVLC (LGPL-2.1-or-later) — файл в `Assets/Licenses/` и упоминание в README.
- README и `docs/knowledge/dev-environment.md`: на Linux для воспроизведения нужны `libvlc5` и `vlc-plugin-base`; без них Play скрыт.
- Регистрация в `AppServices.Desktop.cs` вместо заглушки 219.
- Headless-тесты: служба с движком-заглушкой (интерфейс `IMediaEngine` над созданием `MediaPlayer`) открывает окно, второй `ShowAsync` закрывает первое, закрытие освобождает плеер; ошибка инициализации → `IsSupported = false` и Warning.
- Пункт 046: видео и аудио открываются, играют, перематываются; Escape закрывает; на Linux без libvlc Play не показан.
- Пробник 206 удалить после переноса (`poc/README.md`).

## Анализ
`ImageGen/ImageGen.Avalonia/Services/AvaloniaImageViewerService.cs` (окно-владелец, `ShowDialog`, закрытие), `ImageGen/ImageGen.Avalonia/Composition/AppServices.Desktop.cs` (регистрация), `ImageGen.Avalonia.csproj` (пакеты), `.github/workflows/release.yml` (сборка Avalonia по RID; правка workflow по этой задаче разрешена поручением владельца «с билдами» 2026-11-06 только в части пакетов Avalonia). Android-голова подключает `Views/**` и часть `Services/` ссылками — новый файл службы и окна туда **не** добавлять (проверить список ссылок в `ImageGen.Avalonia.Android.csproj`; если `Views/**` подхватит окно — положить окно в `Services/`).

## План
1. Интерфейс движка и падающие headless-тесты службы.
2. `AvaloniaMediaPlayerService`, `MediaPlayerWindow`, движок на LibVLC.
3. Пакеты с условиями по ОС; сборка desktop и Android-головы (Android не должна тянуть LibVLC).
4. Регистрация; лицензия; README и dev-environment; пункт 046; удаление пробника.

Файлы: `ImageGen/Directory.Packages.props`, `ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj`, `ImageGen/ImageGen.Avalonia/Services/AvaloniaMediaPlayerService.cs` (новый), `ImageGen/ImageGen.Avalonia/Services/MediaPlayerWindow.cs` (новый), `ImageGen/ImageGen.Avalonia/Services/IMediaEngine.cs` (новый), `ImageGen/ImageGen.Avalonia/Composition/AppServices.Desktop.cs`, `ImageGen/ImageGen.Avalonia/Assets/Licenses/` (LibVLC), `ImageGen/ImageGen.Avalonia.Tests/Services/AvaloniaMediaPlayerServiceTests.cs` (новый), при необходимости `.github/workflows/release.yml`, `README.md`, `docs/knowledge/dev-environment.md`, `tasks/backlog/046-desktop-manual-check.md`, удаление `poc/backlog/206-libvlc-avalonia12/`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj
dotnet publish ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj -c Release -r linux-x64 --self-contained -o tmp/207-linux
dotnet build ImageGen/ImageGen.Avalonia.Android/ImageGen.Avalonia.Android.csproj -c Release
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
Создано по поручению владельца («распланируй звук и видео»; выбрано: всё через OpenRouter, LibVLC в Avalonia сразу). ADR 0027, исследование — `docs/knowledge/media-generation.md`. План для Luna, ждёт одобрения.

### 2026-11-06, владелец (в чате, записал claude-audit)
План одобрен: «заапрувь все таски которые будут».

### 2026-11-06, codex-gpt6
Перед реализацией обнаружено расхождение плана с поддерживаемыми платформами: README официального `LibVLCSharp.Avalonia` 3.10.2 перечисляет только Windows, macOS и Linux; Android не поддерживается. Пробник 206 собрал Android-пакет `VideoLAN.LibVLC.Android`, но не подключал и не проверял Avalonia `VideoView` на Android. Текущий план 207 требует одну `MediaPlayerView` и сборку Android-головы, поэтому нельзя считать desktop `VideoView` рабочим Android-путём. Нужен пересмотр плана: Android-рендер через LibVLC callbacks в Avalonia bitmap с отдельной проверкой, другой Android-плеер либо desktop-only плеер. До одобрения нового плана код не менял. Источник: https://github.com/videolan/libvlcsharp/blob/3.x/src/LibVLCSharp.Avalonia/README.md (раздел Supported platforms).

### 2026-11-07, claude-audit
План переписан по решению владельца («давай андроидовский встроенный плеер использовать», ADR 0028): здесь — только desktop, LibVLC в отдельном окне (не в ленте); Android — задача 224. Блокировка снята; одобрено ранее данным поручением «заапрувь все таски которые будут».
