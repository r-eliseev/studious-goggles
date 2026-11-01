---
id: 175
status: planned
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

## Анализ
- `ImageGen/ImageGen.Presentation/Settings/DefaultAssistantModelFile.cs`: `Read()` проверяет только пустоту полей; зависит от `IAppPaths` и `ILogger`.
- `SessionsPageModel.OnAppearingAsync` и `ApiKeyPageModel` (ветка `IsRouteInvalid` → алерт и `GoBackAsync`) — источник цикла.
- Список чат-провайдеров — `IProviderRouter.ChatProviderIds` (156).

## План
Сначала падающий тест (ub-mode).
1. Тесты `DefaultAssistantModelFileTests`: файл с провайдером, которого нет в реестре → `Current` = встроенная модель + Warning; провайдер image-only (`fal`) → встроенная; валидный → из файла; `SaveAsync` неизвестного провайдера → исключение, файл прежний. `SessionsPageModelTests`: файл с неизвестным провайдером → навигация на ключ OpenRouter ровно один раз.
2. `DefaultAssistantModelFile` получает `IProviderRouter` (или `ProviderRegistry` + router) в конструктор; проверка в `Read()` и `SaveAsync`.
3. Регистрация в `AppServices.cs` и `MauiProgram.cs` не меняется (DI подставит новый параметр); тестовые помощники `DefaultAssistantModels` обновить.
4. Проверки: Presentation.Tests, Avalonia.Tests, сборка MAUI Android, `dotnet format`, `scripts/check.sh`.

Файлы: `ImageGen/ImageGen.Presentation/Settings/DefaultAssistantModelFile.cs`, `ImageGen/ImageGen.Presentation.Tests/Settings/DefaultAssistantModelFileTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/SessionsPageModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/TestDoubles/DefaultAssistantModels.cs`, `ImageGen/ImageGen.Core.Tests/TestDoubles/DefaultAssistantModels.cs` (если нужно).

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
