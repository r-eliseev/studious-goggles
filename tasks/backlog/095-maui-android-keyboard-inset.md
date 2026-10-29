---
id: 095
status: planned
assignee:
claimed:
created: 2026-10-29
---

# MAUI Android: клавиатура закрывает поле ввода чата

## Описание
Замечание владельца после проверки релиза v0.1.4 на Android (2026-10-29, скриншот): при открытой экранной клавиатуре поле ввода чата видно лишь наполовину, строка подсказки обрезана, кнопка отправки и скрепка под клавиатурой.

Что известно: в `ImageGen.Maui/Platforms/Android/MainActivity.cs` режим `WindowSoftInputMode` не задан, `ContentPage` чата (`ImageGen.Maui/Pages/ChatPage.xaml`) не меняет размер под клавиатуру (в MAUI это `Application.On<Android>().UseWindowSoftInputModeAdjust(WindowSoftInputModeAdjust.Resize)` или атрибут активности; на Android 15+ с edge-to-edge могут понадобиться отступы по `WindowInsets` IME). Какой способ работает на целевом SDK, выяснить в «Анализе».

Зависит от: —
Среда: Linux или Windows (сборка Android на Linux; ручная проверка — на телефоне или эмуляторе).

## Критерии готовности
- При открытой клавиатуре поле ввода целиком видно над ней, вместе со скрепкой и кнопкой отправки; лента чата сжимается, а не уезжает за верх экрана.
- После закрытия клавиатуры раскладка возвращается.
- Остальные экраны с полями ввода (ключ API, поиск моделей, параметры) тоже не закрываются клавиатурой.
- Ручная проверка на Android описана в «Отчёте»; неочевидное — в `docs/knowledge/maui.md`.

