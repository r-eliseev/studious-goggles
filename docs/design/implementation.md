# Дизайн: справочник для вёрстки

Рекомендации по платформам и дизайн-система ImageGen. Основано на `docs/design/implementation.md` и `docs/design/implementation.md` (2026-10-28); при расхождении с актуальными кодом и спецификацией сверяйся с ними. Токены `tokens.json` и макеты были исходным материалом выбора направлений и не являются спецификацией разметки.

## Рекомендации платформ

### Компоновка и ширина

- Телефон: одна колонка, основное действие в пределах большого пальца; не переносить плотную настольную панель буквально.
- Планшет и настольное окно: использовать доступную ширину, но ограничивать длинные строки и центрировать формы. На широком экране уместно показывать список сессий и чат одновременно.
- При узком окне элементы должны перестраиваться, а не создавать горизонтальную прокрутку.

### Ввод и доступность

- iOS: Dynamic Type, системная клавиатура и жест возврата; интерактивные цели ориентиром 44 pt.
- Android: системный Back, клавиатура не должна закрывать ввод; интерактивные цели ориентиром 48 dp.
- Windows и Linux desktop: клавиатурная навигация и мышь; интерактивные цели 32 px (минимум 24 px) при достаточном расстоянии.
- Контраст текста — WCAG AA: 4.5:1 для обычного текста, 3:1 для крупного текста и границ элементов управления.
- Доступность проверять с экранным диктором; каждая кнопка имеет осмысленное имя, декоративные значки скрыты от диктора.

### Системные соглашения

