---
id: 236
status: approved
assignee:
claimed:
created: 2026-11-07
---

# Выбранная модель пропала из каталога — видно и можно убрать

## Описание
Вопрос владельца 2026-11-07: «если выбранная модель недоступна, она просто пропадёт со списка?» — сейчас: после обновления каталога строка модели исчезает с экрана моделей сессии (строки строятся только из каталога), но модель остаётся в настройках сессии до сохранения экрана, а прогон с ней даёт карточку «Failed: model unavailable» (`ErrorKind.ModelUnavailable`, `GenerationService`). Предупреждения нет.

Нужно: выбранная, но пропавшая из каталога модель видна как «unavailable» и убирается одним нажатием; запуск её не пытается.

Зависит от: 235 (обновление каталогов), 223 и 228 (экраны аудио- и эмбеддинг-моделей — их тоже касается; делать после их слияния).
Среда: Linux (Presentation, Avalonia headless, сборка MAUI Android).

## Критерии готовности
- `SessionModelsPageModel`: модели из настроек сессии (картинки, видео, аудио), которых нет в каталоге, показываются в начале своих разделов отдельными строками с пометкой «Unavailable — no longer offered by the provider» (ресурс), выбранными и с кнопкой «Remove»; сохранение экрана без них убирает их из сессии.
- Модель ассистента сессии, которой нет в каталоге чат-моделей, — так же (строка «unavailable», выбор другой обязателен перед следующим сообщением: отправка сообщения показывает алерт с переходом на экран моделей).
- Модель ассистента по умолчанию в Settings (`SelectedAssistantModel`, задача 188) — уже показывается отдельным элементом; добавить пометку «unavailable», если её нет в каталоге.
- Модель эмбеддингов — статус `model_unavailable` уже даёт 227/228; проверить, что после обновления каталога (235) он появляется.
- Чип моделей в шапке чата (212) показывает знак предупреждения и число недоступных («Models: 2 × 1 · 1 unavailable», формат — ресурс).
- `GenerationService`: при старте прогона модели, которых нет в каталоге, пропускаются (генерация не создаётся); если не осталось ни одной — `InvalidOperationException` с понятным текстом, ассистент/интерфейс показывает его (а не карточку Failed на каждую модель). Если каталог недоступен (нет сети, пустой кэш) — модели не отбрасываются (как сейчас: решает провайдер).
- Тесты Presentation (строки unavailable, Remove, сохранение, чип) и Core (пропуск недоступных моделей, все недоступны → исключение, пустой каталог → без отбрасывания).
- Сборка MAUI Android; пункты 140 и 046.

## Анализ
`ImageGen/ImageGen.Presentation/PageModels/SessionModelsPageModel.cs` (`LoadAsync`: строки из каталога, `selected.Contains(...)`), `ChatPageModel.cs` (текст чипа `ModelsButtonText`, отправка), `SettingsPageModel.cs` (модель по умолчанию), `ImageGen/ImageGen.Core/Services/GenerationService.cs` (≈513–580: проверка модели по каталогу и `ModelUnavailable`), разметка MAUI `SessionModelsPage.xaml`, `ChatPage.xaml`, Avalonia `SessionModelsView.axaml`, `ChatView.axaml`.

## План
1. Падающие тесты Core и Presentation.
2. Пропуск недоступных моделей в `GenerationService`.
3. Строки unavailable, Remove, чип, проверка ассистента; ресурсы.
4. Разметка обеих оболочек; сборки; пункты 140 и 046.

Файлы: `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `ImageGen/ImageGen.Presentation/PageModels/SessionModelsPageModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/ChatPageModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/SettingsPageModel.cs`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.cs`, `ImageGen/ImageGen.Maui/Pages/SessionModelsPage.xaml`, `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, `ImageGen/ImageGen.Avalonia/Views/SessionModelsView.axaml`, `ImageGen/ImageGen.Avalonia/Views/ChatView.axaml`, тесты Core, Presentation, Avalonia, `tasks/backlog/140-android-manual-check.md`, `tasks/backlog/046-desktop-manual-check.md`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
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
Создано по вопросу владельца о пропавших моделях (проверено по коду: модель исчезает из списка, но остаётся в сессии и даёт Failed). План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
Предложение одобрено («Да»); план одобрен ранее данным поручением «заапрувь все таски которые будут».
