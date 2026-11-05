---
id: 188
status: approved
assignee:
claimed:
created: 2026-11-05
---

# Settings: оформление не ждёт сеть; модель по умолчанию всегда в списке

## Описание
Находки ревью PR #62 (задача 172), вынесены из 175 (комментарий 2026-11-01, пп. 1–2). `SettingsPageModel.LoadAsync`:
1. Сначала ждёт каталоги всех чат-провайдеров (`chatModels.ListAllAssistantModelsAsync`, у OpenRouter — сетевой запрос) и только в конце вызывает `ShowAppearance(appearance.Current)`. Без сети раздел «Внешний вид» показывает невыбранные стили и тему до тайм-аута.
2. Если модели по умолчанию нет в каталоге, `SelectedAssistantModel` получает новый `AssistantModelOption`, которого нет в `AssistantModels`. `Picker`/`ComboBox` с `SelectedItem` вне `ItemsSource` показывают пустое поле, а двусторонняя привязка может сбросить значение в `null` — тогда «Save default model» ничего не делает.

Зависит от: 187 (тот же `SettingsPageModel.cs` и `SettingsPageModelTests.cs`; делать после её слияния).
Среда: Linux или Windows.

## Критерии готовности
- `ShowAppearance` вызывается до любых сетевых ожиданий: пока каталог моделей грузится, стиль и тема уже выбраны.
- Если модели по умолчанию нет в каталоге, её вариант добавляется в начало `AssistantModels` и выбирается; в списке ровно один такой вариант.
- Тесты Presentation на оба пункта; прежние тесты зелёные.

## Анализ
`LoadAsync` (`ImageGen/ImageGen.Presentation/PageModels/SettingsPageModel.cs`): порядок — подписка на статус индекса → `GetStatusAsync` → создание `Styles` → ключи провайдеров → каталог моделей → выбор модели → `ShowAppearance`. `ShowAppearance` ставит `style.IsSelected` и `SelectedTheme`. Тесты — `ImageGen/ImageGen.Presentation.Tests/PageModels/SettingsPageModelTests.cs` (`CreateModel`).

## План
1. Падающий тест `LoadAsync_ShowsAppearanceBeforeAssistantCatalogLoads`: каталог чат-моделей отвечает задачей, которая не завершается (`TaskCompletionSource`); текущее оформление — стиль `Gallery`, тема `Dark`. Запустить `LoadCommand.ExecuteAsync(null)` без `await`, дождаться (короткий цикл ожидания с таймаутом 2 с), что стиль `Gallery` выбран (`IsSelected`) и `SelectedTheme == Dark`, затем завершить `TaskCompletionSource` и дождаться команды. Как подменить каталог и оформление — посмотреть в существующем `CreateModel` и тестах 172; если каталог — конкретный `ChatModelCatalogService`, подменять его зависимость (провайдера), а не сам сервис.
2. Падающий тест `LoadAsync_DefaultModelMissingFromCatalog_AddsItToList`: каталог возвращает две модели, модель по умолчанию — третья. Ожидание: `AssistantModels.Count == 3`, `AssistantModels[0]` — модель по умолчанию, `SelectedAssistantModel` — тот же объект, что `AssistantModels[0]`.
3. Фикс: в `LoadAsync` перенести `ShowAppearance(appearance.Current)` сразу после блока создания `Styles`. При выборе модели: найти в списке; если нет — создать вариант (как сейчас), `AssistantModels.Insert(0, вариант)`, затем выбрать.

Файлы: `ImageGen/ImageGen.Presentation/PageModels/SettingsPageModel.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/SettingsPageModelTests.cs`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj --verify-no-changes
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

### 2026-11-05, claude-audit
Вынесено из 175 (пп. 1–2 комментария 2026-11-01) при подготовке планов под Luna.

### 2026-11-05, владелец (в чате, записал claude-audit)
План одобрен: «убедись, что все таски распланированы для Луны» — «и заапрувь их».
