---
id: 223
status: approved
assignee:
claimed:
created: 2026-11-07
---

# Аудио в интерфейсе: выбор моделей, карточка с плеером

## Описание
ADR 0027; заменяет третий этап 167. Выбор аудио-моделей сессии и аудио-карточка в ленте обеих оболочек.

Зависит от: 222, 218 (аудио-плеер MAUI привязан к `AudioFilePath`).
Среда: Linux (Presentation, Avalonia headless, сборка MAUI Android); проверка — 140, 046.

## Критерии готовности
- `SessionModelsPageModel`: раздел «Audio models» (имя, описание в одну строку, «price unknown»), выбор нескольких, сохранение через `UpdateAudioSettingsAsync`; чип моделей в чате учитывает аудио («Models: 1 × 1 · video 1 · audio 1» — формат строки по ресурсу; выбрать короткий вариант, влезающий в компактную шапку 212).
- Аудио-карточка (`IsAudio`): значок, модель, цена, статус; действия Repeat, Edit, Save file, Copy prompt; New seed, Run on model, Describe, Compare — скрыты. Отмена во время генерации — предупреждение `StoppedWaiting` (как у видео).
- MAUI: встроенный аудио-`MediaElement` (уже есть с 205, источник `AudioFilePath` из 218) — проверить, что он показывается только у готового аудио.
- Avalonia: плеер заблокирован (207) — карточка показывает Save file; кнопка воспроизведения скрыта. Android-голова Avalonia — то же.
- Строки ресурсов: «Audio models», подписи карточки.
- Тесты Presentation (модель страницы моделей, модель карточки аудио), тест разметки MAUI (аудио-плеер только при `IsAudio && HasMedia`), Avalonia headless (у аудио-карточки нет кнопки открытия картинки).
- Сборка MAUI Android и Avalonia Android; пункты 140 и 046: запрос «сыграй спокойную мелодию на пианино» даёт аудио-карточку, она играет (MAUI), сохраняется файлом.

## Анализ
Видео-интерфейс 204 — образец (разделы страницы моделей, команды карточки, ресурсы). Разметка: MAUI `SessionModelsPage.xaml`, `ChatPage.xaml`; Avalonia `SessionModelsView.axaml`, `ChatView.axaml`.

## План
1. Падающие тесты моделей страниц и карточки.
2. Модели страниц, ресурсы.
3. Разметка обеих оболочек; тесты разметки; сборки; пункты 140 и 046.

Файлы: `ImageGen/ImageGen.Presentation/PageModels/SessionModelsPageModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/GenerationItemViewModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/ChatPageModel.cs`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.cs`, `ImageGen/ImageGen.Maui/Pages/SessionModelsPage.xaml`, `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, `ImageGen/ImageGen.Avalonia/Views/SessionModelsView.axaml`, `ImageGen/ImageGen.Avalonia/Views/ChatView.axaml`, тесты `ImageGen/ImageGen.Presentation.Tests/` и `ImageGen/ImageGen.Avalonia.Tests/Views/`, `tasks/backlog/140-android-manual-check.md`, `tasks/backlog/046-desktop-manual-check.md`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet build ImageGen/ImageGen.Avalonia.Android/ImageGen.Avalonia.Android.csproj -c Release
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
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
Создано по просьбе владельца 2026-11-07 («генерация аудио нужна и отдельно от видео»); заменяет 167. Провайдер — OpenRouter (ADR 0027). Живой запуск LiveCheck `--only music` (208) владельцем ещё не сделан; клиент рассчитан на оба формата. План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
План одобрен ранее данным поручением «заапрувь все таски которые будут».
