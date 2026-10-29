---
id: 089
status: planned
assignee:
claimed:
created: 2026-10-29
---

# Список сессий: название и число прогонов не обновляются

## Описание
Замечание владельца (MAUI под Windows, широкое окно, 2026-10-29): после первого сообщения в новой сессии заголовок чата сменился на текст сообщения («нарисуй квадратную синюю луну»), а в списке сессий слева строка осталась «New session · 0 runs».

Вероятная причина: `SessionsPageModel` загружает список только в `OnAppearingAsync`; в широком окне список стоит рядом с чатом (067) и заново не появляется, поэтому не видит ни нового названия (`SetTitleFromFirstMessageAsync`), ни новых прогонов, ни переименования. Проверить в «Анализе», в том числе в Avalonia (общий `SessionsPageModel`).

Зависит от: —
Среда: Linux или Windows (логика в `Presentation`, тесты на Linux; ручная проверка MAUI — 016, Avalonia — 046).

## Критерии готовности
- После первого сообщения строка сессии в списке показывает новое название без перезапуска и без перехода между экранами.
- Число прогонов в строке растёт после каждого прогона.
- Группы «Today / Earlier» и подсветка текущей сессии не ломаются; прокрутка списка и введённый поиск не сбрасываются.
- Тесты `Presentation` на обновление строки при изменении сессии; поведение одинаково в MAUI и Avalonia.

## Анализ
Причина подтверждена по коду (2026-10-29), в обеих оболочках одна и та же.

- `SessionsPageModel` (одиночка в DI обеих оболочек) строит `Sessions`, `Today`, `Earlier` только в `OnAppearingAsync`: читает `SessionService.ListAsync` и `CountRunsAsync`, пересоздаёт строки. Других путей обновления строки нет, кроме переименования из самого списка (`RenameSessionAsync` правит `item.Title`) и удаления.
- Кто вызывает `OnAppearingAsync`: MAUI — `SessionsPage.OnAppearing` и `ChatPage.OnAppearing` (список рядом с чатом); Avalonia — `SessionsView` и `ChatView.OnAppearing` (через `SessionPicker`). То есть список перечитывается, только когда открывается чат или экран списка, а не после действий в чате.
- Название меняет ядро: `AssistantService` после сохранения сообщения пользователя вызывает `SessionService.SetTitleFromFirstMessageAsync` (ставит название из первого сообщения и всякий раз сдвигает `UpdatedAt`). `ChatPageModel.ReloadAsync` потом читает ленту и показывает новый `Title` в шапке, а список об этом не знает.
- Прогоны создаёт `GenerationService` (`runs.AddRunAsync`): по сообщению ассистента и по действиям карточки (Repeat, New seed, Edit, Other model). `UpdatedAt` сессии при этом не меняется. Событий об изменении сессии в ядре нет; есть только `GenerationService.GenerationUpdated` (по генерациям, в фоновом потоке) и `ICurrentSessionTracker.Changed` (какая сессия открыта).
- `SessionItemViewModel`: `Title` и `RunCount` наблюдаемые (`DetailsText` пересчитывается при смене `RunCount`), `UpdatedText` — только для чтения, задаётся в конструкторе.
- Строки в `Today`, `Earlier`, `SearchResults` и `Sessions` — одни и те же объекты, поэтому правка строки на месте видна во всех списках сразу; прокрутка (`ScrollView` + `BindableLayout` в MAUI, аналог в Avalonia) и поиск не сбрасываются, если коллекции не пересоздавать. `OnAppearingAsync` же сбрасывает `SearchText` и пересоздаёт строки — поэтому обновлять через него нельзя.

Решение: ядро сообщает «сессия изменилась» (id), список перечитывает одну сессию и правит её строку на месте. Через ядро, а не через `ChatPageModel`, потому что изменения приходят из разных мест (ассистент в фоне, действия карточек, будущая Web-оболочка). Новый тип ядра — небольшой издатель событий-одиночка, его получают `SessionService` и `GenerationService` (необязательным последним параметром конструктора, как `IDebugTrace`, чтобы не править шесть мест создания в тестах); событие может прийти в фоновом потоке, `SessionsPageModel` переносит правку в UI-поток через `IMainThread` (как `ChatPageModel`). Это меняет устройство: `docs/architecture.md`, абзац «Обновление экрана», дополнить одной фразой.

