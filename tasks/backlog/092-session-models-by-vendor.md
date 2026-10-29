---
id: 092
status: planned
assignee:
claimed:
created: 2026-10-29
---

# Модели сессии: группы по производителю, раскрывающиеся

## Описание
Замечание владельца (2026-10-29): модели в списке нужно сгруппировать по производителю (Black Forest Labs, Google, OpenAI, xAI и т. д.) и показывать группы раскрывающимися списками. Касается обеих вкладок из 090: и моделей картинок, и моделей ассистента.

Откуда надёжно брать производителя, неизвестно: префикс id до `/` (`black-forest-labs/flux.2-klein-4b`, `x-ai/grok-imagine-image-2.0`) или часть названия до двоеточия («xAI: Grok Imagine Image 2.0»); у fal формат id другой. Решить в «Анализе»; группировка и имя группы — в `Presentation`, не в оболочках.

Зависит от: 090, 091 (те же файлы экрана моделей).
Среда: Linux или Windows (как у 090).

## Критерии готовности
- Модели разбиты на группы по производителю; группы по алфавиту, внутри группы по названию.
- Группа раскрывается и сворачивается; в заголовке имя производителя и число моделей (для картинок и число выбранных).
- При открытии экрана раскрыты группы с выбранными моделями; при поиске видны только группы с совпадениями, и они раскрыты.
- Модели без понятного производителя попадают в группу «Other».
- Тесты `Presentation` на группировку (fal, OpenRouter, без префикса) и на поиск; тест Avalonia на заголовки групп.

## Анализ
Откуда брать производителя — проверено на живых каталогах 2026-10-29 (без ключа; ответы сохранены в `tmp/093/`, в git не входят):
- OpenRouter, картинки (`GET /api/v1/images/models`, 61 модель): у всех id есть префикс до `/` (13 префиксов) и у всех название вида «Производитель: Модель». Префикс и название почти всегда совпадают по смыслу (`black-forest-labs` → «Black Forest Labs», `bytedance-seed` → «ByteDance Seed», `microsoft` → «Microsoft AI»), но не всегда однозначно: у `x-ai` одна модель «xAI: …», другая «SpaceXAI: …».
- OpenRouter, чат с `tools` (391 модель, кандидаты в ассистенты): есть id-псевдонимы с `~` (`~anthropic/…`, `~openai/…`) — тот же производитель, что без `~`; два префикса с одним производителем (`meta` и `meta-llama` → «Meta»); 11 моделей без «Производитель:» в названии (`openrouter/auto` «Auto Router», `mistralai/mistral-large` «Mistral Large», `rekaai/reka-edge` «Reka Edge»).
- fal (`GET https://api.fal.ai/v1/models?category=text-to-image`, открыт без ключа): id вида `fal-ai/flux/schnell`, `fal-ai/nano-banana-pro`, но и `openai/gpt-image-2`. Префикс `fal-ai` — пространство имён fal, а не производитель. Поля производителя в `metadata` нет (есть `display_name`, `group.key` — семейство, `tags`); названия без «Производитель:» («FLUX.1 [schnell]», «Nano Banana Pro»).
- OpenAI-совместимые модели из конфига (`ConfiguredImageModel`) могут быть без `/` вообще.

Вывод: только название ненадёжно (нет у fal и у части чата, разные написания), только префикс — некрасив («black-forest-labs») и дробит одного производителя. Правило — ключ по префиксу id, подпись по названию:
1. Префикс = часть id до первого `/` после удаления ведущих `~`; нет `/` → производитель неизвестен.
2. У fal префикс `fal-ai` → производитель неизвестен (допущение, вопрос в «Комментариях»).
3. Подпись группы префикса — самая частая «Производитель» (часть названия до `: `) среди её моделей; при равенстве — первая по алфавиту без учёта регистра (`x-ai` → «SpaceXAI»); если ни у одной модели нет `: ` — сам префикс (`openrouter`, `rekaai`).
4. Группы с одинаковой подписью (без учёта регистра) сливаются (`meta` + `meta-llama` → «Meta»); модели разных провайдеров одного производителя попадают в одну группу (`openai/gpt-image-2` у fal — к «OpenAI», если OpenRouter даёт такую подпись).
5. Неизвестный производитель → группа «Other».
6. Порядок: группы по подписи без учёта регистра, «Other» последней (допущение); внутри — по названию, затем по id провайдера (как `GetAllImageModelsAsync`).

Группировка и подписи — в `Presentation` (чистая функция, тестируется без UI); оболочки только показывают группы.

