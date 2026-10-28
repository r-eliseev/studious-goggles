---
id: 049
status: in-progress
assignee: claude-07d6
claimed: 2026-10-28T13:27:16Z
created: 2026-10-28
---

# Модель ассистента: платная по умолчанию и выбор в настройках сессии

## Описание
Владелец (2026-10-28, первая ручная проверка 016): бесплатная модель ассистента `google/gemma-4-31b-it:free` отвечает 429 — общий бесплатный пул перегружен у провайдера («temporarily rate-limited upstream», `limit_source: upstream_provider_shared_pool`). Нужно:
1. модель ассистента по умолчанию — `anthropic/claude-haiku-5.5` (решение владельца);
2. выбор модели ассистента в настройках сессии в обеих оболочках (это часть A задачи 027, вынесенная отдельно и раньше).

Зависит от: — (после 048 по очереди).
Среда: Linux или Windows (MAUI-часть проверяется сборкой Android и CI, интерфейс смотрится в Avalonia на Linux).

## Критерии готовности
- Новая сессия использует `openrouter:anthropic/claude-haiku-5.5`.
- На экране моделей сессии есть выбор модели ассистента (MAUI и Avalonia): список чат-моделей каталога с поиском, у каждой — цена за 1 млн токенов входа и выхода, пометки «принимает картинки» и «бесплатная, возможны лимиты».
- В список попадают только модели с инструментами (`tools` в `supported_parameters`): без них ассистент не может запустить генерацию.
- Сохранённый выбор действует со следующего сообщения; модель видна в чате (подпись рядом с кнопкой моделей или в заголовке).
- Тест-кейсы из плана проходят.

## Анализ
- Сейчас: `SessionSettings.DefaultAssistantModelId = "google/gemma-4-31b-it:free"`, `SessionSettings.AssistantModel` — `ModelRef` (036). `ChatModelCatalogService` (017) умеет `IsImageInputAsync`; каталог чат-моделей берётся из `GET /api/v1/models` (в памяти).
- `anthropic/claude-haiku-5.5` по `GET /api/v1/models` на 2026-10-28: $0.10 за 1 млн токенов входа, $0.50 выхода, контекст 1 000 000, инструменты и картинки на входе есть. Ход чата ≈ 1,5 тыс. токенов входа ≈ $0.0003.
- Существующие сессии хранят свою модель в `settings_json`: сессия, созданная до задачи, останется на `gemma …:free`, пока пользователь не выберет другую на экране. Массово не переписываем (решение пользователя важнее умолчания); но экран показывает текущую модель, и смена — в два клика.
- Задача 027 после этой задачи сводится к части B (управление контекстом): комментарий в 027.
- В лог при 429 уже пишется `metadata.raw` провайдера; на экране текст «Provider returned error» малоинформативен — это задача 043 (тексты ошибок).

## План
1. Core:
   - `SessionSettings.DefaultAssistantModelId = "anthropic/claude-haiku-5.5"`.
   - `ChatModel` (из 017) получает поля `SupportsTools`, `PromptPricePerMillion`, `CompletionPricePerMillion` (decimal?), `IsFree` (`:free` в id или нулевая цена); `OpenRouterClient` разбирает их из `GET models` (`supported_parameters`, `pricing.prompt`, `pricing.completion`).
   - `ChatModelCatalogService.ListAssistantModelsAsync(providerId, ct)` → модели с инструментами, сортировка по имени; `FindAsync(ModelRef)`.
   - `SessionService.UpdateSettingsAsync`: модель ассистента должна быть в каталоге и с инструментами, иначе `ArgumentException`, настройки не меняются (если каталог недоступен — сохранить без проверки и записать предупреждение в журнал).
2. Presentation:
   - `SessionModelsPageModel`: раздел «Assistant model» — список `AssistantModelOptionViewModel` (имя, id, цена, пометки), поиск, одна выбранная; сохранение вместе с моделями картинок.
   - `ChatPageModel`: строка `AssistantModelText` («Assistant: Claude Haiku 5.5»).
   - Строки `AppResources`: заголовок раздела, пометки, формат цены.
3. Оболочки: разметка раздела на `SessionModelsPage` (MAUI) и `SessionModelsView` (Avalonia), подпись в чате.
4. Тест-кейсы:
   - новая сессия → `AssistantModel` = `openrouter:anthropic/claude-haiku-5.5`;
   - разбор каталога: модель с `tools` и ценами → поля заполнены; без `tools` → не в списке ассистента; `:free` → `IsFree`;
   - сохранение модели не из каталога → исключение, настройки прежние; без инструментов → исключение;
   - каталог недоступен → сохранение проходит, в журнале предупреждение;
   - `SessionModelsPageModel`: текущая модель отмечена; выбор другой и Save → `UpdateSettingsAsync` с новой моделью; поиск фильтрует;
   - `AssistantService` после смены модели → запрос уходит с новой моделью;
   - Avalonia headless: раздел виден, выбор сохраняется.
5. Документация: `docs/specs/chat-sessions.md` (S1.3 — модель по умолчанию, S5 — выбор модели ассистента), задача 016 и 046 — пункт ручной проверки (смена модели, ответ без 429).

### Команды проверки
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format <каждый изменённый проект> --verify-no-changes
scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Domain/Sessions/SessionSettings.cs`, `ImageGen/ImageGen.Core/Domain/Models/ChatModel.cs`, `ImageGen/ImageGen.Core/OpenRouter/OpenRouterClient.cs`, `ImageGen/ImageGen.Core/Services/ChatModelCatalogService.cs`, `ImageGen/ImageGen.Core/Services/SessionService.cs`, `ImageGen/ImageGen.Presentation/PageModels/SessionModelsPageModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/AssistantModelOptionViewModel.cs` (новый), `ImageGen/ImageGen.Presentation/PageModels/ChatPageModel.cs`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.*`, `ImageGen/ImageGen.Maui/Pages/SessionModelsPage.xaml`, `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, `ImageGen/ImageGen.Avalonia/Views/SessionModelsView.axaml`, `ImageGen/ImageGen.Avalonia/Views/ChatView.axaml`, тесты во всех тестовых проектах, `docs/specs/chat-sessions.md`, `tasks/v0.1/016-skeleton-manual-check.md`, `tasks/backlog/046-desktop-manual-check.md`

## Отчёт

## Комментарии

### 2026-10-28, владелец (в чате, записал ревью)
План одобрен.
