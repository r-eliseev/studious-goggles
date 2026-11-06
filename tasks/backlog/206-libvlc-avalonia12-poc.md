---
id: 206
status: planned
assignee:
claimed:
created: 2026-11-06
---

# Пробник: LibVLCSharp на Avalonia 12 (desktop и Android)

## Описание
ADR 0027, п. 7. Владелец выбрал LibVLC для Avalonia. `LibVLCSharp.Avalonia` 3.10.2 официально зависит от Avalonia ≥ 11.3.13 и собран под `net8.0`/`netstandard2.0`; проект — на Avalonia 12.1.4 (ADR 0026 добавляет Android-голову). Пробник снимает неизвестность: собирается ли и играет ли видео на Avalonia 12, какой ценой (размер пакетов, нативные зависимости по ОС). Код — только в `poc/backlog/206-libvlc-avalonia12/` (правила `poc/README.md`), не в `ImageGen/`.

Зависит от: —
Среда: Linux (сборка, headless-воспроизведение без экрана); Android SDK (сборка APK); Windows и macOS — только восстановление пакетов и сборка, если доступны (иначе «не проверено»).

## Критерии готовности
- Пробник `poc/backlog/206-libvlc-avalonia12/` с двумя проектами: `Desktop` (Avalonia 12.1.4, `LibVLCSharp` 3.10.2, `LibVLCSharp.Avalonia` 3.10.2) и `Android` (Avalonia.Android 12.1.4, `LibVLCSharp`, `VideoLAN.LibVLC.Android` — версию взять с nuget.org на дату работы).
- Ответы, записанные в «Отчёт» и `docs/knowledge/media-generation.md`, раздел «Воспроизведение»:
  1. Собирается ли `Desktop` с `VideoView` из `LibVLCSharp.Avalonia` на Avalonia 12 (ошибки/предупреждения дословно).
  2. Играет ли файл: консольный режим пробника создаёт `LibVLC` с `--vout=dummy --aout=dummy`, открывает короткий тестовый `mp4` (сгенерировать `ffmpeg -f lavfi -i testsrc=duration=2:size=320x240:rate=25 -f lavfi -i sine=duration=2 -shortest test.mp4`, если `ffmpeg` есть; иначе взять любой свободный тестовый ролик с указанием лицензии) и ждёт событий `Playing` и `EndReached`; печатает `Length`.
  3. Откуда берётся нативная libvlc на Linux (системный пакет — какие `apt`-пакеты нужны; работает ли без них), сколько весит `VideoLAN.LibVLC.Windows` в publish `win-x64` и `VideoLAN.LibVLC.Android` в APK (сравнить размер APK с пробником 094 / Android-головой 195, если она уже есть).
  4. Если `VideoView` не собирается или падает: работает ли запасной путь — колбэки видео LibVLC (`SetVideoFormat` + `SetVideoCallbacks`) в `WriteableBitmap` внутри обычного `Image` Avalonia (собрать и проверить, что колбэк вызывается в headless-режиме).
- Рекомендация для 207: какой путь (VideoView / колбэки / отказ от LibVLC) и почему; вопрос владельцу, если ни один путь не работает.

## Анализ
LGPL-2.1+ допускает распространение с динамической libvlc; в релизе нужен файл лицензии. `ImageGen.Avalonia` сейчас `net10.0`; пакет `netstandard2.0`/`net8.0` подключается к `net10.0`. Экрана в облачной среде нет — визуальная проверка `VideoView` невозможна, проверяется сборка и воспроизведение в фиктивный вывод.

## План
1. Создать папку пробника и два проекта (не добавлять в `ImageGen.slnx`); `Directory.Packages.props` пробника — свой, как у 094.
2. Сборка Desktop; консольный режим воспроизведения (п. 2 «Критериев»).
3. Сборка Android APK.
4. При неудаче `VideoView` — запасной путь (п. 4).
5. Запись результатов и рекомендации.

Файлы: `poc/backlog/206-libvlc-avalonia12/**` (новые), `docs/knowledge/media-generation.md`.

### Проверка
```
dotnet build poc/backlog/206-libvlc-avalonia12/Desktop/Desktop.csproj
dotnet run --project poc/backlog/206-libvlc-avalonia12/Desktop/Desktop.csproj -- --play poc/backlog/206-libvlc-avalonia12/test.mp4
dotnet build poc/backlog/206-libvlc-avalonia12/Android/Android.csproj -c Release
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