Раскрытие (машина состояний на группу; ключ группы — подпись в нижнем регистре, у «Other» — служебный ключ):
- Первое открытие экрана: раскрыта ⇔ в группе есть выбранная модель (картинки — отмеченные, ассистент — выбранный).
- Пользователь нажал заголовок без поиска → состояние меняется и запоминается (`_expanded` в модели страницы).
- Поиск непустой → видны только группы с совпадениями, все раскрыты; нажатие на заголовок меняет только показ, запомненное не трогает.
- Поиск очищен → запомненные состояния возвращаются.
- «Refresh» → запомненные состояния сохраняются; новая группа с выбранной моделью раскрыта, без — свёрнута.
- Свёрнутая группа держит пустой список строк (`Rows`), раскрытая — совпадающие строки. Так MAUI (`BindableLayout`) не создаёт представления свёрнутых групп: у fal моделей сотни (`docs/knowledge/maui.md`: не создавать тысячи `View` в `BindableLayout`).

Заголовок: картинки — «Black Forest Labs · 6 models · 1 selected», ассистент — «Anthropic · 30 models». Число моделей — совпадающие с поиском; число выбранных — отмеченные среди них. Верхний счётчик «Image models · N selected» (090) остаётся по всему каталогу.

Разметка: MAUI — кнопка-заголовок со значком `Expand`/`Forward` (`IconGlyphs`) и вложенный `BindableLayout` строк; встроенного `Expander` нет, у `CommunityToolkit.Maui` он есть, но поведение диктора на Windows неизвестно, поэтому кнопка. Avalonia — встроенный `Expander` (клавиатура и шаблон доступности «развернуть/свернуть» из коробки) с `IsExpanded` ↔ группа (TwoWay).

Плоские `Models` и `AssistantModels` остаются (их используют тесты и `SessionModelsFlowTests`); разметка переходит на группы.

Пересечения: после 090 и 091 (те же `SessionModelsPageModel`, `Controls/SessionModelsView.xaml`, `SessionModelsView.axaml`, `AppResources`); до 093 (та же строка модели). Экран «Other model…» (`ModelPickerPageModel`) не меняется: там свой «Used in this session» / «All models» (S4.5).

## План
Типы и члены (`ImageGen.Presentation/PageModels/`):
- `ModelVendors` (статический, чистый):
  - `PrefixOf(providerId, modelId)` → префикс или null (правила 1–2);
  - `LabelOf(name)` → часть до `: ` или null;
  - `Group<T>(items, providerId, modelId, name)` → упорядоченный список `(Key, Label, Items)` по правилам 3–6; подпись «Other» — `AppResources.ModelGroupOther`.
- `ModelGroupViewModel` (база, `ObservableObject`): `Key`, `Name`, `IsExpanded` (наблюдаемое; изменение сообщает модели страницы через переданный делегат, как `select` у `AssistantModelOptionViewModel`), `ToggleCommand`, `HeaderText`, `AutomationName` («…, expanded/collapsed»).
- `ImageModelGroupViewModel : ModelGroupViewModel` — `Rows: ObservableCollection<ModelOptionViewModel>`, `SelectedCount`; заголовок пересчитывается при смене `IsSelected` строки.
- `AssistantModelGroupViewModel : ModelGroupViewModel` — `Rows: ObservableCollection<AssistantModelOptionViewModel>`.
- `SessionModelsPageModel`: `ImageModelGroups`, `AssistantModelGroups` (`ObservableCollection`); `_expanded` (ключ → bool); `ApplyFilter` строит группы из отфильтрованных строк и применяет машину состояний из «Анализа»; загрузка задаёт начальные состояния.
- `AppResources`: `ModelGroupOther` («Other»), `ModelGroupImageHeaderFormat` («{0} · {1} models · {2} selected»), `ModelGroupAssistantHeaderFormat` («{0} · {1} models»), `ModelGroupExpanded` («expanded»), `ModelGroupCollapsed` («collapsed»).

