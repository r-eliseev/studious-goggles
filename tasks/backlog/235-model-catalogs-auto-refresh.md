---
id: 235
status: blocked
assignee:
claimed:
created: 2026-11-07
---

# Каталоги моделей: автоматическое обновление при запуске

## Описание
Вопрос владельца 2026-11-07: «списки моделей берутся с API OpenRouter в момент старта?» — сейчас нет. Каталоги картинок (`ModelCatalogService`) и видео (`VideoModelCatalogService`) работают от кэша в базе: загружаются из API только при пустом кэше или по кнопке Refresh экрана моделей сессии; автоматически не обновляются никогда (лог владельца: «Image models of provider openrouter: 63 from cache»). Каталог чат-моделей (`ChatModelCatalogService`) держит кэш в памяти — фактически обновляется раз за запуск при первом обращении. Модель, снятая провайдером, остаётся в списке до ручного обновления.

Зависит от: 221 и 227 (добавляют каталоги аудио и эмбеддингов — их тоже обновлять; делать после их слияния).
Среда: Linux или Windows.

## Критерии готовности
- `ModelCatalogRefreshService` (Core): при старте приложения (после `AppInitializer`, в фоне, не задерживая первый экран) для каждого каталога — картинки (по всем провайдерам с ключом), видео, аудио (221), эмбеддинги (227) — если кэш старше 24 часов (`FetchedAt` самой старой записи провайдера) или пуст — загружает из API (`forceRefresh: true`). Чат-модели — без изменений (уже раз за запуск).
- Ошибка или нет сети — кэш остаётся, одна Warning на каталог, запуск не страдает; провайдеры без ключа пропускаются без Warning (как сейчас в `GetAllImageModelsAsync`).
- Порог 24 ч — константа; время — через `TimeProvider`.
- После обновления — событие `CatalogsRefreshed` (или уведомление существующим механизмом), чтобы открытые экраны моделей могли перечитать список; экран не перезагружается сам, если пользователь в нём что-то отмечает (перечитывание — при следующем открытии).
- Вызов из обеих оболочек и Web-хоста — в том же месте, где запускается `AppInitializer` (найти: `rg -n "EnsureInitializedAsync" ImageGen`).
- Тесты: кэш свежее 24 ч → провайдер не вызывается; старше → вызывается и кэш заменён; ошибка провайдера → кэш прежний, Warning; провайдер без ключа пропущен; все четыре каталога обходятся.

## Анализ
`ImageGen/ImageGen.Core/Services/ModelCatalogService.cs` (`GetImageModelsAsync(providerId, forceRefresh, …)`, `GetAllImageModelsAsync`), `VideoModelCatalogService.cs`, `ChatModelCatalogService.cs` (`_byProvider` в памяти), будущие `AudioModelCatalogService` (221) и `EmbeddingModelCatalogService` (227); `FetchedAt` у `ImageModel`/`VideoModel`; `AppInitializer` (`ImageGen.Presentation/Services/AppInitializer.cs`) и его вызовы в оболочках.

## План
1. Падающие тесты службы обновления.
2. Служба; регистрация; запуск после инициализации во всех оболочках.
3. Событие обновления.

Файлы: `ImageGen/ImageGen.Core/Services/ModelCatalogRefreshService.cs` (новый), `ImageGen/ImageGen.Core/CoreServiceCollectionExtensions.cs`, точки запуска (`rg -n "EnsureInitializedAsync" ImageGen` — Presentation/Avalonia/MAUI/Web), тесты `ImageGen/ImageGen.Core.Tests/Services/ModelCatalogRefreshServiceTests.cs` (новый).

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet test --project ImageGen/ImageGen.Web.Tests/ImageGen.Web.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
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
Создано по вопросу владельца о загрузке списков моделей при старте (проверено по коду: кэш без автообновления). План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
Предложение одобрено («Да»); план одобрен ранее данным поручением «заапрувь все таски которые будут».

### 2026-11-11, codex-gpt6
Status reconciliation: the existing task branch records a plan mismatch (catalog services absorb provider failures and log independently; Web startup uses `StartupInitializer`), but the base task card still says `approved`. Set this task to `blocked` and clear the stale claim. The plan needs an amendment before implementation can proceed.
