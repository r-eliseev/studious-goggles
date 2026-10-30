---
id: 145
status: done
assignee:
claimed:
created: 2026-10-30
---

# Проверять референсы при запуске на другой модели

## Описание
RunOnModelAsync переносит source.ReferenceAttachmentIds в новую модель без ReferenceImageLimit.Of. ModelPicker показывает весь каталог. Исходная карточка с референсами → Other model → text-only модель или модель с меньшим лимитом: API получает недопустимый ввод либо транспорт молча игнорирует изображения.

Зависит от: 144.
Среда: Linux или Windows.

## Критерии готовности
- До создания run и provider submit проверяется поддержка и количество референсов целевой модели.
- При несовместимости запуск явно отклоняется, не создаёт run/job и не списывает деньги; референсы не отбрасываются молча.
- Совместимая модель получает все исходные ids в том же порядке, prompt/seed/parameters сохранены.
- При отсутствии референсов text-to-image работает как раньше.

## Анализ
Evidence: GenerationService.RunOnModelAsync:171–178 против StartRunWithAttachmentsAsync:84–91; ModelPickerPageModel.LoadAsync не фильтрует image-input. Для маленького фикса выбрано отклонение, а не скрытое усечение: пользователь выбирает совместимую модель сам. Не менять семантику cap обычного StartRunWithAttachmentsAsync. ReadModelAsync неизвестную модель трактует без подтверждённой reference support. Пересекается с144 по GenerationService/tests; выполнять последовательно.

## План
1. Прочитать RunOnModelAsync, ReadModelAsync, ReferenceImageLimit, ModelPickerPageModel.PickAsync и текущие тесты RunOnModelAsync.
2. Добавить тесты с source.ReferenceAttachmentIds: target limit=0, limit=1 при двух ids, target неизвестна в каталоге → исключение до AddRunAsync/submit. Проверять отсутствие новых записей, не только тип exception.
3. После загрузки source и AcquireStartLease, до CreateRunAsync, прочитать target model и ReferenceImageLimit.Of. Если source ids пусты, сохранить старое поведение и вообще не делать дополнительный запрос target catalog. Если limit=0 — ProviderException с ErrorKind.ModelWithoutReferences и понятным безопасным сообщением. Если count>limit — ProviderException с ErrorKind.BadRequest и сообщением, содержащим count/limit, без путей/изображений. Не добавлять новое enum значение ради этой проверки.
4. Добавить успех с limit=count и limit>count: все ids и их порядок сохранены; seed/prompt/parameters прежние. Не править Repeat/NewSeed/Edit и общий BuildRequestAsync.
5. ModelPicker уже ловит исключения и показывает Alert. Добавить Presentation test: несовместимый выбор показывает ошибку, не вызывает GoBackAsync; успешный выбор возвращает назад. UI-фильтрацию каталога в эту задачу не включать.
6. Кратко зафиксировать отклонение несовместимого Other model в docs/specs/chat-sessions.md в описании card actions.

Файлы: `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `ImageGen/ImageGen.Core.Tests/Services/GenerationServiceTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/ModelPickerPageModelTests.cs`, `docs/specs/chat-sessions.md`.

Проверка: Core.Tests; затем Presentation.Tests через `dotnet test --project ImageGen/ImageGen.<Проект>.Tests/ImageGen.<Проект>.Tests.csproj`.

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

`RunOnModelAsync` теперь проверяет поддержку и максимальное число reference images до создания прогона. Модель без объявленного лимита и неизвестная модель отклоняются как `ModelWithoutReferences`; превышение лимита — как `BadRequest` с фактическими количеством и пределом. При совместимости исходные attachment ids, порядок, prompt, seed и параметры сохраняются. UI показывает ошибку и остаётся на выборе модели. Запуск без референсов оставлен прежним.

Core.Tests — 639/639, Presentation.Tests — 389/389. Покрыты неизвестная модель, text-only, недостаточный лимит, лимит равный количеству и больший лимит; проверено, что несовместимость не создаёт run и не вызывает transport.

## Комментарии

### 2026-10-30, codex-audit
Создано по поручению владельца: подробный план для Luna. В этой задаче код ещё не исправлен; статус planned не означает одобрение реализации.

### 2026-10-30, владелец (в чате, записал codex)
План одобрен явным поручением «Заапрувь таски на фиксы». Можно реализовывать по описанному плану после готовности зависимостей в main и проверки среды/занятости по docs/tasks.md.
