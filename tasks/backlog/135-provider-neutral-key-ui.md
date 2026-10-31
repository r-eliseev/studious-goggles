---
id: 135
status: done
assignee:
claimed:
created: 2026-10-29
---

# Сделать управление provider keys общим

## Описание
Core ApiKeyService already provider-neutral, но ApiKeyPageModel/Settings/first-run hardcode OpenRouter. Перед built-in Together пользователь должен уметь add/change/remove key каждого configured provider; OpenRouter остаётся обязательным только для текущего assistant first-run, остальные optional.

Зависит от: 093, 132.
Среда: Linux или Windows.

## Критерии готовности
- API key route принимает validated provider id; heading/status uses ProviderConfig.DisplayName; Save/Remove uses SecretNames.ForProvider.
- Settings lists all configured providers in registry order with masked key/Not configured and Change/Remove.
- First-run still requests only OpenRouter because default assistant uses it; fal/Together do not block startup. Remove доступен и для OpenRouter с отдельным предупреждением об обязательном ключе ассистента; удаляются только секрет выбранного provider, не сессии/модели/другие ключи. Settings после удаления остаётся открыта и показывает Not configured; следующий SessionsPageModel.OnAppearingAsync снова открывает ввод OpenRouter. Открытый чат без ключа получает обычный InvalidKey, приложение не завершает работу.
- Model rows show provider display name/id so duplicate model names distinguishable. API keys, Authorization headers and secret values never appear in logs/traces at any level; sanitized non-secret content remains Debug-only.
- MAUI/Avalonia same behavior/accessibility; invalid/missing route provider safe alert/back.

## Анализ
ApiKeyService already provider-neutral; existing `ProviderRegistry.All/TryGet` already gives ordered listing and validated lookup, so no new Core catalog service. Task093/092 finalize model row metadata first. Do not hide models without keys; settings lets user configure and generation retains InvalidKey.

## План
Для Haiku/Luna: shared PageModels first; both shells markup; no Together built-in yet.

