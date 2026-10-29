---
id: 135
status: planned
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
- First-run still requests only OpenRouter because default assistant uses it; fal/Together do not block startup.
- Model rows show provider display name/id so duplicate model names distinguishable. API keys, Authorization headers and secret values never appear in logs/traces at any level; sanitized non-secret content remains Debug-only.
- MAUI/Avalonia same behavior/accessibility; invalid/missing route provider safe alert/back.

## Анализ
ApiKeyService already provider-neutral; existing `ProviderRegistry.All/TryGet` already gives ordered listing and validated lookup, so no new Core catalog service. Task093/092 finalize model row metadata first. Do not hide models without keys; settings lets user configure and generation retains InvalidKey.

## План
Для Haiku/Luna: shared PageModels first; both shells markup; no Together built-in yet.

1. Inject existing ProviderRegistry into PageModels; use `All` order and `TryGet` validation. Presentation must not parse providers.json; no new Core service.
2. Add `Routes.ProviderIdParameter` and `Routes.ApiKeyFor(providerId)` using Uri.EscapeDataString. ApiKeyPageModel ApplyQueryAttributes validates id, sets ProviderId/Title, Save uses id.
3. MAUI ApiKeyPage.xaml.cs implements IQueryAttributable and forwards query. Avalonia AppServices changes ApiKey registration to query-aware `Page(...)` and handles ApiKeyPageModel. Invalid/missing id: one alert then back, no key access.
4. `ProviderKeyItemViewModel` and SettingsPageModel collection from registry + ApiKeyService masked values; Change navigation; Remove confirmation/refresh. Existing first-run passes OpenRouter explicitly; no direct ISecretStore.
5. Update MAUI/Avalonia Settings views to provider list, compiled bindings, semantics/automation, target sizes.
6. ModelOptionViewModel receives provider display name; DetailsText/provider label in both model screens. Update tests after093.
7. Tests routes/PageModel Together+invalid id, MAUI forwarding, Avalonia query forwarding, Settings providers, first-run OpenRouter only, duplicate model labels. Capture all log levels/trace and assert full key, Authorization and secret value absent; key-probe response body absent.
8. Avalonia flow and MAUI structural accessibility tests; manual two keys persist separately.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Presentation/Routes.cs`, `ImageGen/ImageGen.Presentation/PageModels/ApiKeyPageModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/SettingsPageModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/ProviderKeyItemViewModel.cs` (новый), `ImageGen/ImageGen.Presentation/PageModels/ModelOptionViewModel.cs`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.cs`, `ImageGen/ImageGen.Presentation.Tests/RoutesTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/ApiKeyPageModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/SettingsPageModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/SessionsPageModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/SessionModelsPageModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/ModelOptionViewModelTests.cs`, `ImageGen/ImageGen.Maui/Pages/ApiKeyPage.xaml`, `ImageGen/ImageGen.Maui/Pages/ApiKeyPage.xaml.cs`, `ImageGen/ImageGen.Maui/Pages/SettingsPage.xaml`, `ImageGen/ImageGen.Maui/Pages/SettingsPage.xaml.cs`, `ImageGen/ImageGen.Avalonia/Views/ApiKeyView.axaml`, `ImageGen/ImageGen.Avalonia/Views/ApiKeyView.axaml.cs`, `ImageGen/ImageGen.Avalonia/Views/SettingsView.axaml`, `ImageGen/ImageGen.Avalonia/Views/SettingsView.axaml.cs`, `ImageGen/ImageGen.Avalonia/Composition/AppServices.cs`, `ImageGen/ImageGen.Avalonia.Tests/Views/ViewsAccessibilityTests.cs`, `ImageGen/ImageGen.Avalonia.Tests/Flows/StartFlowTests.cs`

## Отчёт

## Комментарии
