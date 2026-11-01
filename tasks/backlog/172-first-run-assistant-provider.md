---
id: 172
status: done
assignee: 
claimed: 
created: 2026-10-31
---

# Первый запуск: ключ провайдера выбранного ассистента; обновление списка чат-моделей

## Описание
После 156 модель ассистента можно взять у любого настроенного chat-провайдера, но первый запуск по-прежнему требует ключ OpenRouter (`SessionsPageModel.OnAppearingAsync`), а новые сессии всегда получают ассистента OpenRouter (`SessionSettings.CreateDefault`). Пользователь без OpenRouter не может начать работу. Кроме того, «Refresh» на экране моделей перечитывает только каталог картинок: список чат-моделей кэшируется до перезапуска (`ChatModelCatalogService`).

Зависит от: —
Среда: Linux или Windows; обе UI-оболочки.

## Критерии готовности
- Есть настройка приложения «модель ассистента по умолчанию для новых сессий» (по умолчанию — нынешняя `anthropic/claude-haiku-5.5` у OpenRouter); новые сессии получают её; существующие сессии не меняются.
- Первый запуск требует ключ провайдера модели ассистента по умолчанию, а не всегда OpenRouter; на экране ключа можно выбрать другой chat-провайдер из конфигурации — тогда после сохранения ключа предлагается выбрать модель ассистента этого провайдера, и она становится моделью по умолчанию.
- «Refresh» на экране моделей перечитывает и список чат-моделей; сбой одного провайдера не скрывает остальных (как в 156).
- Обе оболочки; тесты Presentation и Core.

## Анализ
- `SessionSettings.CreateDefault()` жёстко ставит `ModelRef(ProviderIds.OpenRouter, DefaultAssistantModelId)`; `SessionService.CreateAsync` его вызывает.
- `SessionsPageModel.OnAppearingAsync`: `HasKeyAsync(ProviderIds.OpenRouter)` → `Routes.ApiKeyFor(ProviderIds.OpenRouter)`.
- Настройки приложения уже хранятся файлами в папке данных (`AppearanceSettingsFile`) — тот же способ для модели по умолчанию.
- `ChatModelCatalogService` хранит списки в `ConcurrentDictionary` без сброса; `SessionModelsPageModel.RefreshAsync` передаёт `forceRefresh` только каталогу картинок.

## План
Сначала падающий тест (ub-mode).
1. Core: `SessionSettings.CreateDefault(ModelRef assistant)` (перегрузка; старая остаётся для совместимости тестов) и `SessionService.CreateAsync` берёт модель из `IDefaultAssistantModel` (интерфейс в Core, реализация в Presentation через файл `defaults.json` в папке данных; неверный файл → встроенное значение и запись в журнал).
2. Core: `ChatModelCatalogService.ListAllAssistantModelsAsync(bool forceRefresh, ct)` — при `forceRefresh` сбрасывает кэш провайдеров перед загрузкой; `SessionModelsPageModel.RefreshAsync` передаёт `true`.
3. Presentation: `SessionsPageModel` проверяет ключ провайдера модели по умолчанию. `ApiKeyPageModel` в режиме первого запуска показывает выбор chat-провайдера (`IProviderRouter.ChatProviderIds`, display name); после сохранения ключа не-OpenRouter провайдера открывает выбор модели ассистента этого провайдера (список из `ListAssistantModelsAsync(providerId)`; пустой список — сообщение «add chatModels to providers.json»), выбранная модель сохраняется как модель по умолчанию.
4. Settings: строка «Default assistant model» с выбором из всех chat-моделей (тот же список, что во вкладке ассистента).
5. Обе разметки (экран ключа, Settings).
6. Тесты: новая сессия получает модель по умолчанию; первый запуск без OpenRouter, но с ключом выбранного провайдера проходит; повреждённый `defaults.json`; Refresh перечитывает чат-модели (подменённый клиент вызван дважды); сбой одного провайдера.
7. Документы: `docs/specs/chat-sessions.md` (S1.3, S6.4), `docs/architecture.md` (поток «Первый запуск»).

Файлы: `ImageGen/ImageGen.Core/Domain/Sessions/SessionSettings.cs`, `ImageGen/ImageGen.Core/Services/SessionService.cs`, `ImageGen/ImageGen.Core/Services/IDefaultAssistantModel.cs` (новый), `ImageGen/ImageGen.Core/Services/ChatModelCatalogService.cs`, `ImageGen/ImageGen.Presentation/Settings/DefaultAssistantModelFile.cs` (новый), `ImageGen/ImageGen.Presentation/PageModels/SessionsPageModel.cs`, `ApiKeyPageModel.cs`, `SettingsPageModel.cs`, `SessionModelsPageModel.cs`, `AppResources.resx/.cs`, `ImageGen/ImageGen.Avalonia/Views/ApiKeyView.axaml`, `SettingsView.axaml`, `Composition/AppServices.cs`, `ImageGen/ImageGen.Maui/Pages/ApiKeyPage.xaml`, `SettingsPage.xaml`, `MauiProgram.cs`, тесты Core/Presentation/Avalonia, документы.

## Отчёт

- Добавлен `IDefaultAssistantModel` и атомарное хранение выбранной модели в `defaults.json`; при отсутствующем или повреждённом файле используется встроенная модель OpenRouter Haiku. Новые сессии получают выбранную модель; существующие сессии не меняются.
- First-run проверяет ключ провайдера модели по умолчанию. На обеих оболочках можно выбрать другого chat-провайдера, сохранить ключ, выбрать модель его каталога и сделать её значением по умолчанию. Settings позволяет менять модель для новых сессий.
- `Refresh` экрана моделей обновляет кэш чатов; сбой отдельного каталога не мешает получить модели остальных провайдеров. Обновлена спецификация `docs/specs/chat-sessions.md`.
- Проверено: Core.Tests 699/699; Presentation.Tests 433/433; Avalonia.Tests 149 passed, 2 пропущены (DPAPI/Secret Service недоступны в Linux-среде); сборки Avalonia и MAUI Android успешны; форматирование затронутых Core, Core.Tests, Presentation, Presentation.Tests и Avalonia проектов; `scripts/check.sh` прошёл.
- Windows-запуск оболочки не выполнялся в Linux-среде; сборка MAUI Android и обе shell-сборки успешны.

## Комментарии

### 2026-10-31, владелец (в чате, записал claude-audit)
Завести задачу: ключ для first-run (выбрано в вопросе). Объединено с обновлением списка чат-моделей по Refresh, как в варианте вопроса.

### 2026-10-31, владелец (в чате, записал claude-audit)
План одобрен.