Шаги (TDD):
1. `ModelVendorsTests` (сначала падающие), условие → результат:
   - OpenRouter `black-forest-labs/flux.2-klein-4b` «Black Forest Labs: FLUX.2 Klein 4B» → группа «Black Forest Labs»;
   - `x-ai/a` «xAI: A» и `x-ai/b` «SpaceXAI: B» → одна группа «SpaceXAI» (равенство → алфавит);
   - `~anthropic/claude-latest` «Anthropic: Claude» и `anthropic/claude-x` → одна группа;
   - `meta/x` «Meta: X» и `meta-llama/y` «Meta: Y» → одна группа «Meta»;
   - `openrouter/auto` «Auto Router» без других моделей префикса → группа «openrouter»; `mistralai/mistral-large` «Mistral Large» рядом с «Mistral: …» → «Mistral»;
   - fal `fal-ai/flux/dev` «FLUX.1 [dev]» → «Other»; fal `openai/gpt-image-2` вместе с OpenRouter «OpenAI: GPT Image 1» → «OpenAI»;
   - id без `/` (`dall-e-3`, OpenAI-совместимый) → «Other»;
   - порядок: группы по алфавиту без учёта регистра, «Other» последней; внутри — по названию, затем провайдеру; пустой вход → пустой список.
2. `SessionModelsPageModelTests` (сначала падающие):
   - открытие: группа с сохранённой моделью раскрыта и её `Rows` полны, остальные свёрнуты, `Rows` пусты; у ассистента раскрыта группа сохранённого;
   - `ToggleCommand` на свёрнутой → `Rows` заполнены, заголовок «… · N models · M selected»;
   - галочка в строке → `SelectedCount` и заголовок группы меняются, верхний счётчик тоже;
   - поиск «flux» → только группы с совпадениями, все раскрыты, в `Rows` только совпадения; очистка → прежние состояния;
   - свернуть группу во время поиска → после очистки запомненное состояние не изменилось;
   - «Refresh» → состояния групп сохранились, отметки тоже;
   - «Save» сохраняет то же, что раньше (существующие тесты зелёные).
3. Avalonia (headless, сначала падающие): в `SessionModelsView` у вкладки картинок есть `Expander` с заголовком группы из каталога заглушки (с числами), группа с выбранной моделью раскрыта, раскрытие показывает `CheckBox` строк; на вкладке ассистента — группы ассистента; `ViewsAccessibilityTests` — у заголовков есть имена. В `OpenRouterFakes.CatalogJson` — названия «Vendor: Model» и модель другого производителя.
4. Разметка: Avalonia `SessionModelsView.axaml` — `ItemsControl` групп с `Expander`; MAUI `Controls/SessionModelsView.xaml` — `BindableLayout` групп, кнопка-заголовок (`SemanticProperties.Description` = `AutomationName`) и вложенный `BindableLayout` `Rows`. Сборка Android.
5. Документация: `docs/specs/chat-sessions.md` S5 — группы, правила производителя, раскрытие, поиск; ручные пункты в 016 и 046.

Проверка: `dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj`, `dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj`, `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android`, `dotnet format … --verify-no-changes` по изменённым проектам, `scripts/check.sh`.

Файлы: `ImageGen/ImageGen.Presentation/PageModels/ModelVendors.cs`, `ModelGroupViewModel.cs`, `ImageModelGroupViewModel.cs`, `AssistantModelGroupViewModel.cs` (новые), `ImageGen/ImageGen.Presentation/PageModels/SessionModelsPageModel.cs`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx` (и `.cs`, если строки объявляются там), `ImageGen/ImageGen.Maui/Controls/SessionModelsView.xaml(.cs)`, `ImageGen/ImageGen.Avalonia/Views/SessionModelsView.axaml`, `ImageGen/ImageGen.Presentation.Tests/PageModels/ModelVendorsTests.cs` (новый), `ImageGen/ImageGen.Presentation.Tests/PageModels/SessionModelsPageModelTests.cs`, `ImageGen/ImageGen.Avalonia.Tests/TestDoubles/OpenRouterFakes.cs`, `ImageGen/ImageGen.Avalonia.Tests/Flows/SessionModelsFlowTests.cs`, `ImageGen/ImageGen.Avalonia.Tests/Views/ViewsAccessibilityTests.cs`, `docs/specs/chat-sessions.md`, `tasks/v0.1/016-*.md`, `tasks/backlog/046-*.md`.

## Отчёт

## Комментарии
### 2026-10-29, claude-plan
Вопросы владельцу по плану (до ответа план идёт по допущениям):
1. Модели fal с id `fal-ai/…` (FLUX, Nano Banana и др.) — производителя в каталоге fal нет. Допущение: группа «Other». Варианты: отдельная группа «fal.ai» или ручная таблица семейств (`flux` → Black Forest Labs и т. п.).
2. Группа «Other» — последней, а не по алфавиту (допущение).
3. Подпись группы берётся из названий моделей OpenRouter; у `x-ai` сейчас встречаются «xAI» и «SpaceXAI» — при равенстве выбирается первая по алфавиту («SpaceXAI»). Подходит?
