---
id: 060
status: planned
assignee:
claimed:
created: 2026-10-28
---

# Ресурсы оформления: шрифты и значки в обеих оболочках

## Описание
Подключить файлы шрифтов (Onest, IBM Plex Sans, IBM Plex Mono) и шрифт значков Lucide в MAUI и Avalonia, плюс класс констант `IconGlyphs` в Presentation. Без изменения экранов: экраны начнут ими пользоваться в задачах 063–072.

Справочник: `docs/design/implementation.md` (ключи ресурсов, значения, шрифты, значки — дословно оттуда); токены: `docs/design/tokens.json`; макеты: `docs/design/prototype.html` (открыть: `python3 -m http.server` в `docs/design/`, затем http://localhost:8000/prototype.html; опубликованная версия — https://claude.ai/artifact/M9QzPf39KLkRFTnYBtCi6F); решение: ADR 0014.

Зависит от: —.
Среда: Linux или Windows (Avalonia — headless-тесты на Linux; MAUI — сборка Android на Linux с Android SDK, Windows-цель собирает CI; запуск под Windows — ручная проверка 016, Avalonia на рабочем столе — 046).

## Критерии готовности
- Восемь файлов шрифтов из `docs/design/implementation.md`, раздел 3, и `lucide.ttf` лежат в обеих оболочках вместе с лицензиями (`OFL.txt` для каждого семейства, `LICENSE` Lucide).
- MAUI регистрирует шрифты с псевдонимами из раздела 3; Avalonia собирает их как `AvaloniaResource`.
- `IconGlyphs` содержит все константы из раздела 5.
- Тесты из «Плана» проходят; сборка Avalonia и MAUI (Android) проходит.

## Анализ
- MAUI: шрифты в `ImageGen.Maui/Resources/Fonts/` подхватываются правилом `<MauiFont Include="Resources\Fonts\*" />` (`ImageGen.Maui.csproj`), регистрация — `ConfigureFonts` в `MauiProgram.cs` (там уже OpenSans из шаблона: оставить, его используют шаблонные стили до задачи 064).
- Avalonia: в `ImageGen.Avalonia.csproj` нужен `<AvaloniaResource Include="Assets\Fonts\**" />`; семейство в разметке: `avares://ImageGen.Avalonia/Assets/Fonts#Onest` (имя после `#` — имя семейства внутри файла).
- Переменные шрифты из Google Fonts не используем (поддержка осей в MAUI ненадёжна): режем статические начертания `fonttools`. Проверено 2026-10-28: `fonttools varLib.instancer --update-name-table` даёт имена «Onest Medium», «IBM Plex Sans SemiBold».
- Инструменты ставятся только во временную папку `tmp/` в корне репозитория (AGENTS.md, «Запреты»).
- Лицензии: OFL 1.1 и ISC разрешают поставку в приложении при наличии текста лицензии.

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов, ключей и строк переноси дословно. Если шаг не получается так, как написано (компилятор не принимает разметку, API другое), не придумывай обход: опиши в «Комментариях», что пробовал и что получилось, поставь `blocked` и остановись.

1. Скачать и подготовить файлы в `tmp/fonts/` (не коммитить):
   - `python3 -m venv tmp/fonts/venv && tmp/fonts/venv/bin/pip install fonttools==4.55.0`;
   - скачать `curl -sSL -o` три исходных шрифта и три `OFL.txt` по адресам из `implementation.md`, раздел 3;
   - нарезать начертания командами вида `tmp/fonts/venv/bin/fonttools varLib.instancer --update-name-table <исходный> wght=400 -o Onest-Regular.ttf` (для Plex Sans добавить `wdth=100`); IBM Plex Mono Regular и Medium скачать как есть;
   - `curl -sSL -o tmp/fonts/lucide.tgz https://registry.npmjs.org/lucide-static/-/lucide-static-1.56.0.tgz`, распаковать `package/font/lucide.ttf` и `package/LICENSE`.
2. Положить файлы:
   - MAUI: `ImageGen/ImageGen.Maui/Resources/Fonts/` — 8 файлов шрифтов, `lucide.ttf`; лицензии — в `ImageGen/ImageGen.Maui/Resources/Licenses/` с именами `Onest-OFL.txt`, `IBMPlexSans-OFL.txt`, `IBMPlexMono-OFL.txt`, `Lucide-LICENSE.txt` (не в `Fonts/`, иначе `MauiFont` попытается их зарегистрировать).
   - Avalonia: `ImageGen/ImageGen.Avalonia/Assets/Fonts/` — те же 9 файлов; лицензии — `ImageGen/ImageGen.Avalonia/Assets/Licenses/` с теми же именами. В `.csproj` добавить `<AvaloniaResource Include="Assets\Fonts\**" />`.
3. MAUI `MauiProgram.cs`, `ConfigureFonts`: добавить 9 строк `fonts.AddFont("<файл>", "<псевдоним>")` с псевдонимами из раздела 3 (`lucide.ttf` → `LucideIcons`).
4. Presentation: `ImageGen/ImageGen.Presentation/Appearance/IconGlyphs.cs` — `public static class IconGlyphs` с `public const string <Имя> = "\uE13D";` для всех строк раздела 5 (имена констант — первая колонка таблицы).
5. Тесты:
   - `ImageGen.Presentation.Tests/Appearance/IconGlyphsTests.cs`: `Glyphs_AreSingleCharactersInPrivateUseArea` — через отражение все `const string` поля: длина 1, код в диапазоне U+E000–U+F8FF; `Glyphs_AreUnique`.
   - `ImageGen.Avalonia.Tests/Appearance/FontAssetsTests.cs` (`[AvaloniaFact]`): для каждого из 9 файлов `AssetLoader.Exists(new Uri("avares://ImageGen.Avalonia/Assets/Fonts/<файл>"))` — true (`Fonts_AreEmbedded`); `FontManager.Current.TryGetGlyphTypeface` не нужен.
   - Тест на семейства: `Fonts_HaveExpectedFamilyNames` — `new FontFamily("avares://ImageGen.Avalonia/Assets/Fonts#Onest")` и `FontManager.Current.TryGetGlyphTypeface(new Typeface(family), out var face)` → true. Если в headless-режиме это не работает, тест удалить и записать факт в `docs/knowledge/avalonia.md` и «Отчёт».
6. `docs/design/README.md` — строка о том, где лежат шрифты и лицензии в оболочках.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android -p:AndroidSdkDirectory=$ANDROID_HOME
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj --verify-no-changes
scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Maui/Resources/Fonts/*`, `ImageGen/ImageGen.Maui/Resources/Licenses/*`, `ImageGen/ImageGen.Maui/MauiProgram.cs`, `ImageGen/ImageGen.Avalonia/Assets/**`, `ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj`, `ImageGen/ImageGen.Presentation/Appearance/IconGlyphs.cs`, тесты, `docs/design/README.md`

## Отчёт

## Комментарии
