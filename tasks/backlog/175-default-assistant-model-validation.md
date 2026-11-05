---
id: 175
status: approved
assignee:
claimed:
created: 2026-11-01
---

# Модель ассистента по умолчанию: проверять провайдера из defaults.json

## Описание
Найдено при ревью 2026-11-01 (задача 172, `09d5533`). `DefaultAssistantModelFile.Read` принимает любую непустую пару provider/model из `defaults.json` и не сверяет её с конфигурацией. Если провайдера модели по умолчанию потом убрать из `providers.json` (или поменять его вид), первый запуск зацикливается: `SessionsPageModel.OnAppearingAsync` проверяет ключ этого провайдера → нет ключа → `Routes.ApiKeyFor(provider, firstRun)` → `ApiKeyPageModel` не находит провайдера, показывает «unknown provider» и возвращается назад → `SessionsPage` снова появляется → снова экран ключа. Приложение непригодно, пока файл не исправить вручную. Если ключ остался, новые сессии получают ассистента несуществующего провайдера и падают на первом сообщении. Найдено чтением кода, не воспроизведено.

Зависит от: —
Среда: Linux или Windows.

## Критерии готовности
- Модель из `defaults.json` используется, только если её провайдер есть в `ProviderRegistry` и поддерживает чат (`IProviderRouter.ChatProviderIds`); иначе — встроенная модель OpenRouter и одна запись Warning в журнал (без содержимого файла).
- Первый запуск с таким файлом не зацикливается: открывается экран ключа OpenRouter (как без файла).
- `SaveAsync` отклоняет модель неизвестного или не-чат провайдера (`ArgumentException`), файл не меняется.
- Тесты Presentation на все случаи; существующие тесты 172 зелёные.
- Сбой удаления временного файла `defaults.json.*.tmp` (`IOException` или `UnauthorizedAccessException`) пишется Warning и не заменяет исходную ошибку записи.

## Анализ
- `ImageGen/ImageGen.Presentation/Settings/DefaultAssistantModelFile.cs`: `Read()` проверяет только пустоту полей; зависит от `IAppPaths` и `ILogger`.
- `SessionsPageModel.OnAppearingAsync` и `ApiKeyPageModel` (ветка `IsRouteInvalid` → алерт и `GoBackAsync`) — источник цикла.
- Список чат-провайдеров — `IProviderRouter.ChatProviderIds` (156).

## План
Остальные находки из комментария 2026-11-01 вынесены: пп. 1–2 (экран Settings) — в 188, пп. 3–5 (экран ключа при первом запуске) — в 189. Здесь только `DefaultAssistantModelFile`.

1. Падающие тесты в `ImageGen/ImageGen.Presentation.Tests/Settings/DefaultAssistantModelFileTests.cs`. Хелпер `Create()` получает заглушку `IProviderRouter` (NSubstitute), у которой `ChatProviderIds` = `["openrouter", "my-llm"]`; для теста журнала — `CapturingLogger` или аналог из тестовых двойников проекта (найти через `rg -n "class .*Logger" ImageGen/ImageGen.Presentation.Tests`).
   - файл `{"ProviderId":"gone","ModelId":"m"}` → `Current` = встроенная модель OpenRouter (`SessionSettings.DefaultAssistantModelId`), в журнале одна Warning без текста файла;
   - файл с `fal` (нет в `ChatProviderIds`) → встроенная модель;
   - файл с `my-llm`/`m` → `Current` = эта модель;
   - `SaveAsync(new ModelRef("gone","m"))` → `ArgumentException`, файл на диске прежний, `Current` прежний;
   - удаление временного файла бросает `UnauthorizedAccessException` → наружу выходит исходная ошибка записи, а не это исключение. Если подменить удаление без правки класса нельзя — этот случай пропустить и записать в «Отчёт» (правка `catch` из шага 3 всё равно делается).
2. Конструктор: `DefaultAssistantModelFile(IAppPaths paths, IProviderRouter router, ILogger<DefaultAssistantModelFile> logger)`. В `Read()` после проверки на пустоту: если `router.ChatProviderIds` не содержит `model.ProviderId` → Warning «defaults.json names provider that is not a chat provider; using built-in assistant model» и `BuiltIn`. В `SaveAsync` после проверок аргументов — та же проверка, при неудаче `ArgumentException` до записи файла.
3. В `SaveAsync`, блок `finally`: `catch (IOException exception)` заменить на `catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)` (комментарий 2026-11-01 к 177).
4. Регистрации в `MauiProgram.cs` и `AppServices.cs` не трогать: DI подставит `IProviderRouter`. Найти все прямые `new DefaultAssistantModelFile(` в тестах (`rg -n "new DefaultAssistantModelFile|DefaultAssistantModelFile\(" ImageGen`) и передать туда заглушку роутера.

Файлы: `ImageGen/ImageGen.Presentation/Settings/DefaultAssistantModelFile.cs`, `ImageGen/ImageGen.Presentation.Tests/Settings/DefaultAssistantModelFileTests.cs`, при необходимости `ImageGen/ImageGen.Presentation.Tests/TestDoubles/DefaultAssistantModels.cs`.

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

### 2026-11-01, владелец (в чате, записал claude-audit)
Завести задачу на цикл первого запуска из ревью 172 (выбрано в вопросе).

### 2026-11-01, claude-audit — ревью PR #62, ещё по 172
Найдено чтением кода, не воспроизведено. Предлагаю включить в эту задачу (они в тех же файлах):
1. `SettingsPageModel.LoadAsync` (`SettingsPageModel.cs:82`) ждёт каталоги всех чат-провайдеров (у OpenRouter это сетевой запрос) и только после этого вызывает `ShowAppearance` (`:94`). Без сети раздел «Внешний вид» пуст до тайм-аута. Нужно грузить модели после оформления или отдельно от него.
2. Там же (`:91`): если модель по умолчанию не нашлась в каталоге, `SelectedAssistantModel` получает новый `AssistantModelOption`, которого нет в `AssistantModels`. `Picker`/`ComboBox` с `SelectedItem` вне `ItemsSource` показывают пустое поле, а двусторонняя привязка может сбросить значение в null, и тогда «Сохранить» ничего не делает. Лучше добавить этот вариант в список.
3. `ApiKeyPageModel.OnSelectedChatProviderChanged` (`ApiKeyPageModel.cs:125`) не сбрасывает `IsChoosingAssistantModel` и `AssistantModels`. Если после выбора модели провайдера A переключиться на B, на экране остаётся список A, хотя `ProviderId` уже B.
4. Там же (`:182`): модель для другого провайдера заранее выбирается по `defaultAssistantModel.Current`, а это всегда модель OpenRouter, поэтому выбор = null. Кнопка «Использовать» включена (`HasAssistantModels`), но команда молча ничего не делает (`:218`). Нужно заранее выбирать первую модель или выключать кнопку, пока модель не выбрана.
5. `ApiKeyPageModel.IsChatProvider` (`:232`) повторяет `ProviderRouter.ChatProviderIds` по другому правилу: требует `ChatModels`, а роутер нет. Нужен один источник; это то же место, что проверка из критериев этой задачи.

### 2026-11-05, claude-audit
План переписан под Luna: только `DefaultAssistantModelFile` (+ `catch` при удалении временного файла из комментария к 177). Находки 1–2 комментария 2026-11-01 — задача 188, 3–5 — задача 189.

### 2026-11-05, владелец (в чате, записал claude-audit)
План одобрен: «убедись, что все таски распланированы для Луны» — «и заапрувь их».
