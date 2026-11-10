---
id: 233
status: in-progress
assignee: codex-gpt6
claimed: 2026-11-10T10:45:02Z
created: 2026-11-07
---

# Avalonia Android: сохранение картинки на Android 6–9

## Описание
Находка ревью PR #78 (2026-11-06): `AndroidImageSaver.SaveAsync` на Android ниже 10 (API < 29) сразу возвращает `ImageSaveResult.Failed`, хотя приложение ставится с Android 6 (`SupportedOSPlatformVersion` 23). Сохранение в галерею через `MediaStore.RelativePath` требует API 29; для старых версий есть общий путь — системный диалог сохранения файла (`StorageProvider.SaveFilePickerAsync`, Storage Access Framework, работает и на Android), который уже реализует общий `AvaloniaImageSaver` (191).

Зависит от: —
Среда: Linux с Android SDK (сборка); проверка на устройстве с Android 9 — если доступно, иначе «не проверено» (198).

## Критерии готовности
- На API < 29 `AndroidImageSaver` делегирует общему `AvaloniaImageSaver` (с `Func<TopLevel?>` на `MainView.Current`), который показывает системный диалог «Сохранить как»; результат — как у десктопа (`Saved`/`Cancelled`/`Failed`).
- `ActionLabel` на API < 29 — «Save file…» (ресурс, как у десктопа), на API ≥ 29 — «Save to gallery», как сейчас.
- Регистрация в `RegisterAndroid` передаёт сохранителю общий `AvaloniaImageSaver` (или фабрику).
- Сборка Android-головы без предупреждений; пункт 198: на Android 10+ — галерея, на Android 9 (если есть устройство) — диалог сохранения.

## Анализ
`ImageGen/ImageGen.Avalonia.Android/Platform/AndroidImageSaver.cs` (`OperatingSystem.IsAndroidVersionAtLeast(29)`), `ImageGen/ImageGen.Avalonia/Services/AvaloniaImageSaver.cs` (подключён в Android-голову ссылкой, задача 195), `ImageGen/ImageGen.Avalonia.Android/MobileApp.axaml.cs` (`RegisterAndroid`). Модульный тест на Linux для Android-ветки невозможен — проверка сборкой; ветку выбора можно вынести в чистую функцию (`SaverChoice.For(int apiLevel)`) и проверить в Avalonia.Tests, если файл подключён в оба проекта; иначе — только сборка.

## План
1. Делегирование и подпись действия.
2. Регистрация; сборка; пункт 198.

Файлы: `ImageGen/ImageGen.Avalonia.Android/Platform/AndroidImageSaver.cs`, `ImageGen/ImageGen.Avalonia.Android/MobileApp.axaml.cs`, `tasks/backlog/198-avalonia-android-manual-check.md`.

### Проверка
```
dotnet build ImageGen/ImageGen.Avalonia.Android/ImageGen.Avalonia.Android.csproj -c Release
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet format ImageGen/ImageGen.Avalonia.Android/ImageGen.Avalonia.Android.csproj --verify-no-changes
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
Создано по находке ревью PR #78 (2026-11-06). План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
Поручение «на те проблемы, для которых нет задач, создай задачи»; план одобрен ранее данным поручением «заапрувь все таски которые будут».
