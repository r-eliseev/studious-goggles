---
id: 239
status: done
assignee:
claimed:
created: 2026-11-07
---

# MAUI: постоянные AutomationId для UI-тестов

## Описание
Для E2E (ADR 0029, `docs/specs/ui-e2e.md`) элементы ищутся по `AutomationId`; в MAUI-разметке их нет ни одного (2026-11-07: 0 вхождений). Добавить id на элементы, которые трогают smoke-сценарии и сценарии с моком, и общий список констант.

Зависит от: —
Среда: Linux (сборка MAUI Android, Presentation-тесты).

## Критерии готовности
- Новый проект `ImageGen/ImageGen.E2E.Common/ImageGen.E2E.Common.csproj` (`net10.0`, без зависимостей) со статическим классом `TestIds` — строковые константы. Минимальный набор:
  - `ApiKeyPage.xaml`: `apikey.provider` (Picker провайдера), `apikey.entry` (Entry ключа), `apikey.save` (Button), `apikey.status` (Label со `StatusText` — найти по привязке `StatusText` в файле), `apikey.assistantModel` (Picker модели), `apikey.useModel` (Button `UseSelectedAssistantModelCommand`), `apikey.busy` (ActivityIndicator).
  - `SessionsPage.xaml`: `sessions.new` (кнопка пустого состояния `NewSessionCommand`), `sessions.list` (контрол `SessionList`), `sessions.settings` (пункт меню `OpenSettingsCommand`).
  - `ChatPage.xaml`: `chat.input` (поле ввода сообщения), `chat.send` (кнопка отправки), `chat.feed` (CollectionView ленты), `chat.models` (чип/кнопка моделей).
  - `SettingsPage.xaml`: `settings.page` (корневой ScrollView или первый контейнер).
  Если элемента с описанной ролью нет — не придумывать, записать в «Комментарии» и пропустить его.
- Значения `AutomationId` в XAML — строковые литералы, равные константам (`AutomationId="apikey.entry"`); `x:Static` не нужен (проект E2E.Common не подключается к MAUI).
- `SemanticProperties.*` не трогать.
- Тест `ImageGen/ImageGen.Presentation.Tests/E2E/AutomationIdsTests.cs` (xUnit v3, как остальные тесты проекта): для каждой константы `TestIds` (через reflection по `public const string`) ровно один файл в `ImageGen/ImageGen.Maui/Pages/*.xaml` (и `Controls/*.xaml`) содержит `AutomationId="<значение>"`; и нет дублей одного id в двух местах. Путь к репозиторию — подъём от `AppContext.BaseDirectory` до файла `ImageGen/ImageGen.slnx`. В `ImageGen.Presentation.Tests.csproj` — `ProjectReference` на `ImageGen.E2E.Common`.
- `ImageGen.E2E.Common` добавлен в `ImageGen/ImageGen.slnx`.
- Сборка MAUI Android проходит.

## Анализ
Справочно: исходная спецификация владельца — `docs/specs/ui-e2e-original/` (docx и md); при расхождении действуют ADR 0029 и `docs/specs/ui-e2e.md`.
`ImageGen/ImageGen.Maui/Pages/ApiKeyPage.xaml` (Picker/Entry/Button на строках ≈23–53), `SessionsPage.xaml` (строки 14, 19, 33, 41), `ChatPage.xaml`, `SettingsPage.xaml`. Документация MAUI (net-maui-10.0, «UI testing with Appium»): `AutomationId` → Android `MobileBy.Id`, Windows `AccessibilityId`.

## План
1. Проект `ImageGen.E2E.Common` с `TestIds` (сначала только `apikey.entry`), ссылка из Presentation.Tests, падающий `AutomationIdsTests` → id в `ApiKeyPage.xaml` → зелёный.
2. Остальные константы по одной группе (экран) за шаг: константы → тест красный → разметка → зелёный.
3. Сборка MAUI Android, формат.

Файлы: `ImageGen/ImageGen.E2E.Common/ImageGen.E2E.Common.csproj` (новый), `ImageGen/ImageGen.E2E.Common/TestIds.cs` (новый), `ImageGen/ImageGen.slnx`, `ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj`, `ImageGen/ImageGen.Presentation.Tests/E2E/AutomationIdsTests.cs` (новый), `ImageGen/ImageGen.Maui/Pages/ApiKeyPage.xaml`, `ImageGen/ImageGen.Maui/Pages/SessionsPage.xaml`, `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, `ImageGen/ImageGen.Maui/Pages/SettingsPage.xaml`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format ImageGen/ImageGen.E2E.Common/ImageGen.E2E.Common.csproj --verify-no-changes
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

### 2026-11-07, codex-gpt6
- Добавлены `ImageGen.E2E.Common` и `TestIds` с 15 постоянными id для ApiKey, Sessions, Chat и Settings; новый проект включён в solution и подключён к Presentation.Tests.
- Все id поставлены строковыми литералами на элементы из плана. `SemanticProperties` не менялись.
- Тест `AutomationIdsTests.EveryTestId_ExistsExactlyOnceInMauiMarkup` сначала упал (для `apikey.entry` было 0 вхождений), после добавления всех id проверяет, что каждый константный id встречается ровно один раз среди XAML в Pages/Controls и что значения констант уникальны.
- `dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj` — 526/526; `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android` — 0 предупреждений и ошибок.
- Форматирование проверено для E2E.Common и Presentation.Tests. Харнес запускается перед коммитом.

## Комментарии

### 2026-11-07, claude-audit
Создано по спецификации UI-тестов владельца (проверено: AutomationId в MAUI-разметке нет). План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
Спецификация UI-тестов прислана владельцем; выбор: NUnit + Allure, сначала smoke, затем мок. План одобрен ранее данным поручением «заапрувь все таски которые будут». Изменение `.github/workflows/` по этой спецификации владелец попросил явно (ADR 0029).
