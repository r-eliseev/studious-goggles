---
id: 057
status: done
assignee:
claimed:
created: 2026-10-28
---

# Промпт прогона показывается в чате дважды

## Описание
Дефект (ручная проверка 016, владелец, 2026-10-28, скриншот): после вызова `generate_images` в ленте сначала идёт сообщение ассистента `Prompt: A large blue square moon …`, а сразу под ним — строка прогона с тем же `Prompt: A large blue square moon …` курсивом. Промпт должен быть виден один раз — в блоке прогона (S2.3 `docs/specs/chat-sessions.md`: «промпт … всегда виден в чате в блоке прогона и на каждой карточке»).

Зависит от: —.
Среда: Linux или Windows (Core и Presentation; разметка не меняется или меняется в обеих оболочках — проверка сборкой Android, CI и Avalonia headless).

## Критерии готовности
- После вызова инструмента в ленте промпт виден один раз — в блоке прогона.
- Если ассистент вместе с вызовом написал свой текст, этот текст виден сообщением ассистента (без строки `Prompt:`).
- Если своего текста нет, отдельного сообщения ассистента над прогоном нет (или оно не повторяет промпт — решение при реализации, записать в «Отчёт»).
- Уже сохранённые сообщения вида `Prompt: <промпт прогона>` (созданы до задачи) тоже не дублируют промпт на экране.
- Ассистент в следующих ходах по-прежнему знает текущий промпт (он уходит отдельным системным сообщением `Current prompt: …`, это не меняется).

## Анализ
- Откуда дубль: `AssistantPrompts.RunMessage(assistantText, prompt)` (задача 010) пишет в текст сообщения ассистента строку `Prompt: <промпт>` — без своего текста это весь текст сообщения. `RunItemViewModel.PromptText` (задача 014) показывает `RunPromptFormat` = `Prompt: {0}` в блоке прогона. Сообщение и прогон связаны `ChatMessage.RunId`.
- История для модели ассистента: сообщения уходят модели как есть, но текущий промпт и так передаётся системным сообщением (`AssistantPrompts.CurrentPrompt`), поэтому строка `Prompt:` в тексте сообщения модели не нужна.
- Старые данные: в БД уже есть сообщения с текстом `Prompt: …` и `RunId`. Миграция БД не обязательна: достаточно, чтобы экран не показывал строку, совпадающую с промптом связанного прогона.

## План
1. Core: `AssistantPrompts.RunMessage` → возвращает только текст ассистента (обрезанный), без строки `Prompt:`; пустой текст допустим. `AssistantService` сохраняет сообщение ассистента со ссылкой на прогон как сейчас (текст может быть пустым). Тесты `AssistantServiceTests`, которые ждали `Prompt: …` в тексте, переписать по новому поведению.
2. Presentation: при построении ленты (`ChatPageModel.ReloadAsync` и `TimelineOrdering` не меняются) сообщение ассистента с `RunId`:
   - текст = текст сообщения без завершающей строки `Prompt: <промпт этого прогона>` (для старых записей);
   - если после этого текст пустой — строки сообщения в ленте нет, блок прогона остаётся.
   Логика — чистая функция в Presentation (например `RunMessageText.Visible(message, run)`), тестируемая отдельно.
3. Спецификация: `docs/specs/chat-sessions.md` — в S2.3 уточнить «промпт показывается один раз — в блоке прогона; текст ассистента, если есть, — отдельным сообщением».
4. Тест-кейсы:
   - вызов инструмента без текста → сообщение ассистента сохранено с пустым текстом и `RunId`; в ленте его строки нет, у прогона `PromptText` = `Prompt: <промпт>`;
   - вызов с текстом «Here you go» → в ленте сообщение «Here you go», под ним прогон с промптом;
   - старое сообщение `Prompt: <промпт>` с `RunId` → не показывается; старое `Here you go\n\nPrompt: <промпт>` → показывается «Here you go»;
   - сообщение ассистента без `RunId` (обычный ответ) → показывается как есть, даже если начинается с «Prompt:».
5. Пункт в 016 и 046: «после генерации промпт виден один раз».

### Команды проверки
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format <каждый изменённый проект> --verify-no-changes
scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Services/AssistantPrompts.cs`, `ImageGen/ImageGen.Core/Services/AssistantService.cs`, `ImageGen/ImageGen.Core.Tests/Services/AssistantServiceTests.cs`, `ImageGen/ImageGen.Presentation/PageModels/ChatPageModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/RunMessageText.cs` (новый), `ImageGen/ImageGen.Presentation.Tests/PageModels/*`, `docs/specs/chat-sessions.md`, `tasks/v0.1/016-skeleton-manual-check.md`, `tasks/backlog/046-desktop-manual-check.md`

## Отчёт

Сделано:
- Core: `AssistantPrompts.RunMessage(assistantText)` возвращает только текст ассистента (пустой допустим); строка `Prompt:` больше не пишется. `AssistantService` склеивает текст и примечания без пустых частей.
- Presentation: `PageModels/RunMessageText.cs` — `Visible(text, runPrompt)` убирает старую строку `Prompt: <промпт прогона>` (в конце или целиком). `ChatPageModel` берёт промпт связанного прогона из таймлайна; если текст пустой, строки сообщения нет. Для обычных сообщений без `RunId` ничего не меняется. Конструктор `MessageItemViewModel(message, text)`.
- Спецификация `docs/specs/chat-sessions.md`, S2.3: промпт один раз, в блоке прогона.

Отличие от плана: пункт про проверку в 016 и 046 не добавлен в этом коммите (ручная проверка — позже вместе с волной). Решение по критерию «отдельного сообщения ассистента нет, если текста нет»: сообщения нет вовсе (сохранённое с пустым текстом не показывается).

Проверка: Core.Tests 262/262, Presentation.Tests 159/159 (было 152 до 057; новые — RunMessageTextTests и 2 теста ChatPageModel), Avalonia.Tests — без падений (59 пройдено), `dotnet format --verify-no-changes` для Core, Core.Tests, Presentation, Presentation.Tests — без изменений.

## Комментарии

### 2026-10-28, владелец (в чате, записал claude)
План одобрен.
