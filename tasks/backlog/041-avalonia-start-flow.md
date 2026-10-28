---
id: 041
status: done
assignee:
claimed:
created: 2026-10-28
---

# Avalonia: навигация, диалоги, страница ключа и список сессий

## Описание
Сделать в Avalonia то, что в MAUI делают задачи 012–013: маршрутизатор страниц по строкам `Routes`, диалоги, страницу ключа и стартовый список сессий. Модели страниц — общие из `ImageGen.Presentation`, здесь только разметка и реализации сервисов.

Зависит от: 039 (040 — параллельно; до неё ключ в памяти).
Среда: Linux или Windows.

## Критерии готовности
- Запуск без ключа открывает страницу ключа; после верного ключа (подмена HTTP в тесте) — список сессий.
- Список сессий: создать (переход в чат — маршрут есть, страница чата в 042; до неё — заглушка «скоро»), открыть, удалить с подтверждением.
- Headless-тесты из плана проходят на Linux; привязки компилируемые; у кнопок и полей есть `AutomationProperties.Name`.

## Анализ
- Навигация MAUI — Shell с маршрутами `apikey`, `chat?sessionId=…`, `models?sessionId=…` и `..` (назад). В Avalonia Shell нет: нужен свой `AvaloniaNavigationService : INavigationService` — стек «маршрут → страница» в `ContentControl` главного окна. Параметры строки запроса передаются странице так же, как `IQueryAttributable` в MAUI (страница вызывает `ApplyQueryAttributes` своей модели).
- Диалоги: встроенных `DisplayAlert`/`DisplayPrompt` у Avalonia нет. `AvaloniaDialogService : IDialogService` — модальные окна (`Window.ShowDialog`) с текстом, кнопками и полем ввода для `PromptAsync`. В headless-тестах — тот же класс, окна создаются без экрана.
- Модели страниц `ApiKeyPageModel` и `SessionsPageModel` уже покрыты тестами в Presentation; здесь тестируется связка «разметка + навигация».

## План
1. `Services/AvaloniaNavigationService`: реестр «маршрут → (тип страницы, фабрика из DI)»; `GoToAsync(route)` разбирает путь и строку запроса, создаёт страницу, передаёт параметры, кладёт в стек и показывает; `GoBackAsync()` снимает верхнюю страницу и показывает предыдущую (вызывает у неё «появление», как `OnAppearing`). Неизвестный маршрут → `InvalidOperationException` (страница показывает alert, как в MAUI).
2. Жизненный цикл страниц: базовый класс или интерфейс страницы с `OnAppearing`/`OnDisappearing`, которые вызывает навигация; модели вызываются через `FireAndForgetSafeAsync`, как в MAUI.
3. `Services/AvaloniaDialogService`: `AlertAsync`, `ConfirmAsync`, `PromptAsync` (начальное значение, предел 4000 символов) — небольшие окна `DialogWindow` с разметкой и `AutomationProperties`.
4. Страницы (`Views/`): `ApiKeyView` (описание, поле пароля, кнопка, индикатор, статус) и `SessionsView` (кнопка «New session», список с датой, кнопка Delete) — по смыслу как MAUI-страницы, тексты из `AppResources`. Корневая страница при запуске — `SessionsView`.
5. Регистрации в `App`: страницы и модели (модели — как в MAUI: список сессий singleton, остальные transient), навигация, диалоги.
6. Тест-кейсы (headless, подмена HTTP через `StubHttpMessageHandler` из Core.Tests или аналог):
   - старт без ключа → верхняя страница `ApiKeyView`;
   - ввод ключа и «Check and save» с ответом 200 → возврат к `SessionsView`, ключ в `ISecretStore`;
   - ответ 401 → остаётся `ApiKeyView`, текст `ApiKeyInvalid`;
   - «New session» → в репозитории новая сессия, навигация на `chat?sessionId=<id>`;
   - Delete → подтверждение (диалог подменён «да») → строки нет; «нет» → строка осталась;
   - `GoBackAsync` с одной страницей в стеке → ничего не происходит;
   - неизвестный маршрут → alert с ошибкой.
7. Документация: `docs/knowledge/avalonia.md` (навигация и диалоги), `docs/architecture.md` (сервисы Avalonia).

### Команды проверки
```
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet format ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj --verify-no-changes
scripts/check.sh
```
Ручная проверка: запуск на Linux-десктопе и Windows — страница ключа, список сессий.

Файлы: `ImageGen/ImageGen.Avalonia/Services/AvaloniaNavigationService.cs`, `ImageGen/ImageGen.Avalonia/Services/AvaloniaDialogService.cs`, `ImageGen/ImageGen.Avalonia/Views/**`, `ImageGen/ImageGen.Avalonia/App.axaml.cs`, `ImageGen/ImageGen.Avalonia/MainWindow.axaml*`, `ImageGen/ImageGen.Avalonia.Tests/**`, `docs/knowledge/avalonia.md`, `docs/architecture.md`