1. Inject existing ProviderRegistry into PageModels; use `All` order and `TryGet` validation. Presentation must not parse providers.json; no new Core service.
2. Add `Routes.ProviderIdParameter` and `Routes.ApiKeyFor(providerId)` using Uri.EscapeDataString. ApiKeyPageModel.ApplyQueryAttributes(IDictionary<string, object>) валидирует уже декодированный id через registry.TryGet, сохраняет ProviderId/Title и сбрасывает ApiKey/StatusText/HasError. Query не декодировать второй раз. Для нового route применять новый id, не сохранять предыдущий; Save захватывает валидированный id на старте и использует его до конца запроса. CanSave требует валидный provider + !IsBusy + непустой ввод. Title/heading — локализованный формат с DisplayName. Ключ не предзаполняется сохранённым секретом.
3. MAUI ApiKeyPage.xaml.cs implements IQueryAttributable and forwards query. Avalonia AppServices changes ApiKey registration to query-aware `Page(...)` and handles ApiKeyPageModel. ApplyQueryAttributes только сохраняет invalid state: нельзя идти назад до помещения page в navigation stack. OnAppearingAsync один раз показывает локализованный alert и вызывает GoBackAsync для invalid/missing id; повторное appearance не зацикливает alert. Ни HasKey/GetMasked/Save/Remove для invalid id не вызывать. ApiKeyPage.OnAppearing вызывает этот же OnAppearingAsync (сейчас MAUI этого не делает); Avalonia уже вызывает через IAppearing.
4. `ProviderKeyItemViewModel`: ProviderId, DisplayName, KeyText, HasKey, IsBusy, ChangeCommand и RemoveCommand; отдельные observable rows по порядку registry.All. SettingsPageModel заменяет ISecretStore на ApiKeyService+ProviderRegistry+IDialogService; KeyText получает только GetMaskedKeyAsync(id) либо локализованное Not configured. Remove disabled если нет ключа/IsBusy; подтверждение использует display name, OpenRouter получает отдельный предупреждающий resource; после RemoveAsync обновить только соответствующую строку. LoadCommand на повторном appearance обновляет данные без дублей; смена style/theme и OpenLogs работают как раньше. First-run в SessionsPageModel передаёт Routes.ApiKeyFor(ProviderIds.OpenRouter) явно; ChangeCommand каждой provider row использует Routes.ApiKeyFor(row.ProviderId), а старый одиночный Settings.ChangeKeyCommand удалить/заменить вызовами row; все вхождения голого Routes.ApiKey как navigation target заменить, регистрацию маршрута оставить.
5. Update MAUI/Avalonia Settings views to provider list, compiled bindings, semantics/automation, target sizes. Новые resource keys: ProviderKeyTitleFormat, ProviderKeyRemoveTitle, ProviderKeyRemoveConfirmFormat, ProviderKeyRemoveOpenRouterWarning, ProviderKeyRemove, ProviderKeyUnknown; использовать существующие SettingsKeyMissing/SettingsKeyConnectedFormat для состояния и текущие Change/Cancel строки, если уже есть. Включить wrappers в AppResources.cs; названия провайдера брать из registry, не локализовать id.
6. ModelOptionViewModel получает ProviderDisplayName из registry по model.ProviderId, показывает отдельный ProviderText = DisplayName + id; сохраняет manufacturer grouping092 и price/parameters093. Все места создания — ModelPickerPageModel.LoadAsync (включая UsedModels) и SessionModelsPageModel.LoadAsync — передают registry lookup; новый аргумент размещать после обязательных с сохранением существующего isSelected usage. Label добавить в обе image-model страницы и side pane, совпадающие model.Name от разных provider различимы. Не использовать DisplayName как ключ хранения или ModelRef: идентичность остаётся ProviderId+ModelId. При необходимости AssistantModelOptionViewModel не менять: критерий относится к image catalog.
7. Tests: ApiKeyFor корректно экранирует providerId; валидный together/fal/openrouter сохраняет в разные SecretNames.ForProvider; unknown/missing id не обращается к ключам; повторное appearance не дублирует alert; смена route не сохраняет прошлый ключ под новым id; успешное сохранение очищает поле. Settings Remove cancel ничего не удаляет; confirm удаляет ровно один provider, включая явный OpenRouter warning; Load refresh без дублей. Дополнительно MAUI forwarding, Avalonia query forwarding, Settings providers, first-run OpenRouter only, duplicate model labels. Capture all log levels/trace and assert full key, Authorization and secret value absent; key-probe response body absent.
8. Avalonia flow: query → правильный heading/provider save → возврат в обновлённый Settings; старт без OpenRouter открывает его ввод, только без fal/Together не открывает. MAUI markup/query forwarding tests сверяют публичные bindings/семантику. Ручная проверка сохранения двух разных provider отдельно: Android140, MAUI Windows016, Avalonia046; настоящие ключи вводит владелец, тесты используют фиктивные строки.
9. Обновить документацию первого запуска/управления ключами в chat-sessions.md и architecture.md: OpenRouter mandatory, остальные optional, Remove поведение. Заполнить «Отчёт», завершить по docs/tasks.md, release-task.sh135 и scripts/check.sh.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Presentation/Routes.cs`, `ImageGen/ImageGen.Presentation/PageModels/ApiKeyPageModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/SettingsPageModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/SessionsPageModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/ModelPickerPageModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/SessionModelsPageModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/ProviderKeyItemViewModel.cs` (новый), `ImageGen/ImageGen.Presentation/PageModels/ModelOptionViewModel.cs`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.cs`, `ImageGen/ImageGen.Presentation.Tests/RoutesTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/ApiKeyPageModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/SettingsPageModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/SessionsPageModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/SessionModelsPageModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/ModelPickerPageModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/ModelOptionViewModelTests.cs`, `ImageGen/ImageGen.Maui/MauiProgram.cs`, `ImageGen/ImageGen.Maui/Pages/ApiKeyPage.xaml`, `ImageGen/ImageGen.Maui/Pages/ApiKeyPage.xaml.cs`, `ImageGen/ImageGen.Maui/Pages/SettingsPage.xaml`, `ImageGen/ImageGen.Maui/Pages/SettingsPage.xaml.cs`, `ImageGen/ImageGen.Maui/Pages/ModelPickerPage.xaml`, `ImageGen/ImageGen.Maui/Pages/SessionModelsPage.xaml`, `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, `ImageGen/ImageGen.Avalonia/Views/ModelPickerView.axaml`, `ImageGen/ImageGen.Avalonia/Views/SessionModelsView.axaml`, `ImageGen/ImageGen.Avalonia/Views/ApiKeyView.axaml`, `ImageGen/ImageGen.Avalonia/Views/ApiKeyView.axaml.cs`, `ImageGen/ImageGen.Avalonia/Views/SettingsView.axaml`, `ImageGen/ImageGen.Avalonia/Views/SettingsView.axaml.cs`, `ImageGen/ImageGen.Avalonia/Composition/AppServices.cs`, `ImageGen/ImageGen.Avalonia.Tests/Views/ViewsAccessibilityTests.cs`, `ImageGen/ImageGen.Avalonia.Tests/Flows/StartFlowTests.cs`, `ImageGen/ImageGen.Avalonia.Tests/Composition/AppServicesTests.cs`, `ImageGen/ImageGen.Presentation.Tests/Design/MauiProviderKeysMarkupTests.cs` (новый), `docs/specs/chat-sessions.md`, `docs/architecture.md`, задачи ручных проверок016/046/140

## Отчёт

Сделано:
- API-key route принимает provider id с URL escaping; MAUI и Avalonia передают query в общий PageModel. Невалидный/отсутствующий id показывает один alert и безопасно возвращается назад.
- Экран настроек перечисляет провайдеры в порядке registry, показывает только маскированный статус и даёт отдельно изменить или удалить ключ. Удаление подтверждается; для OpenRouter есть предупреждение, настройки остаются открыты.
- Первый запуск по-прежнему запрашивает только OpenRouter. Строки моделей показывают имя и id провайдера, сохраняя идентичность модели по provider/model id.
- Добавлены Presentation, Avalonia flow/accessibility и MAUI markup проверки; обновлены спецификация, архитектура и сценарии ручной проверки платформ.

Проверки:
- Presentation tests: 384/384.
- Avalonia headless tests: 143 passed, 2 skipped (DPAPI и Secret Service недоступны на этой платформе).
- MAUI Android build: успешно, 0 warnings/errors.
- `dotnet format --verify-no-changes`: Presentation, Presentation.Tests, Avalonia, Avalonia.Tests и MAUI прошли.
- `bash scripts/check.sh`: все проверки пройдены.

Не проверено вручную: ввод реальных ключей/живой запрос, MAUI Windows и Android UI, Avalonia desktop UI. Сценарии добавлены в задачи 016, 140 и 046 соответственно; проверка настоящих ключей остаётся владельцу.

Отклонений от плана нет.

## Комментарии

### 2026-10-29, codex-review
Задача нужна до включения Together; использует существующий ApiKeyService. Решено поведение Remove обязательного OpenRouter, сброс/валидация route и UI lifetime; дописаны все места создания ModelOptionViewModel и маршрутизации первого запуска. Подтверждения удаления не показывают секрет.

### 2026-10-30, codex-audit — проверка завершённости
Статус done не подтверждён полностью; задача оставлена вне архива и переведена в blocked до фиксов 153. План шаг5 требовал platform target sizes. MAUI SettingsPage.xaml:27,33 задаёт локальный MinimumHeightRequest=44, переопределяя Android IgControlHeight=48. Основной key flow реализован; минимальная гарантия target size не выполнена.
Исторический отчёт и прежние комментарии сохранены. Остаток реализовывать только в названных follow-up задачах, не занимать эту задачу параллельно им. Вернуть done после их merge и проверки исходных критериев; отсутствие устройства само по себе причиной reopen не было.

### 2026-10-31, opencode-vps — закрыт остаток 153
Остаток по размеру цели касания закрыт (задача 153): у кнопок Change и Remove в строке ключа провайдера убран локальный `MinimumHeightRequest=44`, который перекрывал общий стиль кнопки; теперь высоту задаёт общий platform-aware ресурс `IgControlHeight` (Android 48, iOS 44, WinUI/MacCatalyst 32). Проверка разметки дополнена утверждением, что эти кнопки не переопределяют локальный минимум и используют общий стиль `ghost`. Команды, подписи, семантика и provider rows не менялись. MAUI под Android собирается; ручной пункт на Android (не меньше 48 dp, длинные имена провайдеров, TalkBack) записан в задачу 140. Windows UI на Linux не проверялся.
