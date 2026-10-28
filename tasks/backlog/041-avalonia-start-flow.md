---
id: 041
status: approved
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

## Комментарии

### 2026-10-28, владелец (в чате, записал ревью)
План одобрен.
