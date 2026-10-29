---
id: 079
status: in-progress
assignee: claude-536f
claimed: 2026-10-29T05:24:35Z
created: 2026-10-28
---

# Поиск по сессиям: нечёткий поиск по названиям, промптам и описаниям

## Описание
Решения владельца (2026-10-28): поиск — не только по названиям, а «контекстный»: по названиям сессий, промптам (прогонов и генераций) и описаниям картинок (Img2Text, задача 019). Эта задача — простой нечёткий поиск без эмбеддингов; поиск по смыслу (тексты и картинки) — 081. Результаты — список сессий с найденным фрагментом под названием («prompt: …lighthouse on a rocky…»); открытие — чат сессии.

Справочник: `docs/design/implementation.md`; макеты: `docs/design/prototype.html` (https://claude.ai/artifact/M9QzPf39KLkRFTnYBtCi6F); решения владельца — задача 074 и «Комментарии» этой задачи.

Зависит от: 075 (те же файлы списка).
Среда: Linux или Windows (Avalonia — headless-тесты на Linux; MAUI — сборка Android на Linux с Android SDK, Windows-цель собирает CI; ручные проверки — 016 и 046).

## Критерии готовности
- Поле «Search sessions» над списком (значок `Search`, кнопка очистки); поиск начинается через 250 мс после последнего ввода; пустой запрос — обычный список.
- Совпадения без учёта регистра, с опечаткой в одну букву для слов от 4 букв, все слова запроса должны найтись (в одном тексте).
- Результат: сессии по убыванию релевантности, при равенстве — по времени обновления; под названием — фрагмент до 80 символов с многоточиями и меткой источника (`title`, `prompt`, `description`).
- «Nothing found» при пустом результате.
- SQL — только параметры; поиск идёт не на UI-потоке.
- Тесты из «Плана» проходят.

## Анализ
- Данных немного (один пользователь), поэтому: один запрос забирает все тексты, сравнение — в памяти, в ядре (чистая функция, легко тестировать).
- Таблицы (`ImageGen.Data/Migrations/0001_initial.sql`, `0005_image_descriptions.sql`): `sessions(id, title)`, `runs(session_id, prompt)`, `generations(session_id, prompt)`, `image_descriptions(session_id, text)`. Запрос: `SELECT id, 0, title FROM sessions UNION ALL SELECT DISTINCT session_id, 1, prompt FROM runs UNION ALL SELECT DISTINCT session_id, 1, prompt FROM generations UNION ALL SELECT session_id, 2, text FROM image_descriptions` (вид: 0 — title, 1 — prompt, 2 — description).
- Сообщения чата в поиск не входят (владелец назвал названия, промпты, описания).

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов, ключей и строк переноси дословно. Если шаг не получается так, как написано (API другое, компилятор не принимает разметку), не придумывай обход: опиши в «Комментариях», что пробовал, поставь `blocked` и остановись.

1. Core (`ImageGen.Core/Search/`, пространство `ImageGen.Core.Search`):
   - `SearchTextKind` (enum `Title`, `Prompt`, `Description`), `SearchText` (record: `Guid SessionId`, `SearchTextKind Kind`, `string Text`), `SessionSearchHit` (record: `Guid SessionId`, `int Score`, `SearchTextKind Kind`, `string Snippet`).
   - `ISearchTextSource` (`Task<IReadOnlyList<SearchText>> ListAllAsync(CancellationToken)`) — реализация в Data.
   - `FuzzyMatcher` (static): `Tokenize(string)` — нижний регистр (`ToLowerInvariant`), разбиение по символам не-буквам и не-цифрам; `Match(IReadOnlyList<string> queryTokens, string text)` → `(int Score, int FirstIndex)?`: для каждого токена запроса ищется слово текста: начинается с токена — 3 очка; содержит — 2; токен длиной ≥ 4 и расстояние Левенштейна ≤ 1 с словом (или с началом слова той же длины) — 1; если хоть один токен не найден — `null`.
   - `SessionSearchService(ISearchTextSource source)`: `SearchAsync(string query, CancellationToken)` → `IReadOnlyList<SessionSearchHit>`; вес вида: Title ×3, Prompt ×2, Description ×1; по сессии — лучший текст; фрагмент — 80 символов вокруг `FirstIndex` с «…» по краям.
   - Регистрация в `AddImageGenCore`.
   - Тесты `ImageGen.Core.Tests/Search/`: `FuzzyMatcherTests` — `Match_Prefix_Scores3`, `Match_Substring_Scores2`, `Match_OneTypoLongWord_Scores1`, `Match_TypoShortWord_NoMatch`, `Match_AllTokensRequired`, `Tokenize_SplitsPunctuationAndLowercases`, `Match_Cyrillic_Works`; `SessionSearchServiceTests` — `Search_TitleBeatsPrompt`, `Search_SnippetHasEllipsis`, `Search_EmptyQuery_ReturnsEmpty`, `Search_GroupsBySession_BestHit`.
2. Data: `Repositories/SearchTextSource.cs` (SQL из «Анализа», регистрация в `AddImageGenData`); тест `SearchTextSourceTests.ListAll_ReturnsAllKinds`.
3. Presentation: `SessionsPageModel.SearchText` (debounce 250 мс через `CancellationTokenSource` и `Task.Delay` с `TimeProvider`, чтобы тест не ждал), `SearchResults` (`ObservableCollection<SessionItemViewModel>` с заполненными `Snippet` и `SnippetKindText`), `IsSearching`, `HasNoResults`, `ClearSearchCommand`. Строки: `SearchSessions` = "Search sessions", `NothingFound` = "Nothing found", `SearchInTitle` = "title", `SearchInPrompt` = "prompt", `SearchInDescription` = "description". Тесты: `Search_ShowsResultsWithSnippet`, `Search_Debounced_OnlyLastQueryRuns`, `ClearSearch_ShowsAllSessions`, `Search_NoHits_ShowsNothingFound`.
4. Avalonia `SessionsListView`: `TextBox` поиска (`x:Name="SearchBox"`, метод `FocusSearch()` для 073), список результатов вместо групп при непустом запросе; `TextBlock.meta` с «prompt: …фрагмент…». MAUI `Controls/SessionsListView.xaml`: `SearchBar`.
5. Тест Avalonia `Views/SessionSearchTests.cs`: ввод «lighthose» находит сессию с промптом «lighthouse», под названием фрагмент.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android -p:AndroidSdkDirectory=$ANDROID_HOME
dotnet format ImageGen/<каждый изменённый проект>/<проект>.csproj --verify-no-changes
scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Search/*` (новые), `ImageGen/ImageGen.Core/CoreServiceCollectionExtensions.cs`, `ImageGen/ImageGen.Data/Repositories/SearchTextSource.cs` (новый), `ImageGen/ImageGen.Data/DataServiceCollectionExtensions.cs`, `ImageGen/ImageGen.Presentation/PageModels/{SessionsPageModel,SessionItemViewModel}.cs`, строки, `ImageGen/ImageGen.Avalonia/Views/SessionsListView.*`, `ImageGen/ImageGen.Maui/Controls/SessionsListView.*`, тесты, `docs/architecture.md` (модуль поиска в ядре)

## Отчёт

## Комментарии

### 2026-10-28, владелец (в чате, записал claude-cloud-t9wd)
Поиск: «давай сделаем полноценный контекстный поиск: и по промптам, и по названиям, и по описаниям. Первый таск — на простой фаззи-поиск, отдельный — на эмбеддинги». Результаты — сессии с фрагментом.