Порядок в списке: сортировка по `UpdatedAt`, как при загрузке. Если `UpdatedAt` сессии изменился, строка переносится в начало «Today» (из «Earlier» тоже); переименование `UpdatedAt` не меняет (S1.2) и строку не двигает. Прогон без сообщения (Repeat и др.) `UpdatedAt` не меняет — строка обновляет только число прогонов и остаётся на месте. Нужно ли поднимать сессию при новом прогоне, спецификация не говорит — вопрос в «Комментариях», допущение: нет (как сейчас при загрузке).

Пересечения с незавершёнными задачами:
- 101 (поиск: опечатка в недописанном слове) — может менять `SessionsPageModel` (`SearchText`) и `SessionsPageModelTests`; основная правка 101 в `ImageGen.Core/Search/FuzzyMatcher.cs`. Порядок любой; если обе меняют `SessionsPageModel`, лучше 089 первой (её правка — новые члены, поиск она не трогает), при `rebase` сохранять обе стороны.
- 096 (сводка прогона) меняет `RunItemViewModel` и, возможно, `GenerationService`-связанные тесты; с этой задачей файлы не пересекаются, кроме возможной правки `GenerationService.cs` — здесь только новый необязательный параметр и одна строка после `AddRunAsync`.
- 088 файлов этой задачи не трогает.

## План
TDD: сначала падающий тест, потом минимальный код. Всё выполняется и проверяется на Linux.

1. **Ядро: издатель изменений сессии.** Новый тип `SessionChangeNotifier` (`ImageGen.Core/Services/`, одиночка в `CoreServiceCollectionExtensions`): событие `SessionChanged` (`EventHandler<Guid>` или аргументы с `SessionId`) и метод `Notify(Guid sessionId)`. Подписчик может получить событие в любом потоке; исключение подписчика не должно ломать операцию ядра (ловится и пишется в журнал).
   Тесты (`ImageGen.Core.Tests/Services/SessionServiceTests.cs`, `GenerationServiceTests.cs`):
   - `SetTitleFromFirstMessageAsync` → одно событие с id сессии (и при первом сообщении, и при последующих: `UpdatedAt` меняется);
   - `RenameAsync` → событие; `UpdateSettingsAsync`, `UpdateGenerationParametersAsync` → событие (меняют запись сессии);
   - `CreateAsync` → событие с id новой сессии;
   - старт прогона (`StartRunWithAttachmentsAsync`, а значит и Repeat/Edit/Other model) → событие с id сессии после записи прогона;
   - без издателя (параметр не передан) всё работает как раньше.
2. **`SessionItemViewModel`:** `UpdatedText` становится наблюдаемым; новый метод `Apply(Session session, int runCount)` обновляет `Title`, `RunCount`, `UpdatedText` и запоминает `UpdatedAt` (нужен для порядка). `DetailsText` уведомляется при смене `UpdatedText` и `RunCount`.
   Тесты (`SessionItemViewModelTests`): `Apply` с новым названием → `Title` новый, событие `PropertyChanged(Title)`; с новым числом прогонов → `DetailsText` содержит «2 runs», уведомление `DetailsText`; `IsCurrent` и `SearchLine` не меняются.