| Что | iOS / iPadOS | Android | macOS | Windows | Linux |
|---|---|---|---|---|---|
| Назад | верхняя панель и свайп от края | системная кнопка или жест | ⌘[ | Alt+← | как Windows |
| Основное действие | верхняя панель | верхняя панель или FAB | панель окна, ⌘N | панель окна, Ctrl+N | как Windows |
| Настройки | экран приложения | экран приложения | отдельное окно, ⌘, | экран или окно | как Windows |
| Действия списка | свайп, долгое нажатие | долгое нажатие | правый клик | правый клик | правый клик |
| Диалоги | основная кнопка справа | основная справа | основная справа | основная слева | по среде, обычно как Windows |

Это общие рекомендации, а не утверждение о проверке на каждой ОС. Apple HIG в исследовании сверить с первоисточниками не удалось; уточнить при наличии платформенной среды.

## Дизайн-система

Первоначальное исследование 2026-10-28 предложило три близких направления: A — спокойный инструмент, B — галерея, C — журнал экспериментов. Владелец решил включить все три как переключаемые стили (ADR 0014). Исходные HTML-макеты и снимки удалены; текущие токены остаются в `tokens.json`, а реализованное поведение задают оболочки и тесты.

## 1. Настройки внешнего вида
- Стиль: `Quiet` (A), `Gallery` (B), `Notebook` (C). По умолчанию `Quiet`.
- Тема: `System`, `Light`, `Dark`. По умолчанию `System`.
- Файл `appearance.json` в папке данных: `{"style":"quiet","theme":"system"}` (значения строчными). Неизвестное значение или испорченный файл → значение по умолчанию и предупреждение в журнал.

## 2. Ключи ресурсов (одинаковые в обеих оболочках)
Префикс `Ig`. Ресурсы подключаются через `DynamicResource`, иначе смена стиля и темы не применится без перезапуска.

### 2.1 Цвета
Для каждого токена `color.<theme>.<name>` два ресурса: `Ig<Name>Color` (тип `Color`) и `Ig<Name>Brush` (`SolidColorBrush`). `<Name>` — имя токена с заглавной буквы.

| Токен | Ключи |
|---|---|
| bg | `IgBgColor`, `IgBgBrush` |
| surface | `IgSurfaceColor`, `IgSurfaceBrush` |
| sunken | `IgSunkenColor`, `IgSunkenBrush` |
| fg | `IgFgColor`, `IgFgBrush` |
| muted | `IgMutedColor`, `IgMutedBrush` |
| line | `IgLineColor`, `IgLineBrush` |
| control | `IgControlColor`, `IgControlBrush` |
| accent | `IgAccentColor`, `IgAccentBrush` |
| onAccent | `IgOnAccentColor`, `IgOnAccentBrush` |
| accentSoft | `IgAccentSoftColor`, `IgAccentSoftBrush` |
| ok | `IgOkColor`, `IgOkBrush` |
| err | `IgErrColor`, `IgErrBrush` |
| errSoft | `IgErrSoftColor`, `IgErrSoftBrush` |
| warn | `IgWarnColor`, `IgWarnBrush` |
| warnSoft | `IgWarnSoftColor`, `IgWarnSoftBrush` |
| placeholder | `IgPlaceholderColor`, `IgPlaceholderBrush` |

Значения — поле `$value.hex` в `tokens.json` (`style.<a|b|c>.color.<light|dark>.<name>`). Стиль `a` = Quiet, `b` = Gallery, `c` = Notebook.

### 2.2 Размеры и шрифты стиля

| Ключ | Тип (Avalonia / MAUI) | Quiet | Gallery | Notebook |
|---|---|---|---|---|
| `IgRadiusSm` | `CornerRadius` / `CornerRadius` | 6 | 12 | 3 |
| `IgRadiusMd` | то же | 8 | 18 | 4 |
| `IgRadiusLg` | то же | 12 | 24 | 6 |
| `IgRadiusControl` | то же | 6 | 999 | 3 |
| `IgCardGap` | `double` / `x:Double` | 12 | 16 | 10 |
| `IgFontText` | `FontFamily` / `x:String` (псевдоним) | системный (см. 4) | Onest | IBM Plex Sans |
| `IgFontMeta` | то же | системный | Onest | IBM Plex Mono |

### 2.3 Шкала шрифтов и высота элементов (по платформе, общая для стилей)

| Ключ | Десктоп (Avalonia; MAUI WinUI и MacCatalyst) | iOS | Android |
|---|---|---|---|
| `IgFontSizeCaption` | 12 | 12 | 12 |
| `IgFontSizeMeta` | 12 | 13 | 13 |
| `IgFontSizeBody` | 14 (Gallery — 15) | 17 | 16 |
| `IgFontSizeTitle` | 15 | 17 | 16 |
| `IgFontSizeHeading` | 20 | 22 | 22 |
| `IgFontSizeDisplay` | 24 | 28 | 28 |
| `IgControlHeight` | 32 | 44 | 48 |

`IgFontSizeBody` на десктопе зависит от стиля (`style.*.size.bodyDesktop`), поэтому лежит в словаре стиля; остальные — в общем словаре. В MAUI значения по платформе задаются через `OnPlatform`.

### 2.4 Варианты разметки как ресурсы
Пять вариантов (`$extensions.com.imagegen.variants`) выражены значениями ресурсов: разметка экранов одна, она читает эти ключи. Классов и веток по стилю в разметке нет.

| Ключ | Тип (Avalonia / MAUI) | Quiet | Gallery | Notebook |
|---|---|---|---|---|
| `IgUserMessageBackgroundBrush` | `IBrush` / `Brush` | `Transparent` | = `IgSunkenBrush` | `Transparent` |
| `IgUserMessageAlignment` | `HorizontalAlignment` / `LayoutOptions` | `Left` / `Start` | `Right` / `End` | `Left` / `Start` |
| `IgUserMessagePadding` | `Thickness` | 0 | 14,10 | 0 |
| `IgUserMessageCornerRadius` | `CornerRadius` | 0 | 18,18,6,18 | 0 |
| `IgUserMessageRoleVisible` | `bool` / `x:Boolean` | True | False | True |
| `IgRoleFont` | как `IgFontText` | = `IgFontText` | = `IgFontText` | = `IgFontMeta` |
| `IgRunBorderThickness` | `Thickness` | 0 | 0 | 1 |
| `IgRunPadding` | `Thickness` | 0 | 0 | 12 |
| `IgPromptBackgroundBrush` | `IBrush` / `Brush` | `Transparent` | = `IgSunkenBrush` | = `IgSunkenBrush` |
| `IgPromptForegroundBrush` | то же | = `IgMutedBrush` | = `IgFgBrush` | = `IgFgBrush` |
| `IgPromptPadding` | `Thickness` | 0 | 10,8 | 10,8 |
| `IgPromptFontStyle` | `FontStyle` / `FontAttributes` | `Italic` | `Normal` / `None` | `Normal` / `None` |
| `IgPromptFont` | как `IgFontText` | = `IgFontText` | = `IgFontText` | = `IgFontMeta` |
| `IgCardBorderThickness` | `Thickness` / `x:Double` | 1 | 0 | 1 |
| `IgCardBackgroundBrush` | `IBrush` / `Brush` | = `IgSurfaceBrush` | `Transparent` | = `IgSurfaceBrush` |
| `IgCardImageCornerRadius` | `CornerRadius` | 0 | = `IgRadiusMd` (18) | 0 |
| `IgCardShowFullMeta` | `bool` / `x:Boolean` | True | False | True |
| `IgCardShowShortMeta` | `bool` / `x:Boolean` | False | True | False |
| `IgCardShowOverlay` | `bool` / `x:Boolean` | False | True | False |

`IgCardShowShortMeta` — обратное значение `IgCardShowFullMeta`: под картинкой в Gallery короткий блок (модель и цена), в Quiet и Notebook он скрыт в пользу полного блока.

«= Ключ» значит то же значение, что у названного ключа в этом же словаре (цвет зависит от темы). В Notebook подпись автора на макете строчными; в оболочках регистр не меняем (у текстовых элементов нет такого свойства), меняется только шрифт.

## 3. Шрифты
Файлы TTF, лицензия OFL 1.1 (файл `OFL.txt` кладётся рядом со шрифтами). Источник — репозиторий Google Fonts; там переменные шрифты, статические начертания режутся инструментом `fonttools` (Python), см. задачу на ресурсы оформления.

| Файл | Семейство (имя внутри файла) | Откуда | Ось |
|---|---|---|---|
| `Onest-Regular.ttf` | Onest | `https://raw.githubusercontent.com/google/fonts/main/ofl/onest/Onest%5Bwght%5D.ttf` | `wght=400` |
| `Onest-Medium.ttf` | Onest Medium | то же | `wght=500` |
| `Onest-SemiBold.ttf` | Onest SemiBold | то же | `wght=600` |
| `IBMPlexSans-Regular.ttf` | IBM Plex Sans | `https://raw.githubusercontent.com/google/fonts/main/ofl/ibmplexsans/IBMPlexSans%5Bwdth,wght%5D.ttf` | `wght=400 wdth=100` |
| `IBMPlexSans-Medium.ttf` | IBM Plex Sans Medium | то же | `wght=500 wdth=100` |
| `IBMPlexSans-SemiBold.ttf` | IBM Plex Sans SemiBold | то же | `wght=600 wdth=100` |
| `IBMPlexMono-Regular.ttf` | IBM Plex Mono | `https://raw.githubusercontent.com/google/fonts/main/ofl/ibmplexmono/IBMPlexMono-Regular.ttf` | уже статический |
| `IBMPlexMono-Medium.ttf` | IBM Plex Mono Medium | `https://raw.githubusercontent.com/google/fonts/main/ofl/ibmplexmono/IBMPlexMono-Medium.ttf` | уже статический |

Лицензии: `https://raw.githubusercontent.com/google/fonts/main/ofl/onest/OFL.txt`, `.../ofl/ibmplexsans/OFL.txt`, `.../ofl/ibmplexmono/OFL.txt`. Проверено 2026-10-28: все адреса отвечают 200; `fonttools varLib.instancer --update-name-table` даёт правильные имена начертаний.

Псевдонимы MAUI (`ConfigureFonts`): `OnestRegular`, `OnestMedium`, `OnestSemiBold`, `PlexSansRegular`, `PlexSansMedium`, `PlexSansSemiBold`, `PlexMonoRegular`, `PlexMonoMedium`, `LucideIcons`.

## 4. Системный шрифт (Quiet)
- Avalonia: ресурс `IgFontText` = `FontFamily` `$Default` (системный шрифт по умолчанию на каждой ОС; на Linux — то, что даёт fontconfig). Новых пакетов шрифтов не добавляем (новый пакет требует ADR, `docs/conventions.md`).
- MAUI: системный шрифт платформы (свойство `FontFamily` не задаётся, ресурс `IgFontText` = пустая строка).

## 5. Значки
Шрифт Lucide (`lucide-static@1.56.0`, лицензия ISC, файл `package/font/lucide.ttf` из `https://registry.npmjs.org/lucide-static/-/lucide-static-1.56.0.tgz`; лицензия — `package/LICENSE`). Значок — текстовый элемент со шрифтом `LucideIcons` и символом из таблицы. Цвет — как у текста. Значок рядом с подписью скрыт от диктора; кнопка из одного значка получает доступное имя.

| Имя константы (`IconGlyphs`) | Lucide | Код |
|---|---|---|
| `Plus` | plus | U+E13D |
| `Search` | search | U+E151 |
| `Send` | arrow-up | U+E04A |
| `Attach` | paperclip | U+E12D |
| `Parameters` | sliders-horizontal | U+E29A |
| `Models` | layers | U+E529 |
| `More` | ellipsis | U+E0B6 |
| `Repeat` | repeat | U+E146 |
| `NewSeed` | dices | U+E2C5 |
| `Edit` | pencil | U+E1F9 |
| `Describe` | eye | U+E0BA |
| `PromptFromImage` | type | U+E198 |
| `Copy` | copy | U+E09E |
| `Save` | download | U+E0B2 |
| `Delete` | trash-2 | U+E18E |
| `Key` | key-round | U+E4A3 |
| `Settings` | settings | U+E154 |
| `Queued` | clock | U+E087 |
| `Error` | circle-alert | U+E077 |
| `Filtered` | shield-minus | U+E518 |
| `Refresh` | refresh-cw | U+E145 |
| `Image` | image | U+E0F6 |
| `BackIos` | chevron-left | U+E06E |
| `Forward` | chevron-right | U+E06F |
| `Expand` | chevron-down | U+E06D |
| `Back` | arrow-left | U+E048 |
| `Close` | x | U+E1B2 |
| `Check` | check | U+E06C |
| `Info` | info | U+E0F9 |
| `Minus` | minus | U+E11C |

## 6. Ширина окна
- Широкий экран — ширина окна ≥ 840: список сессий слева (272), справа чат; модели и параметры — третья колонка (360).
- Узкий — < 840: по одному экрану, как сейчас.
- Шапка чата: название в одну строку с многоточием, чипы переносятся на следующую строку; на узком экране MAUI название в шапке скрыто — его показывает панель навигации (задача 098). В Avalonia название остаётся всегда.

## 7. Порядок кнопок в диалогах
- Windows и Linux: основная кнопка первой (слева), «Cancel» после неё.
- macOS: «Cancel» слева, основная справа.
- iOS и Android в MAUI: системные диалоги (`DisplayAlert`, `DisplayActionSheet`, `DisplayPromptAsync`) — порядок задаёт платформа.

## 8. Проверка соответствия
Значения в словарях оболочек должны совпадать с `tokens.json`. Задачи на тему добавляют тест, который читает `docs/design/tokens.json` и сравнивает с загруженными ресурсами (Avalonia — headless-тест; MAUI — тест на разобранном XAML словарей, см. задачу).


## Исходное исследование платформ (2026-10-28)

Сбор рекомендаций для дизайн-системы и макетов (задача 051). Здесь только то, на что опираются макеты; сами токены и макеты лежат рядом в `docs/design/`.

Метки: «проверено» — взято со страницы первоисточника; «по пересказам» — первоисточник не открылся, цифры взяты из копий и пересказов, сверить с первоисточником до утверждения дизайн-системы.

## Принцип
Общее у всех платформ — токены: цвета, шрифтовая шкала, отступы, радиусы, вид карточек. Своё у каждой платформы — навигация, меню, сочетания клавиш, порядок кнопок в диалогах, жесты. Одинаковые пиксели везде не цель: иначе на iOS и macOS приложение выглядит чужим.

## Раскладка по ширине окна (Material 3, проверено)
- Классы ширины: компактный < 600 dp, средний 600–839 dp, широкий ≥ 840 dp (есть ещё large и extra-large от 1200 dp).
- Типовые раскладки и наши экраны:
  - **list-detail** — список сессий и чат. На широком окне рядом; на компактном и среднем — по одному, «Назад» возвращает к списку. При сужении окна остаётся открытый чат, список прячется.
  - **supporting pane** — чат как основное, модели и параметры сессии во второй панели. На телефоне панель становится отдельным экраном или нижней панелью (sheet).
  - **feed** — сетка карточек: будущая галерея (задача 022) и сравнение (023).
- iPad (по пересказам HIG): горизонтальные классы compact и regular; при Split View на ⅓ экрана приложение получает compact. Это совпадает с классами Material 3, отдельная раскладка для iPad не нужна.

## Приёмы адаптации и отступы (Fluent 2, проверено)
- Приёмы: передвинуть (reposition), изменить размер (resize), перестроить в колонки (reflow), показать или скрыть подробности (show/hide), разделить экран (re-architect). В макете каждого экрана записывается, какой приём на каком классе ширины.
- Шаг сетки 4; ряд отступов: 0, 2, 4, 6, 8, 10, 12, 16, 20, 24, 28, 32, 36, 40, 48, 52, 56. Значения 2, 6, 10 — для выравнивания значков.
- Точки перелома Fluent (для сравнения, мы берём Material 3): 320, 480, 640, 1024, 1366, 1920.
- Размер касания у Fluent: iOS и Web 44×44, Android 48×48.

## Способ ввода важнее ширины
- Широкое окно не значит мышь: планшеты, сенсорные экраны Windows. Плотность и размер элементов зависят от способа ввода, раскладка — от ширины.
- Всё, что появляется при наведении (действия на карточке), на сенсорном экране доступно иначе: видимой кнопкой «…» или долгим нажатием.
- В вебе это `pointer: coarse | fine` и `hover` (web.dev, проверено). В MAUI и Avalonia прямого аналога нет: решаем по платформе (Android, iOS — сенсор; Windows, macOS, Linux — мышь). Неизвестно, как отличить сенсорный экран Windows в Avalonia и MAUI; при вёрстке проверить.

## Размер касания и контраст
- WCAG 2.2: 2.5.8 (AA) — не меньше 24×24 CSS px или достаточный зазор; 2.5.5 (AAA) — 44×44 (проверено по Smashing Magazine и Adrian Roselli).
- Apple, таблица из HIG Accessibility (по пересказам): iOS и iPadOS — по умолчанию 44×44 pt, минимум 28×28; macOS — по умолчанию 28×28 pt, минимум 20×20.
- Android (Material): 48×48 dp.
- Контраст текста — не ниже WCAG AA: 4.5:1 для обычного текста, 3:1 для крупного и для границ элементов управления.
- Решение владельца (2026-10-28, утверждение макетов 051): 44 pt на iOS, 48 dp на Android, на десктопе с мышью 32 (не меньше 24) — `core.target` в `tokens.json`.

## Соглашения платформ
Источник по Apple — пересказы HIG (страницы developer.apple.com не открылись, сверить); по Android — Material 3; по Windows — Fluent 2 и общие соглашения Windows.

| Что | iOS / iPadOS | Android | macOS | Windows | Linux |
|---|---|---|---|---|---|
| Назад | кнопка в верхней панели и свайп от левого края; системной кнопки нет | системная кнопка или жест | история в окне, ⌘[ | история в окне, Alt+← | как Windows |
| Основное действие («New session») | в верхней панели; без FAB | в верхней панели или FAB | панель инструментов окна, ⌘N | панель окна, Ctrl+N | как Windows |
| Настройки и ключ | экран внутри приложения | экран внутри приложения | отдельное окно, ⌘, | экран или окно | как Windows |
| Меню | контекстное меню (долгое нажатие) | контекстное меню | строка меню обязательна: File, Edit, Window | контекстное меню, меню окна по желанию | как Windows |
| Действия над строкой списка | свайп по строке, долгое нажатие | долгое нажатие | правый клик | правый клик | правый клик |
| Порядок кнопок в диалоге | основная справа, «Отмена» слева | основная справа | основная справа | основная слева («OK», «Отмена») | зависит от среды, по умолчанию как Windows |
| Нижняя панель (sheet) | да, с промежуточной высотой, закрытие свайпом | да | sheet прикреплён к окну | диалог | диалог |
| Кнопки окна | — | — | слева сверху, левый верх не занимать | справа сверху | справа сверху (зависит от среды) |
| Плотность | основной текст около 17 pt | основной текст около 14–16 sp | основной текст около 13 pt | основной текст около 14 px | как Windows |
| Размер шрифта по системе | Dynamic Type обязателен | масштаб шрифта системы | нет | масштаб системы | нет |
| Системный шрифт | SF Pro | Roboto | SF Pro | Segoe UI Variable | нет единого (Cantarell, Ubuntu, Noto Sans) — нужен запасной |
| Безопасные зоны | вырез сверху, полоска «домой» снизу | вырезы и жестовая панель | — | — | — |

Технически: строка меню — `NativeMenu` в Avalonia, `MenuBarItem` в MAUI (Mac Catalyst); сочетания клавиш описываются действием один раз, клавиша — по платформе (⌘ на Apple, Ctrl на остальных).

## Значки
- SF Symbols по их лицензии используются только в приложениях для платформ Apple (по памяти, сверить лицензию) — нам не подходят.
- Кандидаты со свободной лицензией: Lucide (ISC), Material Symbols (Apache 2.0), Fluent System Icons (MIT). Lucide и Material Symbols нейтральнее смотрятся на iOS, чем Fluent.

## Шрифты
- Вариант «системный шрифт»: лучше всего работает с Dynamic Type и выглядит родным; на Linux нужен запасной шрифт в комплекте.
- Вариант «свой шрифт» (один на все платформы): нужна лицензия OFL и кириллица (русский интерфейс позже); на iOS масштабирование по Dynamic Type настраивается отдельно.

## Liquid Glass (iOS 26, macOS 26)
Новый визуальный язык Apple (2025). Родные элементы MAUI на iOS, вероятно, перенимают его сами (не проверено). Подделывать его своим рисованием не стоит: на других платформах это чужеродно.

## Шаблоны генеративных интерфейсов
- **Variations** (Shape of AI): несколько вариантов с общим сидом сеткой миниатюр — это наши «N запусков на модель».
- **Regenerate** рядом с результатом; исходный результат не заменяется без подтверждения (Cloudscape) — совпадает с правилом «результат не теряется».
- **Карусель перегенераций**: листать варианты одного запуска (AI UX Playground) — кандидат для просмотра картинки на телефоне.

## Токены
Формат W3C Design Tokens Community Group, стабильная версия 2025.10 (проверено; это отчёт Community Group, не стандарт W3C). Кандидат для хранения токенов в `docs/design/` одним файлом, из которого получаются `ResourceDictionary` обеих оболочек. Нужен ли генератор или токены переносятся вручную — решается в задаче на общие ресурсы темы.

## Источники
- Android: [Canonical layouts](https://developer.android.com/develop/ui/compose/layouts/adaptive/canonical-layouts), [Design for large screens](https://developer.android.com/design/ui/large-screens)
- [Fluent 2: Layout](https://fluent2.microsoft.design/layout)
- Apple HIG: [Layout](https://developer.apple.com/design/human-interface-guidelines/layout), [Designing for iOS](https://developer.apple.com/design/human-interface-guidelines/designing-for-ios), [Designing for macOS](https://developer.apple.com/design/human-interface-guidelines/designing-for-macos), [Accessibility](https://developer.apple.com/design/human-interface-guidelines/accessibility) — не открылись; [форум Apple о размере кнопок macOS](https://developer.apple.com/forums/thread/739201)
- [Smashing Magazine: минимальный размер элемента по WCAG](https://smashingmagazine.com/2024/07/getting-bottom-minimum-wcag-conformant-interactive-element-size/), [Adrian Roselli: Target Size and 2.5.5](https://adrianroselli.com/2019/06/target-size-and-2-5-5.html)
- [web.dev: Interaction](https://web.dev/learn/design/interaction)
- [Shape of AI: Variations](https://www.shapeof.ai/patterns/variations), [Cloudscape: Response regeneration](https://cloudscape.design/gen-ai/patterns/response-regeneration/), [AI UX Playground: Regeneration carousel](https://aiuxplayground.com/pattern/regen-carousel)
- [Design Tokens Format Module 2025.10](https://www.w3.org/community/reports/design-tokens/CG-FINAL-format-20251028/)