## Анализ
- **Что сейчас.** `MainActivity` (`Platforms/Android/MainActivity.cs`) не задаёт `WindowSoftInputMode`; `App` (`App.xaml.cs`) не вызывает `UseWindowSoftInputModeAdjust`; ни одна страница и стиль не задают `SafeAreaEdges` (поиск по `ImageGen.Maui` пуст). В `ImageGen.Maui.csproj` `TargetAndroidSdkVersion` не задан, значит действует значение по умолчанию для `net10.0-android`. Какое именно (ожидаемо API 36), в этой среде не проверено: Android-нагрузки здесь нет. Сверить в `obj/.../AndroidManifest.xml` после сборки (шаг 1 плана).
- **Почему поле уходит под клавиатуру (документация Microsoft, .NET MAUI 10).** Начиная с .NET 10, safe area на Android задаётся свойством `SafeAreaEdges` (значения `None`, `SoftInput`, `Container`, `Default`, `All`; можно задать отдельно для каждого края в порядке лево, верх, право, низ). У `ContentPage` значение по умолчанию `None` (контент от края до края), у `Layout` (`Grid` и других) — `Container`, то есть системные панели учитываются, а клавиатура нет. У `ScrollView` `SoftInput` не действует: его ставят на обёртку. Это совпадает с симптомом: корневой `Grid ChatLayout` чата учитывает системные панели, а клавиатуру нет. Документация о режиме клавиатуры: при `WindowSoftInputModeAdjust.Resize` в .NET 10 «может понадобиться» `ContentPage.SafeAreaEdges="All"`. Источники: [Safe area layout](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/safe-area?view=net-maui-10.0), [Soft keyboard input mode on Android](https://learn.microsoft.com/en-us/dotnet/maui/android/platform-specifics/soft-keyboard-input-mode) (прочитаны 2026-10-29).
- **Выбранный способ.** Основной — `SafeAreaEdges = All` на страницах: так учитываются и системные панели, и клавиатура. Задаётся одним неявным стилем `ContentPage` (`ApplyToDerivedTypes`) в `Resources/Styles/Styles.xaml`, рядом с существующим стилем `Page`, и сразу покрывает все экраны с полями ввода: `ApiKeyPage` (`Entry`), `ModelPickerPage` и `SessionModelsPage` (`SearchBar`), `SessionParametersPage`, `SettingsPage`, `ChatPage`. Режим `UseWindowSoftInputModeAdjust(Resize)` для `Application` — запасной шаг, если одного `SafeAreaEdges` на устройстве не хватит.
- **Неизвестно (снимается на устройстве):** (1) применяется ли `SafeAreaEdges` через стиль (свойство привязываемое, но на устройстве это не проверено; запасной вариант — атрибут на каждой странице); (2) не дублируется ли верхний отступ под панелью навигации `Shell` при `All`; (3) нужен ли `Resize` при targetSdk 35 и выше (edge-to-edge там обязателен, окно не сжимается, приходят только вставки IME) и не появится ли двойной отступ на Android 14 и ниже, если включить `Resize`. Ради этого `poc/` не нужен: проверка — сборка и запуск этого же приложения на телефоне.
- **Среда.** Сборка Android — Linux с Android SDK (облачная сессия, VPS); запуск — только на реальном устройстве (VPS: `adb connect`) или у владельца (`docs/knowledge/dev-environment.md`). На Windows `SafeAreaEdges` ничего не меняет (в документации безопасные области описаны только для Android, iOS и Mac Catalyst); Windows-цель проверяет CI.
- **TDD.** Логики в `Presentation` нет, правка только в платформенной разметке. Avalonia не затронута: у десктопа нет экранной клавиатуры (Avalonia на мобильных — исследование 094).
- **Пересечения.** 097 и 098 правят `ChatPage.xaml` (другие участки: шапка и лента; эта задача — разве что корневой `Grid`, если стиль не сработает). 091 (липкие кнопки моделей сессии) затрагивает `SessionModelsPage`, на которую подействует новый стиль. Порядок: **095 → 097 → 098**. Сначала 095: без неё на устройстве не проверить прокрутку при открытой клавиатуре (097). 098 правит только шапку и идёт последней.

## План
1. Собрать Android-цель как есть (`dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android`) и записать в «Отчёт» `targetSdkVersion` из сгенерированного `obj/.../AndroidManifest.xml`.
2. `Resources/Styles/Styles.xaml`: неявный стиль `ContentPage` с `ApplyToDerivedTypes`, сеттер `SafeAreaEdges = All`. Если компилятор XAML не принимает стиль или на устройстве он не действует, задать `SafeAreaEdges="All"` атрибутом на каждой странице из `Pages/` (семь файлов).
3. Сборка Android, `dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes` (на Linux с Android SDK).
4. Ручная проверка на Android (по возможности Android 15+ и Android 14 и ниже; жестовая и трёхкнопочная навигация):
   - чат: тап по полю → поле, скрепка, строка подсказки и кнопка отправки целиком над клавиатурой; лента сжимается, шапка и панель навигации `Shell` на месте; набрать длинный текст до `MaximumHeightRequest` 160 → поле по-прежнему видно;
   - закрыть клавиатуру («Назад» или кнопкой клавиатуры) → раскладка прежняя: нет пустой полосы снизу, поле не под системной панелью навигации; сверху нет двойного отступа под заголовком;
   - поворот экрана с открытой клавиатурой;
   - ключ API (`Entry`), выбор модели и модели сессии (`SearchBar` и список под ним), параметры, настройки → поле с фокусом видно над клавиатурой, список прокручивается до конца.
5. Если клавиатура всё ещё закрывает поле: в `App` (конструктор) вызвать `On<Android>().UseWindowSoftInputModeAdjust(WindowSoftInputModeAdjust.Resize)` для `Application` и повторить шаг 4. Отдельно проверить Android 14 и ниже на двойной отступ (поле поднято выше клавиатуры на её высоту).
6. Документация: `docs/knowledge/maui.md` — по умолчанию в .NET 10 у `ContentPage` `None`, у layout `Container`; что сработало на устройстве (стиль или атрибут, нужен ли `Resize`, версии Android). Если устройства нет, пункт ручной проверки дописать в `tasks/v0.1/016-skeleton-manual-check.md` и в «Отчёт».

Проверка:
- `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android`
- `dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes`
- Windows-цель — CI (задача `maui`)
- `scripts/check.sh`

Файлы: `ImageGen/ImageGen.Maui/Resources/Styles/Styles.xaml` (или `ImageGen/ImageGen.Maui/Pages/*.xaml`), при необходимости `ImageGen/ImageGen.Maui/App.xaml.cs`; `docs/knowledge/maui.md`; при необходимости `tasks/v0.1/016-skeleton-manual-check.md`.

## Отчёт

## Комментарии
### 2026-10-29, claude-plan
- Вопрос владельцу: на каком телефоне и какой версии Android снят скриншот? Нужно, чтобы проверить на той же версии (поведение `SafeAreaEdges` и `Resize` разное на Android 15+ и на 14 и ниже). Допущение в плане: проверяем на доступном устройстве, Android 15+ обязательно.
- Допущение: `SafeAreaEdges = All` на всех страницах (через стиль), а не только в чате: критерии требуют, чтобы клавиатура не закрывала и остальные экраны.
