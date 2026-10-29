---
id: 095
status: approved
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
- **Что сейчас.** `MainActivity` (`Platforms/Android/MainActivity.cs`) не задаёт `WindowSoftInputMode`; `App` (`App.xaml.cs`) не вызывает `UseWindowSoftInputModeAdjust`; ни одна страница и стиль не задают `SafeAreaEdges`. В `ImageGen.Maui.csproj` `TargetAndroidSdkVersion` не задан — действует значение по умолчанию для `net10.0-android` (в этой среде не проверено: нет Android-нагрузки; шаг 1 записывает его в «Отчёт»).
- **Причина (документация Microsoft, .NET MAUI 10).** Safe area на Android задаётся свойством `SafeAreaEdges` (`None`, `SoftInput`, `Container`, `Default`, `All`). У `ContentPage` по умолчанию `None`, у `Layout` (`Grid` и др.) — `Container`: системные панели учитываются, клавиатура нет. Корневой `Grid ChatLayout` чата поэтому уходит под клавиатуру. `All` — «учитывать все вставки, включая клавиатуру». При `WindowSoftInputModeAdjust.Resize` документация советует `ContentPage.SafeAreaEdges="All"`. Источники: [Safe area layout](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/safe-area?view=net-maui-10.0), [Soft keyboard input mode on Android](https://learn.microsoft.com/en-us/dotnet/maui/android/platform-specifics/soft-keyboard-input-mode) (прочитаны 2026-10-29).
- **Решение.** Атрибут `SafeAreaEdges="All"` на корневом `ContentPage` каждой из семи страниц `ImageGen.Maui/Pages/*.xaml`. Не неявным стилем: в `Styles.xaml` уже есть неявный стиль `Page` (`ApplyToDerivedTypes`, фон), а элементу MAUI достаётся один неявный стиль — стиль `ContentPage` перекрыл бы фон. Атрибут проверяется текстовым тестом, как `MauiStylesTests`. `UseWindowSoftInputModeAdjust(Resize)` не ставим; он — запасной шаг только по итогам проверки на устройстве (шаг 5).
- **Среда.** Сборка Android — Linux с Android SDK; запуск — только на реальном устройстве. На Windows `SafeAreaEdges` ничего не меняет. Avalonia не затронута.

## План
Выполнять после: — (обязательных предшественников нет). Пересечения только текстовые: `ChatPage.xaml` правят 088, 090, 097, 098 (другие участки файла); `SessionModelsPage.xaml` правят 090 (091 снята как дубль). При rebase сохранять обе стороны. 097 зависит от095 явно,098 — транзитивно через097; зависимости записаны в строках «Зависит от». 103 также меняет `SessionsPage.xaml`, но другой участок: сохранять и SafeAreaEdges, и семантические свойства кнопок.

Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов, членов, ключей строк и тестов переноси дословно. Рутинные изменения синтаксиса сверяй с установленным MAUI 10 и документацией. Если само свойство не поддерживается установленной версией, запиши версию/ошибку и реальную неизвестность в «Комментарии»; не заменяй решение произвольными постоянными отступами.

Прочитай перед началом:
- `docs/knowledge/maui.md` (разделы «XAML и привязки», «Клавиатура Android в `Editor`»);
- `ImageGen/ImageGen.Presentation.Tests/Design/MauiStylesTests.cs` (как тесты находят папку `ImageGen.Maui` — метод `MauiRoot()`);
- корневой элемент каждой страницы в `ImageGen/ImageGen.Maui/Pages/` (семь файлов: `ApiKeyPage.xaml`, `ChatPage.xaml`, `ModelPickerPage.xaml`, `SessionModelsPage.xaml`, `SessionParametersPage.xaml`, `SessionsPage.xaml`, `SettingsPage.xaml`).

1. Собрать Android-цель без изменений: `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android`. Найти сгенерированный манифест (`find ImageGen/ImageGen.Maui/obj -name AndroidManifest.xml`) и записать в «Отчёт» значение `android:targetSdkVersion`. Если Android SDK в среде нет — записать «не проверено: нет Android SDK» и продолжать (шаги 2–3 проверяются тестом, шаг 4 — CI).
2. Падающий тест. Новый файл `ImageGen/ImageGen.Presentation.Tests/Design/MauiSafeAreaTests.cs`, класс `MauiSafeAreaTests` (пространство `ImageGen.Presentation.Tests.Design`), со своим закрытым методом `MauiRoot()`, скопированным из `MauiStylesTests`. Тест `Pages_RespectKeyboardSafeArea`: для каждого файла `Pages/*.xaml` разобрать XML (`XDocument.Load`), у корневого элемента (`ContentPage`) атрибут `SafeAreaEdges` равен `All`; в сообщении об ошибке — имя файла. Сейчас тест падает на всех семи файлах.
3. В каждом из семи файлов `ImageGen/ImageGen.Maui/Pages/*.xaml` добавить корневому `ContentPage` атрибут `SafeAreaEdges="All"` (после `x:Class`/`x:DataType`, перед `Title`, если он есть). Другие атрибуты не трогать. Тест шага 2 проходит.
4. Сборка Android и формат: `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android`, `dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes`. Если компилятор XAML не принимает `SafeAreaEdges="All"` на `ContentPage` (ошибка про неизвестное свойство или преобразование) — `blocked` с текстом ошибки в «Комментариях». Если Android SDK нет — эти две команды пропустить и записать в «Отчёт»; Windows-цель проверит CI.
5. Проверка на устройстве — только если в среде есть подключённое Android-устройство (`adb devices` показывает его). Пройти «Ручную проверку» ниже.
   - Все пункты выполнены → записать результат в «Отчёт», перейти к шагу 6.
   - Поле чата всё ещё закрыто клавиатурой → в `ImageGen/ImageGen.Maui/App.xaml.cs`, в конструкторе `App` после `InitializeComponent()`, вызвать для `Application` платформенную настройку `On<Microsoft.Maui.Controls.PlatformConfiguration.Android>().UseWindowSoftInputModeAdjust(WindowSoftInputModeAdjust.Resize)` (пространство `Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific`), собрать и повторить ручную проверку. Если поле всё равно закрыто или появился двойной отступ (поле поднято выше клавиатуры ещё на её высоту) — `blocked`, описать в «Комментариях» версию Android и что видно.
   - Устройства нет → в «Отчёт»: «На устройстве не проверено, проверить владельцу» и список из «Ручной проверки»; шаг 5 на этом закончен.
6. Документация: `docs/knowledge/maui.md`, в раздел «XAML и привязки» после пункта «Клавиши в `Editor` на Windows» — новый пункт «**Клавиатура Android и safe area (.NET 10, задача 095):**»: у `ContentPage` по умолчанию `SafeAreaEdges=None`, у layout — `Container` (клавиатура не учитывается); все страницы приложения ставят `SafeAreaEdges="All"` атрибутом (не неявным стилем: он перекрыл бы стиль `Page`); ссылки на две страницы документации; проверено ли на устройстве и нужен ли был `Resize` (по итогам шага 5, иначе «на устройстве не проверено»).

7. Завершить по `docs/tasks.md`: записать проверенные платформы и ограничения, заполнить «Отчёт», снять занятость через `scripts/release-task.sh 095`, проверить `scripts/check.sh`. Отсутствие телефона не блокирует завершение доступных проверок; сбой доступной Android-сборки блокирует.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

### Ручная проверка
Android (телефон; по возможности Android 15+ и Android 14 или ниже; жестовая и трёхкнопочная навигация). Если не проверено исполнителем — в «Отчёт» как «не проверено, проверить владельцу».
1. Чат: тап по полю ввода → поле, скрепка, строка подсказки и кнопка отправки целиком над клавиатурой; лента сжалась; шапка и верхняя панель навигации на месте, сверху нет лишнего отступа.
2. Набрать длинный текст (поле растёт до 160) → поле и кнопка отправки видны.
3. Закрыть клавиатуру → раскладка как до открытия: снизу нет пустой полосы, поле не под системной панелью навигации.
4. Повернуть экран с открытой клавиатурой → поле видно.
5. Экран ключа API (поле ключа), выбор модели и модели сессии (поиск и список), параметры, настройки → поле с фокусом над клавиатурой, список прокручивается до конца.
Windows (задача `tasks/v0.1/016-skeleton-manual-check.md`, дописать пункт только о Windows): страницы выглядят как раньше (без лишних отступов). Непроверенные Android-сценарии занести в задачу ручной проверки Android 140; Android-пункты не переносить в Windows-only 016.

Файлы: `ImageGen/ImageGen.Maui/Pages/ApiKeyPage.xaml`, `ChatPage.xaml`, `ModelPickerPage.xaml`, `SessionModelsPage.xaml`, `SessionParametersPage.xaml`, `SessionsPage.xaml`, `SettingsPage.xaml` (все в `ImageGen/ImageGen.Maui/Pages/`); `ImageGen/ImageGen.Presentation.Tests/Design/MauiSafeAreaTests.cs` (новый); только по исходу шага 5 — `ImageGen/ImageGen.Maui/App.xaml.cs`; `docs/knowledge/maui.md`; `tasks/v0.1/016-skeleton-manual-check.md`, задача ручной проверки Android 140.

## Отчёт

## Комментарии
### 2026-10-29, claude-plan
- Вопрос владельцу: на каком телефоне и какой версии Android снят скриншот? Нужно, чтобы проверить на той же версии (поведение `SafeAreaEdges` и `Resize` разное на Android 15+ и на 14 и ниже). Допущение в плане: проверяем на доступном устройстве, Android 15+ обязательно.
- Допущение: `SafeAreaEdges = All` на всех страницах (через стиль), а не только в чате: критерии требуют, чтобы клавиатура не закрывала и остальные экраны.

### 2026-10-29, владелец (в чате, записал claude-plan)
План одобрен; допущения из комментариев claude-plan приняты как решения. План переписан под небольшую модель.

### 2026-10-29, codex-review
Актуальное решение для реализации — атрибут `SafeAreaEdges="All"` на корневом `ContentPage`, как в «Анализе» и шаге 3. Оно уточняет прежнее допущение «через стиль»: неявный стиль `ContentPage` не вводится, чтобы сохранить существующий стиль `Page`. Задача нужна; устройства для ручной проверки может не быть, это явно отражается в отчёте.
