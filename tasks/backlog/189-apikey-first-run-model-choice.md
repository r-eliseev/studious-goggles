---
id: 189
status: approved
assignee:
claimed:
created: 2026-11-05
---

# Первый запуск: выбор модели ассистента на экране ключа

## Описание
Находки ревью PR #62 (задача 172), вынесены из 175 (комментарий 2026-11-01, пп. 3–5). `ApiKeyPageModel` при первом запуске (`IsFirstRun`):
1. `OnSelectedChatProviderChanged` не сбрасывает `IsChoosingAssistantModel`, `AssistantModels` и `SelectedAssistantModel`: после выбора модели провайдера A и переключения на B на экране остаётся список A, хотя `ProviderId` уже B.
2. После сохранения ключа не-OpenRouter провайдера модель выбирается по `defaultAssistantModel.Current` — это всегда модель OpenRouter, поэтому выбор `null`. Кнопка «Use» видна (`HasAssistantModels`), но `UseSelectedAssistantModelAsync` молча выходит.
3. `IsChatProvider` повторяет `IProviderRouter.ChatProviderIds` по другому правилу (требует `ChatModels` у OpenAI/Anthropic-совместимых), а каталог чат-моделей (`ChatModelCatalogService`) уже опирается на роутер.

Зависит от: 175 (`DefaultAssistantModelFile` и тестовые двойники модели по умолчанию; делать после её слияния).
Среда: Linux или Windows.

## Критерии готовности
- Смена провайдера в списке первого запуска скрывает выбор модели и очищает список и выбор.
- После загрузки моделей выбрана модель по умолчанию, если она этого провайдера, иначе первая в списке; «Use» сохраняет её.
- Список провайдеров первого запуска берётся из `IProviderRouter.ChatProviderIds` (в порядке `ProviderRegistry.All`); `IsChatProvider` удалён.
- Тесты Presentation на три пункта; прежние тесты `ApiKeyPageModelTests` зелёные (если тест опирался на старое правило `ChatModels`, поправить его ожидание и написать об этом в «Отчёте»).

## Анализ
Файл: `ImageGen/ImageGen.Presentation/PageModels/ApiKeyPageModel.cs` — `ApplyQueryAttributes` (наполняет `ChatProviders`), `OnSelectedChatProviderChanged`, `SaveAsync` (ветка `IsFirstRun && providerId != defaultAssistantModel.Current.ProviderId`), `UseSelectedAssistantModelAsync`, `IsChatProvider`. Конструктор первичный; `IProviderRouter` в нём нет — добавить параметром (DI в оболочках подставит сам). Тесты — `ImageGen/ImageGen.Presentation.Tests/PageModels/ApiKeyPageModelTests.cs`.

## План
1. Падающие тесты в `ApiKeyPageModelTests` (заглушка `IProviderRouter` с `ChatProviderIds`):
   - первый запуск, провайдер A (не OpenRouter) с ключом сохранён → модели A показаны, `SelectedAssistantModel` = первая модель A (не `null`); `UseSelectedAssistantModelCommand` сохраняет её в `IDefaultAssistantModel` и уходит назад;
   - затем выбрать в `SelectedChatProvider` провайдера B → `IsChoosingAssistantModel == false`, `AssistantModels` пуст, `SelectedAssistantModel == null`, `HasAssistantModels == false`;
   - в реестре OpenAI-совместимый провайдер без `ChatModels`, но он есть в `ChatProviderIds` заглушки → он есть в `ChatProviders`; провайдер, которого нет в `ChatProviderIds` (например `fal`), — нет.
2. Конструктор получает `IProviderRouter router` (после `ProviderRegistry providers`). Обновить создание модели в тестах (`rg -n "new ApiKeyPageModel" ImageGen`).
3. `ApplyQueryAttributes`: провайдеры = `providers.All.Where(p => router.ChatProviderIds.Contains(p.Id))`. Метод `IsChatProvider` удалить.
4. `OnSelectedChatProviderChanged` (в ветке, где провайдер принят): дополнительно `IsChoosingAssistantModel = false`, `AssistantModels.Clear()`, `SelectedAssistantModel = null`, `OnPropertyChanged(nameof(HasAssistantModels))`.
5. `SaveAsync`: `SelectedAssistantModel = AssistantModels.FirstOrDefault(совпадает с Current) ?? AssistantModels.FirstOrDefault()`.

Файлы: `ImageGen/ImageGen.Presentation/PageModels/ApiKeyPageModel.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/ApiKeyPageModelTests.cs`, при необходимости `ImageGen/ImageGen.Avalonia.Tests/` (если там модель создаётся вручную — `rg -n "new ApiKeyPageModel" ImageGen`).

### Проверка
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
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
Вынесено из 175 (пп. 3–5 комментария 2026-11-01) при подготовке планов под Luna.

### 2026-11-05, владелец (в чате, записал claude-audit)
План одобрен: «убедись, что все таски распланированы для Луны» — «и заапрувь их».
