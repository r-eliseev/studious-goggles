# Дизайн: справочник для вёрстки

Как токены `tokens.json` и макеты `prototype.html` переносятся в оболочки MAUI и Avalonia (ADR 0014). Задачи на вёрстку ссылаются на этот файл: имена ключей и значения берутся отсюда дословно.

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

## 7. Порядок кнопок в диалогах
- Windows и Linux: основная кнопка первой (слева), «Cancel» после неё.
- macOS: «Cancel» слева, основная справа.
- iOS и Android в MAUI: системные диалоги (`DisplayAlert`, `DisplayActionSheet`, `DisplayPromptAsync`) — порядок задаёт платформа.

## 8. Проверка соответствия
Значения в словарях оболочек должны совпадать с `tokens.json`. Задачи на тему добавляют тест, который читает `docs/design/tokens.json` и сравнивает с загруженными ресурсами (Avalonia — headless-тест; MAUI — тест на разобранном XAML словарей, см. задачу).