3. **`SessionsPageModel`:** новые зависимости `SessionChangeNotifier` и `IMainThread`. Подписка один раз (повторный вызов `OnAppearingAsync` не удваивает её: снять и подписать, как `tracker.Changed`). Обработчик:
   - в UI-потоке запустить `RefreshSessionAsync(id)` (`FireAndForgetSafeAsync`);
   - `RefreshSessionAsync`: прочитать `SessionService.GetAsync(id)` и число прогонов (`CountRunsAsync`, значение по id или 0); сессии нет (удалена) → убрать строку из всех коллекций; строки нет → создать её (как при загрузке: `IsCurrent` по трекеру) и вставить в начало `Sessions` и «Today»; строка есть → `Apply`; если `UpdatedAt` стал новее прежнего — переместить строку в начало `Sessions` и «Today» (из «Earlier» — удалить там и вставить в «Today»; внутри «Today» — `Move`, чтобы не пересоздавать элементы), затем `NotifySessionsCount`;
   - `SearchText`, `SearchResults` и `SearchLine` не трогать; повторный поиск не запускать;
   - ошибка чтения → журнал/диалог как в остальных методах, список не очищается.
   Тесты (`SessionsPageModelTests`, издатель настоящий, `IMainThread` выполняет сразу, репозитории — подмены как в файле):
   - список загружен с «New session · 0 runs»; сессия переименована в ядре и `Notify` → строка показывает новое название, `Sessions.Count` прежний, тот же объект строки;
   - `Notify` после нового прогона (подмена `CountRunsBySessionAsync` возвращает 1) → `DetailsText` с «1 run»;
   - сессия из «Earlier» с новым `UpdatedAt` сегодня → строка первой в «Today», в «Earlier» её нет;
   - переименование без смены `UpdatedAt` → порядок в «Today» не меняется;
   - введён поиск, есть результаты; `Notify` → `SearchText` прежний, строка в `SearchResults` осталась и показывает новое название;
   - текущая сессия (`tracker.CurrentId`) после обновления сохраняет `IsCurrent = true`;
   - `Notify` для неизвестной сессии → строка добавлена в начало «Today»; для удалённой (`GetAsync` → null) → строка убрана, `HasNoSessions` пересчитан;
   - `OnAppearingAsync` дважды, затем один `Notify` → данные сессии прочитаны один раз (подписка не удвоилась).
4. **Оболочки.** Регистрации DI не меняются (новые зависимости — одиночки ядра и `IMainThread`, уже зарегистрированы в `MauiProgram` и `Composition/AppServices.cs`; проверить сборкой и тестом композиции Avalonia). Разметка `SessionsListView` (MAUI и Avalonia) уже привязана к `Title` и `DetailsText` — правка не нужна; если при проверке окажется, что строка не обновляется, искать в разметке одноразовые привязки. Поведение одинаково, потому что логика вся в `Presentation`.
5. **Документация:** `docs/architecture.md`, абзац «Обновление экрана»: список сессий обновляет строку по событию изменения сессии из ядра. Спецификация S1.2 не меняется. В `tasks/v0.1/016-*.md` и `tasks/backlog/046-*.md` дописать ручной пункт: широкое окно, новая сессия, первое сообщение → строка слева сразу с новым названием и «1 run» после прогона; поиск и прокрутка не сбрасываются.

Крайние случаи: событие приходит, пока список ещё ни разу не загружен (до `OnAppearingAsync`) — подписки ещё нет, ничего не делается; два события подряд для одной сессии — последнее чтение побеждает, строк не становится две (поиск строки по id перед вставкой в UI-потоке); смена суток — «Today» определяется по `TimeProvider`, как при загрузке.

Команды проверки:
- `dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj`
- `dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj`
- `dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj`
- `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android` (при наличии Android SDK; иначе CI)
- `dotnet format ImageGen/<Проект>/<Проект>.csproj --verify-no-changes` для Core, Presentation и их тестов
- `scripts/check.sh`

Файлы: `ImageGen/ImageGen.Core/Services/SessionChangeNotifier.cs` (новый), `ImageGen/ImageGen.Core/Services/SessionService.cs`, `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `ImageGen/ImageGen.Core/CoreServiceCollectionExtensions.cs`, `ImageGen/ImageGen.Core.Tests/Services/SessionServiceTests.cs`, `ImageGen/ImageGen.Core.Tests/Services/GenerationServiceTests.cs`, `ImageGen/ImageGen.Presentation/PageModels/SessionsPageModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/SessionItemViewModel.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/SessionsPageModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/SessionItemViewModelTests.cs`, `docs/architecture.md`, `tasks/v0.1/016-skeleton-manual-check.md`, `tasks/backlog/046-desktop-manual-check.md`.

## Отчёт

## Комментарии
### 2026-10-29, claude-plan
Вопрос владельцу: поднимать ли сессию наверх списка (и в «Today»), когда в ней запущен прогон без нового сообщения (Repeat, New seed, Edit, Other model)? Сейчас время изменения сессии двигает только сообщение. Допущение в плане: не поднимать, обновлять только число прогонов.
