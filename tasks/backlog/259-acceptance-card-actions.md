---
id: 259
status: planned
assignee:
claimed:
created: 2026-11-09
---

# Приёмочные сценарии: действия над карточкой генерации

## Описание
Автоматизировать раздел «Действия над генерацией» каталога: ACT-01 … ACT-08. Файл `Features/CardActions.feature`, тег `@actions`.

Зависит от: 253, 257.
Среда: Linux или Windows (headless).

## Критерии готовности
- ACT-01 … ACT-08 в `.feature`; каждый зелёный или `@ignore @bug-NNN` с задачей.
- ACT-01/02: `RepeatAsync` и `NewSeedAsync` у готовой карточки → новый прогон из одной генерации в ленте; модель и промпт те же (тело запроса); сид тот же (Repeat) или следующий из генератора сидов (New seed). Ассистент при этом не вызывается (в журнале фейка нет нового chat completion).
- ACT-03: `EditAsync` → диалог правки (фейк диалогов возвращает исправленный текст) → прогон с исправленным промптом, без ассистента.
- ACT-04: у карточки с ошибкой `OtherModelAsync` → экран выбора модели (`ModelPickerPageModel`): модели этой сессии в `UsedModels` сверху; выбор модели (`PickAsync`) → прогон на ней с тем же промптом.
- ACT-05: `CopyPromptAsync` → фейк буфера обмена получил точный промпт.
- ACT-06: `DetailsAsync` у ошибки → диалог с исходным текстом ошибки из тела ответа фейка; на карточке — короткая причина (`ErrorText`), не исходный текст.
- ACT-07: `SaveAsync` у готовой → фейк `IImageSaver` получил имя `imagegen-<первые 8 символов id>.png`.
- ACT-08 — `Структура сценария`: какие действия доступны в состоянии «готово», «ошибка», «отфильтровано», «отменено» (таблица по спецификации S4.5–S4.8). Доступность — по тому, что видит пользователь в меню карточки (`ShowActionsAsync` / набор действий по коду); как именно проверять — по `Views/CardActionsTests.cs`.
- Статусы ACT-* в каталоге обновлены.

## Анализ
- `ImageGen/ImageGen.Presentation/PageModels/GenerationItemViewModel.cs` (команды `Repeat`, `NewSeed`, `Edit`, `Save`, `Details`, `OtherModel`, `CopyPrompt`, `ShowActions`), `ModelPickerPageModel.cs`, `CopyTextAction.cs`.
- `ImageGen/ImageGen.Presentation/Services/ImageFileNames.cs` (имя файла), `IImageSaver.cs`, `IClipboardService.cs`, `IDialogService.cs`.
- Потоковые тесты: `Flows/OtherModelFlowTests.cs`, `Views/CardActionsTests.cs`, `Flows/ChatFlowTests.cs` (`Repeat_AfterFinish_StartsSecondRun`).
- Спецификация: `docs/specs/chat-sessions.md` S4.1–S4.8; `docs/specs/image-client.md` F5.5, F6.4, F9.1.

## План
По одному сценарию ACT-01 → ACT-08.

Файлы: `ImageGen/ImageGen.Acceptance.Tests/Features/CardActions.feature`, `Steps/CardActionsSteps.cs` (новый), `Steps/CommonSteps.cs`, `Support/FakePlatform.cs` (только новые ответы фейков), `docs/specs/acceptance-scenarios.md`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj -- --filter-trait "Category=actions"
dotnet format ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj --verify-no-changes
bash scripts/check.sh
```
(Фильтр по тегу — если Reqnroll передаёт теги как категории xUnit; как именно — записано в `docs/knowledge/acceptance-tests.md` задачей 252.)

### Порядок исполнения (для Luna)
- Сначала прочитай только файлы из строки «Файлы», названные тесты и разделы `docs/specs/acceptance-scenarios.md`; сигнатуры сверяй с текущим `main`.
- Шаги строго по порядку (ub-mode): сначала сценарий в `.feature`, запуск — он падает, потому что шаг не привязан или ожидание не выполнено; затем минимальная привязка; сценарий зелёный. Один сценарий — один коммит (id сценария в сообщении).
- Ожидание сценария берётся из спецификации, не из кода. Если код ведёт себя иначе: не подгоняй сценарий и не чини код — тег `@ignore @bug-NNN`, новая задача `scripts/new-task.sh --publish backlog <имя>` с шагами воспроизведения, статус «расхождение (NNN)» в каталоге.
- Шаги действуют как пользователь: команды моделей страниц, выбор пунктов, навигация. Не вызывай репозитории, сервисы ядра и SQL напрямую, кроме шагов «Дано», которые готовят мир, и проверок «файл существует».
- Новый общий шаг — в `Steps/CommonSteps.cs`; сначала поищи, нет ли уже такого шага с другой формулировкой.
- Не меняй файлы вне строки «Файлы», не переименовывай существующие члены сверх плана, не чини соседние находки — они в отдельных задачах.
- Если код расходится с планом (нет такого метода, другая сигнатура), не импровизируй: остановись и запиши вопрос в «Комментарии».
- Команды `dotnet` выполняй по одной, не параллельно. После каждого изменённого проекта — `dotnet format <проект> --verify-no-changes`.
- В «Отчёте»: какие сценарии зелёные, какие `@ignore` и с какими задачами, команды и результаты, отличия от плана. Статусы сценариев в `docs/specs/acceptance-scenarios.md` обнови.

## Отчёт

## Комментарии

### 2026-11-09, claude-bdd-plan
Создано по просьбе владельца распланировать тест-сценарии приложения для исполнения GPT Luna. Сценарии и ожидания — в `docs/specs/acceptance-scenarios.md`. План ждёт одобрения.