## Отчёт
- Навигация: `Services/Navigation/AvaloniaNavigationService` (стек страниц в `PageArea` главного окна), `PageRoutes` (маршрут → фабрика страницы, в композиции), `RouteParser` (`chat?sessionId=…`), `IAppearing` (вызывается при показе, в том числе при возврате назад). Неизвестный маршрут — `InvalidOperationException`; без окна — тоже.
- Диалоги: `Services/AvaloniaDialogService` (alert, confirm, prompt; окно собрано в коде, у каждого контрола `AutomationProperties.Name`; окно немодальное, владелец — главное окно). Вместо отдельного `DialogWindow.axaml` (план) — код в сервисе: окно короткое, разметка не нужна.
- Страницы (`Views/`): `ApiKeyView` (поле с маскировкой, «Check and save», индикатор, статус, предупреждение о хранилище из `ApiKeyPageModel.PersistenceWarning`), `SessionsView` (кнопка «New session», список: название — кнопка «открыть», дата, «Delete»), `ComingSoonView` (заглушка для чата и моделей до 042). Тексты — через расширение `{markup:Res Ключ}` (`Markup/ResExtension`), потому что `x:Static` на `AppResources` компилятор Avalonia не разрешает.
- Композиция (`AppServices.Create`): новые опциональные параметры `openRouterHandler` и `dialogs` (для тестов); маршруты, навигация, страницы; `App` ставит страницу `sessions` при запуске.
- Presentation: `Routes.Sessions = "sessions"`; строка `ComingSoon` (`AppResources.resx` и аксессор).
- Отклонения от плана: диалоговое окно — в коде (см. выше); `Avalonia.Fonts.Inter` по-прежнему не подключён; заглушки `NotYetAvailable` удалены (навигация и диалоги реальные).
- TDD: красные тесты (сборка падала на отсутствующих типах `Services.Navigation`, `PageRoutes`, `Routes.Sessions`), затем код.
- Тесты (Avalonia, 29 + 2 пропуска): `RouteParserTests` (2), `AvaloniaNavigationServiceTests` (5), `ViewsAccessibilityTests` (1: у всех кнопок и полей есть имя доступности), `StartFlowTests` (7: старт без ключа → страница ключа; верный ключ (подмена HTTP 200) → список и ключ в хранилище; неверный (401) → остаётся на странице с текстом; «New session» → сессия в БД и маршрут чата; «открыть» → маршрут `chat?sessionId=`; удаление с подтверждением «да» и «нет»), `AppStartupTests` (1), остальные из 039–040.
- Результаты (Linux, облачная сессия): Release-сборка Avalonia-тестов — 0 предупреждений; `dotnet test` (Release) — 29/29, пропущены 2 (Secret Service без сессии D-Bus, DPAPI не Windows); `dotnet format --verify-no-changes` для `ImageGen.Avalonia`, `ImageGen.Avalonia.Tests`, `ImageGen.Presentation`, `ImageGen.Presentation.Tests` — 0; `ImageGen.Presentation.Tests` 114/114; MAUI `net10.0-android` — 0 ошибок, 0 предупреждений; `scripts/check.sh` — пройден.
- Ручная проверка на Linux (Xvfb, снимок экрана `tmp/shot/no-key.png`): запуск без ключа показывает страницу ключа с описанием, полем, неактивной кнопкой и предупреждением о хранилище (в контейнере Secret Service без коллекции `default`, поэтому ключ «только в памяти»).
- Не проверено: список сессий на экране в реальном окне (нужен сохранённый ключ: в контейнере он не сохраняется, тесты проверяют список на уровне моделей и разметки); диалоги на экране; Windows (запуск, диалоги, шрифты); Linux-рабочий стол с gnome-keyring. Поэтому статус `in-progress`.
- Среда: Linux (облачная сессия) с Xvfb.

**Проверено на Windows (2026-10-28, ревью после мерджа PR #19; Windows 11, SDK 10.0.402):**
- `dotnet format --verify-no-changes` для всех девяти проектов — без изменений.
- Тесты: Core 94, Data 37, Presentation 114, Avalonia 45 из 46 (пропущен только интеграционный тест Secret Service — он для Linux); тест DPAPI на Windows выполнен и прошёл.
- Сборка: MAUI `net10.0-windows10.0.19041.0` и `net10.0-android`, Avalonia — 0 ошибок, 0 предупреждений; CI на PR #19: задачи `core`, `maui`, `avalonia` зелёные.
- Запуск `ImageGen.Avalonia.exe`: окно «ImageGen» открывается, база создаётся в `%LOCALAPPDATA%ImageGen`.
- Закрыто: сборки и автоматические проверки выполнены; ручные пункты (экран на Windows и Linux-рабочем столе, сохранённый ключ после перезапуска, gnome-keyring login, реальный ответ OpenRouter) перенесены в задачу ручной проверки 046. macOS — не проверено (решение владельца).

## Комментарии

### 2026-10-28, владелец (в чате, записал ревью)
План одобрен.
