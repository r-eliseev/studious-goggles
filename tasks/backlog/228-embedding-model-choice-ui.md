---
id: 228
status: in-progress
assignee: codex-gpt6
claimed: 2026-11-10T12:40:12Z
created: 2026-11-07
---

# Семантический поиск: выбор модели эмбеддингов в настройках

## Описание
Интерфейс к 227: в Settings, раздел «Semantic search», — выбор модели эмбеддингов из всего каталога OpenRouter с ценой, подтверждение переиндексации с оценкой и сообщение об устаревшей модели.

Зависит от: 227.
Среда: Linux (Presentation, Avalonia headless, сборка MAUI Android); проверка — 140, 046.

## Критерии готовности
- `SettingsPageModel`: `EmbeddingModels` (все модели `EmbeddingModelCatalogService`, сортировка по имени; строка — имя, «$X / 1M tokens» или «price unknown», «context N»), поиск по имени и id (как у моделей картинок), `SelectedEmbeddingModel` = текущая модель из настройки (если её нет в каталоге — отдельный элемент в начале списка с пометкой «unavailable»), команда `ChangeEmbeddingModelCommand`.
- Смена модели при включённом поиске: диалог `ConfirmAsync` с текстом «Re-index N texts with <model>? About M tokens, ≈$X (or "price unknown"). Semantic search works on the new model as indexing progresses.» (ресурс; числа из `EstimateReindexAsync`); согласие → `ChangeModelAsync`; отказ → выбор возвращается к текущей модели. При выключенном поиске — сохранение без диалога.
- Статус `model_unavailable`: под заголовком раздела — «The embedding model <id> is no longer available. Choose another model to re-index.» (ресурс) и выбор модели раскрыт.
- Прогресс переиндексации — существующая строка статуса (`Indexing N / M`).
- Разметка MAUI (`SettingsPage.xaml`) и Avalonia (`SettingsView.axaml`): выбор модели (Picker/ComboBox или отдельная страница выбора, если список с поиском не помещается — выбрать по образцу выбора модели ассистента по умолчанию на этой же странице) с доступностью.
- Тесты Presentation: список с ценами; выбор новой модели → диалог с оценкой → `ChangeModelAsync`; отказ → модель прежняя, `ChangeModelAsync` не вызван; поиск выключен → без диалога; `model_unavailable` → текст и элемент «unavailable». Avalonia headless — элемент выбора есть в разделе.
- Сборка MAUI Android; пункты 140 и 046: смена модели запускает переиндексацию, поиск по смыслу находит по новой модели.

## Анализ
`ImageGen/ImageGen.Presentation/PageModels/SettingsPageModel.cs` (раздел семантического поиска: `SemanticIndexStatus`, `SemanticSearchStatusText`, команды включения; выбор модели ассистента по умолчанию — образец списка моделей на этой странице, 188), `ImageGen/ImageGen.Maui/Pages/SettingsPage.xaml`, `ImageGen/ImageGen.Avalonia/Views/SettingsView.axaml`, ресурсы `AppResources`.

## План
1. Падающие тесты модели страницы.
2. Свойства, команда, ресурсы.
3. Разметка обеих оболочек; тест Avalonia; сборка MAUI Android; пункты 140 и 046.

Файлы: `ImageGen/ImageGen.Presentation/PageModels/SettingsPageModel.cs`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/SettingsPageModelTests.cs`, `ImageGen/ImageGen.Maui/Pages/SettingsPage.xaml`, `ImageGen/ImageGen.Avalonia/Views/SettingsView.axaml`, `ImageGen/ImageGen.Avalonia.Tests/Views/` (тест раздела), `tasks/backlog/140-android-manual-check.md`, `tasks/backlog/046-desktop-manual-check.md`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
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
Создано по решению владельца (выбор модели эмбеддингов и переиндексация). План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
План одобрен ранее данным поручением «заапрувь все таски которые будут».
