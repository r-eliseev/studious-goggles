---
id: 151
status: approved
assignee:
claimed:
created: 2026-10-30
---

# Уведомлять подписчиков после создания сессии

## Описание
В одобренном плане089 шаг2 обещан changes.Notify после успешного sessions.AddAsync в CreateAsync и regression Create_NotifiesSessionChanged. Ни вызова, ни теста в main нет. Остальные пути изменения уже уведомляют. Основной UI обычно маскирует пропуск повторным OnAppearing; это недостающий контракт, а не доказанный сбой каждого создания.

Зависит от: 154 (сначала изоляция обработчиков; обе задачи закрывают остатки089).
Среда: Linux или Windows.

## Критерии готовности
- CreateAsync после подтверждённого AddAsync публикует ровно одно SessionChanged с id созданной сессии.
- Если AddAsync падает или отменён, события нет; существующие поля/default settings не меняются.
- Исключение подписчика изолировано существующим SessionChangeNotifier, создание не дублируется.
- Остальные notification и список сессий сохраняют поведение089.

## Анализ
Evidence: SessionService.CreateAsync:20–33 и task089 план шаг2; Core.Tests/Services/SessionServiceTests не содержит Create_NotifiesSessionChanged. Не добавлять новый notifier/interface/перезагрузку списка. Исправление одной точки контракта, пересечение с другими задачами отсутствует по production файлам.

## План
1. Прочитать SessionService.CreateAsync, SessionChangeNotifier и Core SessionServiceTests setup, а также SessionsPageModel event handler (чтобы избежать повторного создания/двойной подписки).
2. Failing test Create_NotifiesSessionChanged: подписаться на существующий notifier, вызвать CreateAsync, проверить один id совпадает с возвращённой session.Id и AddAsync уже завершён к моменту notification.
3. Добавить changes.Notify(session.Id) сразу после await sessions.AddAsync и до return. Никакого события до записи и никакой повторной записи в event handler.
4. Добавить AddAsync exception/cancellation→нет notify; throwing subscriber→создание возвращает session, другие подписчики вызваны (использовать существующий notifier contract, не менять его реализацию).
5. Прогнать Core.Tests и Presentation.Tests, в089 дописать выполненный пункт2 и результат; вернуть089 в done только после154+151 и проверки её исходных критериев.

Файлы: `ImageGen/ImageGen.Core/Services/SessionService.cs`, `ImageGen/ImageGen.Core.Tests/Services/SessionServiceTests.cs`, `tasks/backlog/089-sessions-list-refresh.md`.

Проверка: Core.Tests; затем Presentation.Tests.

### Команды проверки

Выполнять по одной, из корня репозитория:

```bash
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
bash scripts/check.sh
```

### Порядок исполнения и проверки
План рассчитан на Luna: прочитать перечисленные файлы, выполнить шаги по порядку; сначала падающий регрессионный тест, затем минимальный фикс (ub-mode из AGENTS.md). Не реализовывать соседние задачи. Сигнатуры сверять с текущим main; при rebase сохранять чужие изменения. Не использовать платные API и не записывать секреты. Команды выполнять последовательно: общие build outputs не допускают параллельные dotnet build/test.
После изменения каждого проекта: `dotnet format ImageGen/<Проект>/<Проект>.csproj --verify-no-changes`; после всех изменений — перечисленные тесты и `bash scripts/check.sh`. В отчёте указать команды, результаты, ручные ограничения и отличие от плана. Код писать только после одобрения плана по docs/tasks.md.

## Отчёт

## Комментарии

### 2026-10-30, codex-audit
Создано по поручению владельца: подробный план для Luna. В этой задаче код ещё не исправлен; статус planned не означает одобрение реализации.

### 2026-10-30, владелец (в чате, записал codex)
План одобрен явным поручением «Заапрувь таски на фиксы». Можно реализовывать по описанному плану после готовности зависимостей в main и проверки среды/занятости по docs/tasks.md.
