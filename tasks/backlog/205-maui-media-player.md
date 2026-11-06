---
id: 205
status: approved
assignee:
claimed:
created: 2026-11-06
---

# MAUI: воспроизведение видео и аудио в карточке (MediaElement)

## Описание
ADR 0027, п. 7. В MAUI-карточке видео и аудио воспроизводятся встроенным плеером `CommunityToolkit.Maui.MediaElement` 10.0.0 (MIT).

Зависит от: 204.
Среда: Linux (сборка Android) и Windows-цель через CI; проверка воспроизведения — ручные 140 (Android) и 016 (Windows).

## Критерии готовности
- Пакет `CommunityToolkit.Maui.MediaElement` 10.0.0 в `ImageGen/Directory.Packages.props` и `ImageGen.Maui.csproj`; `UseMauiCommunityToolkitMediaElement()` в `MauiProgram` (рядом с `UseMauiCommunityToolkit`; точное имя метода и требования к `MainActivity` на Android сверить с README пакета 10.0.0 и записать в `docs/knowledge/maui.md`).
- Видео-карточка: `MediaElement` с `Source` = файл (`MediaSource.FromFile(MediaFilePath)` через конвертер в общем стиле проекта), `ShouldAutoPlay=False`, `ShouldShowPlaybackControls=True`, размер как у области картинки (264×176), `SemanticProperties.Description` — модель и длительность. Аудио-карточка (для 167): тот же элемент высотой под панель управления.
- Плеер не создаётся для неготовой или неуспешной генерации (`IsVisible` по `IsVideo`/`IsAudio` и `HasMedia`).
- При уходе со страницы чата воспроизведение останавливается (в `OnDisappearing` страница вызывает `Stop()` у видимых `MediaElement` или использует штатный способ пакета; описать выбранный в `docs/knowledge/maui.md`).
- Сборка MAUI Android и Windows без предупреждений (Windows — CI).
- Пункты в 140 и 016: видео играет, пауза, перемотка, звук; уход со страницы останавливает.

## Анализ
`ChatPage.xaml` — шаблон карточки (`BindableLayout.ItemTemplate`, `DataTemplate x:DataType="pm:GenerationItemViewModel"`), шаблон видео добавлен в 204. ADR 0018 описывает `CommunityToolkit.Maui`; MediaElement — отдельный пакет, его выбор записан в ADR 0027. Известные ограничения пакета (Android: нужен `AndroidManifest`/Activity-атрибуты для полноэкранного режима) — только по README 10.0.0, не выдумывать.

## План
1. Пакет и регистрация; сборка Android.
2. Конвертер пути в `MediaSource` (`ImageGen/ImageGen.Maui/Converters/FilePathToMediaSourceConverter.cs`) и тест-разметки в `ImageGen.Presentation.Tests/Design/MauiChatPageMarkupTests.cs` (по образцу существующих проверок разметки): у видео-шаблона есть `MediaElement` с `ShouldAutoPlay=False` и описанием доступности.
3. Шаблоны видео и аудио в `ChatPage.xaml`; остановка в `ChatPage.xaml.cs`.
4. `docs/knowledge/maui.md`, пункты в 140 и 016.

Файлы: `ImageGen/Directory.Packages.props`, `ImageGen/ImageGen.Maui/ImageGen.Maui.csproj`, `ImageGen/ImageGen.Maui/MauiProgram.cs`, `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml.cs`, `ImageGen/ImageGen.Maui/Converters/FilePathToMediaSourceConverter.cs` (новый), `ImageGen/ImageGen.Presentation.Tests/Design/MauiChatPageMarkupTests.cs`, `docs/knowledge/maui.md`, `tasks/backlog/140-android-manual-check.md`, `tasks/v0.1/016-skeleton-manual-check.md`.

### Проверка
```
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
bash scripts/check.sh
```
Windows-цель MAUI — CI.

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
