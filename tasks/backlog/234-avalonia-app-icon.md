---
id: 234
status: approved
assignee:
claimed:
created: 2026-11-07
---

# Avalonia: значок Omnilite (Android и desktop)

## Описание
Android-голова Avalonia использует значок пробника 094 — логотип Avalonia (`ImageGen.Avalonia.Android/Icon.png`, `Icon = "@drawable/icon"` в `MainActivity`); у desktop Avalonia значка окна и исполняемого файла нет. У MAUI уже есть значок Omnilite (задача 030, `ImageGen.Maui/Resources/AppIcon/appicon.svg` — фон, `appiconfg.svg` — изображение, цвет фона `#0B0618` в `ImageGen.Maui.csproj`).

Зависит от: —
Среда: Linux (растеризация SVG инструментом среды, сборки).

## Критерии готовности
- PNG-значки строятся из тех же SVG, что у MAUI (наложение `appiconfg.svg` на `appicon.svg`), инструментом `rsvg-convert` (`librsvg2-bin`; установка — как инструмента среды, `docs/knowledge/dev-environment.md`) и закоммичены как файлы (сборка не зависит от инструмента). Команды генерации записаны в `docs/knowledge/avalonia-mobile.md`, чтобы повторить при смене значка.
- Android-голова: `Resources/mipmap-mdpi|hdpi|xhdpi|xxhdpi|xxxhdpi/appicon.png` (48, 72, 96, 144, 192 px) и `appicon_round.png` тех же размеров (круглая маска); `MainActivity` — `Icon = "@mipmap/appicon"`, `RoundIcon` (если атрибут поддерживается привязкой; иначе в манифесте `android:roundIcon`); старый `Icon.png` удалён.
- Desktop Avalonia: `Assets/appicon.png` (256 px) как `Icon` главного окна (`MainWindow`), `appicon.ico` (16, 32, 48, 256) как `ApplicationIcon` в `ImageGen.Avalonia.csproj` (значок exe на Windows; `.ico` собрать `icotool` или ImageMagick — что доступно, записать).
- Сборки desktop и Android-головы без предупреждений; Avalonia headless тест: у `MainWindow` задан `Icon`.
- Пункты 046 и 198: значок Omnilite на рабочем столе/в списке приложений.

## Анализ
`ImageGen/ImageGen.Maui/Resources/AppIcon/appicon.svg`, `appiconfg.svg`, `ImageGen/ImageGen.Maui/ImageGen.Maui.csproj` (`MauiIcon … Color="#0B0618"`), `ImageGen/ImageGen.Avalonia.Android/MainActivity.cs`, `ImageGen.Avalonia.Android.csproj` (`<AndroidResource Include="Icon.png" …/>`), `ImageGen/ImageGen.Avalonia/Views/MainWindow.axaml`, `ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj`.

## План
1. Установить инструмент, сгенерировать PNG/ICO (файлы — в `tmp/`, затем в ресурсы).
2. Подключить в обоих проектах; тест `MainWindow.Icon`.
3. Сборки; документация; пункты 046 и 198.

Файлы: `ImageGen/ImageGen.Avalonia.Android/Resources/mipmap-*/appicon*.png` (новые), `ImageGen/ImageGen.Avalonia.Android/MainActivity.cs`, `ImageGen/ImageGen.Avalonia.Android/ImageGen.Avalonia.Android.csproj`, удаление `ImageGen/ImageGen.Avalonia.Android/Icon.png`, `ImageGen/ImageGen.Avalonia/Assets/appicon.png`, `ImageGen/ImageGen.Avalonia/Assets/appicon.ico` (новые), `ImageGen/ImageGen.Avalonia/Views/MainWindow.axaml`, `ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj`, `ImageGen/ImageGen.Avalonia.Tests/Views/` (тест значка), `docs/knowledge/avalonia-mobile.md`, `tasks/backlog/046-desktop-manual-check.md`, `tasks/backlog/198-avalonia-android-manual-check.md`.

### Проверка
```
dotnet build ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj
dotnet build ImageGen/ImageGen.Avalonia.Android/ImageGen.Avalonia.Android.csproj -c Release
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
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

### 2026-11-07, claude-audit
Создано: значок пробника 094 в Android-голове и отсутствие значка desktop Avalonia (ревью PR #78). План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
Поручение «на те проблемы, для которых нет задач, создай задачи»; план одобрен ранее данным поручением «заапрувь все таски которые будут».
