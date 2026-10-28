# Avalonia MAUI Backend (`Avalonia.Controls.Maui`): оценка для проекта

Исследование 2026-10-28: можно ли запускать наш MAUI-код на Linux (и в браузере) через бэкенд Avalonia вместо отдельной оболочки `ImageGen.Avalonia` (ADR 0006). Источники: блог и документация Avalonia, репозиторий проекта, nuget.org, GitHub API (ссылки — в конце). Факты о версиях и issue — на дату исследования; к моменту чтения они могут устареть.

Итог: **сейчас не берём.** Бэкенд — предрелиз, требует .NET 11 и MAUI 11, а наш MAUI-код использует ровно те API, у которых в бэкенде известные ограничения (`MainThread`, Essentials, доступность, Shell). Задачу «Linux-десктоп» уже решает `ImageGen.Avalonia`. Пересмотреть — после стабильного выпуска для .NET 11 и перевода проекта на .NET 11 (раздел «Когда пересматривать»).

## Что это
- **Название.** Проект — `Avalonia.Controls.Maui` от команды AvaloniaUI ([репозиторий](https://github.com/AvaloniaUI/Avalonia.Controls.Maui), лицензия MIT). Пакета `Avalonia.NET.MAUI.Compatibility` нет; встречающееся в пересказах название — путаница с пакетом `Avalonia.Controls.Maui.Compatibility`, который нужен только для устаревших контролов MAUI (`Frame`, `ListView`, `TableView`). Раньше был отдельный проект `AvaloniaMauiHybrid` (встраивание Avalonia в MAUI); его объявили устаревающим после стабильного `Avalonia.Controls.Maui` для .NET 11 ([AvaloniaMauiHybrid](https://github.com/AvaloniaUI/AvaloniaMauiHybrid)).
- **Как работает.** MAUI отделяет описание контрола (виртуальный вид, например `IButton`) от отрисовки (обработчик, handler). Бэкенд подставляет свои обработчики: вместо нативных виджетов создаются контролы Avalonia и рисуются её движком. XAML, привязки, стили, навигация и Shell MAUI остаются — меняется только слой отрисовки ([architecture.md](https://github.com/AvaloniaUI/Avalonia.Controls.Maui/blob/main/docs/architecture.md), [документация Avalonia по миграции](https://docs.avaloniaui.net/docs/migration/maui/)). Форк MAUI не делается: нужные изменения вносятся в официальный `dotnet/maui`. В одном месте, где у MAUI нет публичной точки расширения (`SemanticScreenReader.SetDefault`), используется рефлексия ([architecture.md](https://github.com/AvaloniaUI/Avalonia.Controls.Maui/blob/main/docs/architecture.md)).
- **Подключение** (по [блогу Preview 1](https://avaloniaui.net/blog/maui-avalonia-preview-1)): пакет `Avalonia.Controls.Maui.Desktop`, цель `net11.0` в MAUI-проекте, вызов `UseAvaloniaApp()` в `MauiBuilder`. Загрузчик Avalonia генерируется сам (source generator), его можно расширить или отключить.
- **Два режима** ([architecture.md](https://github.com/AvaloniaUI/Avalonia.Controls.Maui/blob/main/docs/architecture.md)):
  - полный хостинг (`UseAvaloniaApp`) — весь UI MAUI рисует Avalonia; так получаются цели, которых у MAUI нет;
  - встраивание (`UseAvaloniaEmbedding<TApp>`) — контролы Avalonia внутри обычного нативного MAUI-приложения.

## Пакеты
| Пакет | Назначение |
|---|---|
| `Avalonia.Controls.Maui` | обработчики всех встроенных контролов MAUI, `GraphicsView`, `Microsoft.Maui.Graphics` |
| `Avalonia.Controls.Maui.Desktop` | метапакет для десктопа, генерация загрузчика |
| `Avalonia.Controls.Maui.Essentials` | реализации `Microsoft.Maui.Essentials` на Avalonia (частично) |
| `Avalonia.Controls.Maui.Compatibility` | устаревшие контролы `Frame`, `ListView`, `TableView` |
| `Avalonia.Controls.Maui.SkiaSharp.Views` | `SKCanvasView`, `SKGLView` |
| `Avalonia.Controls.Maui.SourceGenerators` | генераторы времени сборки |

## Платформы (по [architecture.md](https://github.com/AvaloniaUI/Avalonia.Controls.Maui/blob/main/docs/architecture.md), 2026-10-28)
| Цель | Встроено в MAUI | Полный хостинг | Встраивание |
|---|---|---|---|
| Linux, Windows, macOS через `Avalonia.Desktop` (`net11.0`) | нет | да | — |
| Браузер (`net11.0-browser`, WebAssembly) | нет | да (single view lifetime) | — |
| Android, iOS, Mac Catalyst | да | пересекается с нативной целью MAUI, пока не основной путь | да |
| Windows WinUI | да | обработчики-заглушки | да |

Для нас это значит: Linux и браузер — да; на Android, iOS и WinUI бэкенд пока не заменяет нативный MAUI, то есть MAUI-оболочка там остаётся как есть.

## Зрелость (проверено 2026-10-28)
- **Стабильной версии нет.** На nuget.org у `Avalonia.Controls.Maui`, `.Desktop` и `.Essentials` по 14 версий, все предрелизные; последняя — `11.0.0-rc.3.26299.205` (GitHub-релиз 2026-10-26, помечен prerelease). Предыдущие: `rc.2` (2026-10-14), `rc.1` (2026-09-09).
- **Зависимости последней версии** (nuspec): `Avalonia` 12.1.4 (как у нас), `Microsoft.Maui.Controls.Core`, `Microsoft.Maui.Graphics`, `Microsoft.Maui.Graphics.Skia` — `11.0.0-rc.2.26505.5`. То есть нужен MAUI 11 RC, а не наш MAUI 10.
- **Цель Avalonia** — стабильный выпуск вместе с .NET 11 ([документация по миграции](https://docs.avaloniaui.net/docs/migration/maui/)). .NET 11 по графику Microsoft выходит 2026-11-10 (STS, [dotnet/core releases](https://github.com/dotnet/core/blob/main/releases.md); запуск на .NET Conf 2026, 10–12 ноября).
- **Репозиторий:** 126 звёзд, 21 открытый issue, активные коммиты (последний push 2026-10-28). Первый анонс — ноябрь 2025 ([блог-анонс](https://avaloniaui.net/blog/net-maui-is-coming-to-linux-and-the-browser-powered-by-avalonia)), отчёт о ходе работ — декабрь 2025 ([progress update](https://avaloniaui.net/blog/avalonia-maui-progress-update)), Preview 1 — март 2026 ([блог Preview 1](https://avaloniaui.net/blog/maui-avalonia-preview-1)).

## Известные ограничения ([known-issues.md](https://github.com/AvaloniaUI/Avalonia.Controls.Maui/blob/main/docs/known-issues.md))
Пересказ на 2026-10-28:
- Blazor Hybrid не реализован.
- Часть API `Microsoft.Maui.Essentials` переопределить нельзя, в том числе **`MainThread`**: вызов уходит в заглушку .NET Standard и бросает `NotSupportedOrImplementedException`. Обход — `Dispatcher` MAUI.
- Мост доступности между MAUI и Avalonia ограничен.
- `VerticalTextAlignment` не поддерживается (текст прижат к верху); `IWindowOverlay` не поддерживается.
- Полный хостинг на существующих нативных целях MAUI (iOS, Mac Catalyst, Android, WinUI) пока не поддерживается.
- `Microsoft.Maui.Maps` не поддерживается (замена — Mapsui через пакет SkiaSharp).
- `FileResult.OpenReadAsync()` работает только через тип `AvaloniaFileResult` из их FilePicker/MediaPicker.
- Нативные элементы UI нельзя встроить в приложение на полном хостинге (обратное направление — Avalonia в нативном MAUI — работает).

Открытые issue, которые задевают наши экраны ([список issue](https://github.com/AvaloniaUI/Avalonia.Controls.Maui/issues)):
- [#218](https://github.com/AvaloniaUI/Avalonia.Controls.Maui/issues/218) — распознаватели жестов внутри `DataTemplate` у `CollectionView` перестают работать после возврата назад по навигации;
- [#24](https://github.com/AvaloniaUI/Avalonia.Controls.Maui/issues/24) — реализацию `CollectionView` планируют заменить;
- [#25](https://github.com/AvaloniaUI/Avalonia.Controls.Maui/issues/25) — в Shell зашиты фиксированные значения и раскладка кнопок;
- [#241](https://github.com/AvaloniaUI/Avalonia.Controls.Maui/issues/241) — `TypeLoadException` (`SetHitTestVisible`) при запуске.

## Сверка с нашим MAUI-кодом
Что из ограничений бэкенда задевает `ImageGen.Maui` (код на `main`, 2026-10-28):

| Наш код | Что использует | Риск в бэкенде |
|---|---|---|
| `Services/MauiMainThread.cs:8` | `MainThread.BeginInvokeOnMainThread` | известное ограничение: бросает `NotSupportedOrImplementedException`; нужна замена на `Dispatcher` |
| `Services/MauiSecretStore.cs:13,22,26` | `SecureStorage` (Essentials) | реализация `SecureStorage` в `Avalonia.Controls.Maui.Essentials` не проверена; неизвестно, куда она пишет на Linux (в `ImageGen.Avalonia` для этого Secret Service, ADR 0008) |
| `Services/MauiAppPaths.cs:7` | `FileSystem.Current.AppDataDirectory` | неизвестно; закрытый issue [#176](https://github.com/AvaloniaUI/Avalonia.Controls.Maui/issues/176) — про изоляцию папки данных между приложениями |
| `Pages/ApiKeyPage.xaml.cs:21` и 21 `SemanticProperties` в `Pages/*.xaml` | `SemanticScreenReader.Announce`, семантика | мост доступности ограничен, а доступность у нас обязательна (`AGENTS.md`, «Главные правила кода») |
| `Services/ShellNavigationService.cs`, `ShellDialogService.cs`, `AppShell.xaml` | `Shell.Current.GoToAsync`, `DisplayAlertAsync`, `DisplayPromptAsync` | Shell поддерживается, но есть открытый [#25](https://github.com/AvaloniaUI/Avalonia.Controls.Maui/issues/25); диалоги не проверены |
| `Pages/*.xaml`: 5 `CollectionView`, `TapGestureRecognizer` в `SessionsPage.xaml` | лента чата и списки | [#218](https://github.com/AvaloniaUI/Avalonia.Controls.Maui/issues/218) (жесты в шаблоне после возврата назад), [#24](https://github.com/AvaloniaUI/Avalonia.Controls.Maui/issues/24) |
| `Pages/ChatPage.xaml`: `FlexLayout`, `Editor`, `Stepper`, `SearchBar`, `CheckBox`, `ActivityIndicator`, `Border`, `Image` | карточки и формы | обработчики для встроенных контролов заявлены; на нашем коде не проверено |
| `ImageGen.Maui.csproj` | `net10.0-android`, `-ios`, `-maccatalyst`, `-windows…`; `Microsoft.Maui.Controls` 10.0.110 | нужна новая цель `net11.0` и переход всего решения на .NET 11 / MAUI 11 |

## Плюсы и минусы для проекта
Что дало бы:
- одна разметка (MAUI XAML) вместо двух — отпадает правило ADR 0006 «каждая экранная фича в обеих оболочках»;
- браузер через WebAssembly без Vue — потенциальная замена части задач 031–032 (но не серверной части: ключи и прогоны в браузере требуют отдельного решения по безопасности, ADR 0001);
- Linux-сборка MAUI-кода — интерфейс MAUI собирался бы в облачных сессиях и на VPS.

Чего стоило бы:
- переход на .NET 11 (STS: по [таблице выпусков](https://github.com/dotnet/core/blob/main/releases.md) STS-версия .NET 9 поддерживается 24 месяца, а LTS .NET 10 — до 2028-11-14, 36 месяцев) и предрелизные пакеты до стабильного выпуска;
- замена `MainThread` на `Dispatcher`, проверка `SecureStorage`, `FileSystem`, Shell и диалогов;
- потеря того, что уже сделано и проверено в `ImageGen.Avalonia`: ключи в системных хранилищах (ADR 0008), headless-тесты интерфейса (`Avalonia.Headless.XUnit`, 44 теста), свой маршрутизатор;
- риск доступности: у нас это обязательное правило, а у бэкенда — известное ограничение.

## Когда пересматривать
Условия, при которых стоит вернуться к вопросу (решение — за владельцем, `docs/status.md`):
1. Вышел стабильный (не preview/rc) `Avalonia.Controls.Maui` для .NET 11.
2. Проект переходит на .NET 11 (или следующий LTS) по своим причинам.
3. В [known-issues.md](https://github.com/AvaloniaUI/Avalonia.Controls.Maui/blob/main/docs/known-issues.md) сняты ограничения `MainThread` и доступности.

Тогда — пробный код в `poc/` до любого ADR: наш `ImageGen.Maui` с целью `net11.0` и `UseAvaloniaApp()` на Linux; проверить четыре страницы (ключ, сессии, чат, модели), навигацию и диалоги Shell, `SecureStorage` и папку данных, объявления экранного диктора; сравнить с `ImageGen.Avalonia` по тем же сценариям, что в задаче 046.

## Ссылки
- репозиторий, MIT: https://github.com/AvaloniaUI/Avalonia.Controls.Maui
- устройство, режимы, таблица платформ: https://github.com/AvaloniaUI/Avalonia.Controls.Maui/blob/main/docs/architecture.md
- известные ограничения: https://github.com/AvaloniaUI/Avalonia.Controls.Maui/blob/main/docs/known-issues.md
- issue: https://github.com/AvaloniaUI/Avalonia.Controls.Maui/issues
- Preview 1, подключение в четыре шага: https://avaloniaui.net/blog/maui-avalonia-preview-1
- первый анонс: https://avaloniaui.net/blog/net-maui-is-coming-to-linux-and-the-browser-powered-by-avalonia
- отчёт о ходе работ, архитектура обработчиков: https://avaloniaui.net/blog/avalonia-maui-progress-update
- документация Avalonia: бэкенд или миграция, статус: https://docs.avaloniaui.net/docs/migration/maui/
- прежний проект встраивания, устаревает: https://github.com/AvaloniaUI/AvaloniaMauiHybrid
- график выпусков .NET: https://github.com/dotnet/core/blob/main/releases.md
- NuGet: https://www.nuget.org/packages/Avalonia.Controls.Maui

Текст источников пересказан, а не процитирован.
