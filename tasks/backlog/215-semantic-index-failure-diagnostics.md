---
id: 215
status: done
assignee:
claimed:
created: 2026-11-07
---

# Семантический индекс: понятная причина сбоя и проверка эмбеддингов вживую

## Описание
Логи владельца (Android, MAUI, 2026-11-05 и 2026-11-06): каждый `POST /api/v1/embeddings` отвечает `200` за 0,6–1,2 с, сразу после — `Semantic search indexing failed with provider error Unknown.` Значит, ответ получен, но не прошёл разбор или проверку. `ErrorKind.Unknown` дают два места: `OpenRouterClient.InvalidEmbeddingResponse()` (формат `data`, `index`, `embedding`, размерность) и `SemanticIndexService.ValidateResult` (число векторов, размерность, `result.Model != "baai/bge-m3"`, нечисловые значения). Журнал пишет только вид ошибки, поэтому причину по логу не определить. Вероятная причина (не проверено) — имя модели в ответе отличается от `baai/bge-m3` (регистр, суффикс).

Зависит от: —
Среда: Linux или Windows; живой запуск LiveCheck — с ключом OpenRouter владельца (стоимость ничтожна: `baai/bge-m3` — $0.01 за 1 млн токенов).

## Критерии готовности
- Каждая проверка в `ReadEmbeddings` и `ValidateResult` бросает `ProviderException` со своим текстом, без содержимого векторов и текстов, например: «embedding response has 3 items, expected 4», «item 2 has no index», «vector 1 has 1023 values, expected 1024», «response model 'BAAI/bge-m3' differs from requested 'baai/bge-m3'», «vector 0 contains a non-finite value».
- `SemanticIndexService` при `ProviderException` пишет Warning с видом **и текстом** исключения (текст формируется нашим кодом и секретов не содержит).
- Экран Settings при `Failed` показывает короткую причину (поле `SemanticIndexStatus.ErrorDetail` = текст исключения, обрезанный до 200 символов) под «Indexing failed…», на обеих оболочках.
- Сценарий LiveCheck `embeddings`: один запрос с двумя короткими текстами к `baai/bge-m3`; в отчёт — HTTP-код, поле `model` ответа, число и длина векторов, наличие `index`, `usage` (включая `cost`, если есть); итог — «проверка прошла/не прошла» тем же кодом разбора, что приложение. Бюджет $0.01; без ключа пропуск.
- Тесты: каждая ветка проверки даёт свой текст; Warning содержит текст; разбор сценария LiveCheck — на записанной фикстуре.
- После живого запуска (владелец или исполнитель с ключом): причина записана в «Отчёт» и в `docs/knowledge/dev-environment.md`, новый раздел «OpenRouter embeddings» (ADR 0016 не переписывается). Если причина — имя модели в ответе, исправление в этой же задаче: сравнивать без учёта регистра (`StringComparison.OrdinalIgnoreCase`) и хранить в эмбеддинге имя из ответа. Другая причина — отдельная задача с планом.

## Анализ
`ImageGen/ImageGen.Core/OpenRouter/OpenRouterClient.cs`: `EmbedAsync`, `ReadEmbeddings`, `InvalidEmbeddingResponse`. `ImageGen/ImageGen.Core/Services/SemanticIndexService.cs`: `IndexAsync` (`catch (ProviderException exception)`), `ValidateResult`. Статус — `ImageGen/ImageGen.Core/Search/SemanticIndexStatus.cs`; экран — `SettingsPageModel.UpdateSemanticStatus`/текст статуса, разметка MAUI `SettingsPage.xaml`, Avalonia `SettingsView.axaml`. LiveCheck — `ImageGen/ImageGen.LiveCheck/` (по образцу сценариев чата OpenRouter; бюджет — `BudgetGuard`, `LiveCheckEstimates`).

## План
1. Падающие тесты текстов исключений (`OpenRouterClientTests` — ответы с ошибкой каждого вида; `SemanticIndexServiceTests` — модель с другим регистром даёт текст про модель; Warning с текстом).
2. Тексты исключений и журнал.
3. `ErrorDetail` в статусе и показ на экране настроек (тест `SettingsPageModelTests`).
4. Сценарий LiveCheck и его тест.
5. Живой запуск (если есть ключ) и запись причины; исправление по «Критериям».

