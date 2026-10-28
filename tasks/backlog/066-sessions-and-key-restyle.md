---
id: 066
status: planned
assignee:
claimed:
created: 2026-10-28
---

# Список сессий и экран ключа в новом стиле

## Описание
Перевести на дизайн-систему экран списка сессий и экран ключа OpenRouter (макеты «Список сессий» и «Ключ OpenRouter»). На широком окне (≥ 840) список сессий — левая колонка 272 px, справа пустое состояние «What do you want to draw?» с кнопкой «New session»; на узком — только список. Экран ключа — карточка по центру с состояниями: проверка, ключ отклонён, нет сети, хранилище недоступно (жёлтая плашка).

Справочник: `docs/design/implementation.md` (ключи ресурсов, значения, шрифты, значки — дословно оттуда); токены: `docs/design/tokens.json`; макеты: `docs/design/prototype.html` (открыть: `python3 -m http.server` в `docs/design/`, затем http://localhost:8000/prototype.html; опубликованная версия — https://claude.ai/artifact/M9QzPf39KLkRFTnYBtCi6F); решение: ADR 0014.

Зависит от: 063, 064.
Среда: Linux или Windows (Avalonia — headless-тесты на Linux; MAUI — сборка Android на Linux с Android SDK, Windows-цель собирает CI; запуск под Windows — ручная проверка 016, Avalonia на рабочем столе — 046).

## Критерии готовности
- Строки списка: название (одна строка с многоточием), под ним время — шрифт `IgFontMeta`; выбранная строка — фон `IgAccentSoftBrush`; группы «Today» и «Earlier».
- Удаление: на десктопе — значок корзины при наведении на строку (Avalonia и MAUI Windows/Mac) с доступным именем; на телефонах — `SwipeView` (iOS, Android) с красной кнопкой Delete. Подтверждение — как сейчас (`IDialogService.ConfirmAsync`).
- Экран ключа: заголовок «Connect OpenRouter», поле пароля, ошибка под полем (`TextBox.error` / красная рамка), кнопка «Check and save» (`primary`), плашка о недоступном хранилище с фоном `IgWarnSoftBrush`.
- Явные `MinHeight="44"` в этих экранах убраны: высоту задают стили (`IgControlHeight`).
- Тесты из «Плана» проходят, тест доступности Avalonia проходит.

## Анализ
- Разметка: Avalonia `Views/SessionsView.axaml`, `Views/ApiKeyView.axaml`; MAUI `Pages/SessionsPage.xaml`, `Pages/ApiKeyPage.xaml`.
- Модель: `SessionsPageModel.Sessions` (`SessionItemViewModel`: `Title`, `UpdatedText`, `OpenCommand`, `DeleteCommand`). Группы «Today/Earlier» требуют даты: добавить в `SessionItemViewModel` свойство `IsToday` (по `TimeProvider`; если `SessionsPageModel` ещё не получает `TimeProvider`, добавить его в конструктор — проверить, зарегистрирован ли `TimeProvider.System` в `CoreServiceCollectionExtensions`), группировку делать в разметке нельзя без группированной коллекции — сделать `SessionsPageModel.Groups` (`ObservableCollection<SessionGroupViewModel>` с `Title` и `Items`) или две коллекции `Today` и `Earlier`. План выбирает две коллекции: проще для обеих оболочек.
- Ширина: Avalonia — `Grid` с двумя колонками и обработчик `SizeChanged` в code-behind, который при ширине < 840 скрывает правую колонку (простое и проверяемое решение; `ContainerQuery` Avalonia 11+ не используем, если не уверен). MAUI — `AdaptiveTrigger MinWindowWidth="840"` в `VisualStateManager` (`VisualStateGroup` «Width»): состояние `Wide` показывает правую колонку.
- `ApiKeyPageModel`: `StatusText`, `PersistenceWarning`, `IsBusy`. Чтобы различать ошибку и просто статус, добавить `HasError` (true, когда ключ отклонён или нет сети) — тесты в `ApiKeyPageModelTests`.
- Строки новые: `SessionsToday` = "Today", `SessionsEarlier` = "Earlier", `EmptyChatTitle` = "What do you want to draw?", `EmptyChatHint` = "Describe an image. The assistant writes the prompt and runs the models of this session.", `ApiKeyHeading` = "Connect OpenRouter".

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов, ключей и строк переноси дословно. Если шаг не получается так, как написано (компилятор не принимает разметку, API другое), не придумывай обход: опиши в «Комментариях», что пробовал и что получилось, поставь `blocked` и остановись.

1. Presentation: `SessionItemViewModel.IsToday` (сравнение локальной даты обновления с `TimeProvider.GetLocalNow().Date`); `SessionsPageModel.Today` и `Earlier` (заполняются вместе с `Sessions`; `Sessions` оставить для совместимости тестов); `ApiKeyPageModel.HasError`; новые строки. Тесты: `Load_SplitsTodayAndEarlier` (время подменено своим наследником `TimeProvider` в `TestDoubles/`: пакета `Microsoft.Extensions.TimeProvider.Testing` в `Directory.Packages.props` нет, новые пакеты не добавлять), `Delete_RemovesFromGroup`, `Save_Rejected_SetsHasError`, `Save_Ok_ClearsHasError`.
2. Avalonia `SessionsView.axaml`: колонки 272 и `*`; слева — кнопка `primary` «New session» со значком `Plus`, `TextBlock.section` «Today»/«Earlier», `ItemsControl` строк (`Button` без рамки на всю ширину, внутри `Grid`: название и `TextBlock.meta` с временем; кнопка-значок `Delete` видна только при `:pointerover` строки — стиль `Grid.session-row:pointerover Button.row-delete { IsVisible: True }`); справа — пустое состояние (заголовок `heading`, подсказка `muted`). `SizeChanged` в `.axaml.cs`: ширина < 840 → правая колонка `Width=0` и `IsVisible=false`.
3. Avalonia `ApiKeyView.axaml`: `Border` по центру, ширина ≤ 440, фон `IgSurfaceBrush`, рамка `IgLineBrush`, радиус `IgRadiusLg`, отступы 28; `TextBox.error` при `HasError` (`Classes.error="{Binding HasError}"`); плашка — `Border` с фоном `IgWarnSoftBrush`, значок `Info` цветом `IgWarnBrush`, видима при непустом `PersistenceWarning`.
4. MAUI `SessionsPage.xaml`: то же на `StyleClass`; `SwipeView` с `SwipeItem` Delete (фон `IgErrColor`) для телефонов; на Windows и Mac — значок удаления в строке, видимый всегда (наведение в `CollectionView` MAUI ненадёжно — записать в «Отчёт», если удалось через `PointerGestureRecognizer`). `AdaptiveTrigger` для правой колонки.
5. MAUI `ApiKeyPage.xaml`: та же карточка (`Border` + `RoundRectangle` `IgRadiusLg`).
6. Тесты Avalonia: `Flows/StartFlowTests` и `ViewsAccessibilityTests` должны проходить без правок логики; новый `Views/SessionsViewTests.cs`: `Narrow_HidesEmptyState` (окно 600), `Wide_ShowsEmptyState` (окно 1100), `DeleteButton_HasAutomationName`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android -p:AndroidSdkDirectory=$ANDROID_HOME
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Presentation/PageModels/{SessionsPageModel,SessionItemViewModel,ApiKeyPageModel}.cs`, строки, `ImageGen/ImageGen.Avalonia/Views/{SessionsView,ApiKeyView}.*`, `ImageGen/ImageGen.Maui/Pages/{SessionsPage,ApiKeyPage}.*`, тесты

## Отчёт

## Комментарии