Файлы: `ImageGen/ImageGen.Core/OpenRouter/OpenRouterClient.cs`, `ImageGen/ImageGen.Core/Services/SemanticIndexService.cs`, `ImageGen/ImageGen.Core/Search/SemanticIndexStatus.cs`, `ImageGen/ImageGen.Presentation/PageModels/SettingsPageModel.cs`, `ImageGen/ImageGen.Maui/Pages/SettingsPage.xaml`, `ImageGen/ImageGen.Avalonia/Views/SettingsView.axaml`, `ImageGen/ImageGen.LiveCheck/` (сценарий), тесты Core/Presentation/LiveCheck, `docs/knowledge/dev-environment.md`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.LiveCheck.Tests/ImageGen.LiveCheck.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
bash scripts/check.sh
```
Живой запуск: команда LiveCheck со сценарием `embeddings` (имя параметра — по текущему `LiveCheckRunner`).

### Порядок исполнения (для Luna)
- Сначала прочитай только файлы из строки «Файлы» и названные тесты; сигнатуры сверяй с текущим `main`.
- Шаги строго по порядку (ub-mode): падающий тест → запуск, убедиться, что он падает именно по описанной причине → минимальный фикс → тест зелёный.
- Не меняй файлы вне строки «Файлы», не переименовывай существующие члены сверх плана, не чини соседние находки — они в отдельных задачах.
- Если код расходится с планом (нет такого метода, другая сигнатура, тест уже есть), не импровизируй: остановись и запиши вопрос в «Комментарии».
- Команды `dotnet` выполняй по одной, не параллельно. После каждого изменённого проекта — `dotnet format <проект> --verify-no-changes`.
- В «Отчёте»: что изменено, команды и результаты, отличия от плана.

## Отчёт

- `OpenRouterClient.ReadEmbeddings` сообщает конкретный дефект ответа (data/count/index/vector/dimension/non-finite); `SemanticIndexService.ValidateResult` сообщает mismatch batch/model/vector/dimension. Сравнение модели без учёта регистра; исходное имя модели сохраняется в индексе.
- `SemanticIndexStatus.ErrorDetail` содержит безопасную причину до 200 символов; Warning включает ErrorKind и текст. `SettingsPageModel.SemanticSearchStatusText` показывает причину под статусом ошибки на MAUI и Avalonia через общую модель представления.
- LiveCheck `--only embeddings` выполняет один запрос с двумя короткими фразами, лимитирует оценку $0.01 и добавляет в отчёт HTTP, модель, число/размеры векторов, наличие индексов и usage. Парсер формы ответа приложения вызывается через `OpenRouterClient.EmbedAsync`; разбор trace-summary покрыт fixture-тестом.
- Проверки: Core 808/808; Presentation 514/514; LiveCheck 12/12; Avalonia 180 passed, 2 skipped (DPAPI и D-Bus); MAUI Android build — 0 warnings/errors; изменённые Core, Core.Tests, Presentation, Presentation.Tests, LiveCheck и LiveCheck.Tests прошли `dotnet format --verify-no-changes`.
- LiveCheck без ключа выполнен с `--only embeddings --budget-usd 0.01 --max-requests 1`: сценарий отмечен `unverified`, запрос не отправлялся. Живой ответ и исходную причину сбоя установить не удалось, ключ OpenRouter в среде отсутствует; после доступности ключа следует запустить указанный сценарий. Единица `usage.cost` остаётся неподтверждённой.
- План соблюдён; проверку на живом API заменил безопасным запуском без ключа, тестами фикстуры и тем же клиентским парсером, как и предусмотрено случаем отсутствия ключа.

## Комментарии

### 2026-11-07, claude-audit
Создано по отчёту владельца о сборке v0.1.11 на Android (MAUI, логи 2026-11-05 и 2026-11-06, скриншоты). План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
План одобрен ранее данным поручением «заапрувь все таски которые будут».
