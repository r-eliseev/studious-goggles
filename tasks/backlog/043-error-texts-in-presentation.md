---
id: 043
status: planned
assignee:
claimed:
created: 2026-10-28
---

# Тексты ошибок формирует слой представления, а не ядро

## Описание
Правило `docs/architecture.md` (раздел «Правила слоёв»): ядро отдаёт ошибки кодами (`ErrorKind`, `GenerationStatus`), тексты интерфейса лежат в `.resx` слоя `Presentation`. Код его нарушает: ядро пишет в БД готовые английские фразы, и `Presentation` показывает их как есть.

Доказательства (аудит 2026-10-28, `docs/log/2026-10-28-audit.md`):
- `ImageGen.Core/Services/AssistantService.cs:25-26` — константы `EmptyResponseText`, `InvalidToolCallText`; `:73` — `"Assistant error ({ex.Kind}): {ex.Message}"`. Это текст сообщения ассистента с `IsError = true`.
- `ImageGen.Core/Services/GenerationService.cs:228,235` — `generation.ErrorMessage = ex.Message`; сами тексты — английские строки `ImageGen.Core/OpenRouter/OpenRouterClient.cs` («Could not reach OpenRouter.», «OpenRouter returned no image.» и т. п.) или сообщение ответа провайдера.
- `ImageGen.Presentation/PageModels/MessageItemViewModel.cs:15` показывает `message.Text`; `ImageGen.Presentation/PageModels/GenerationItemViewModel.cs:105` показывает `generation.ErrorMessage`. Для `GenerationStatus` перевод в `AppResources` уже есть (`:117-122`), для `ErrorKind` — нет.

Нужно, чтобы пользователь видел текст ошибки из `.resx` по коду ошибки, а ядро тексты интерфейса не формировало. Это рефакторинг: поведение для пользователя (английский интерфейс) остаётся тем же по смыслу.

Источник: аудит 2026-10-28, решение владельца — отдельная задача на рефакторинг (не в составе 028).
Среда: Linux или Windows (Core, Data, Presentation и их тесты; разметка MAUI и Avalonia, если меняется привязка, — по ADR 0006 в обеих оболочках).

## Критерии готовности
- В `ImageGen.Core` нет строк, предназначенных для показа пользователю, в путях ошибок ассистента и генераций; ошибки передаются кодом `ErrorKind`.
- Сообщение ассистента об ошибке хранит код ошибки; текст для экрана берётся в `Presentation` из `AppResources` по коду.
- Карточка генерации с ошибкой показывает текст из `AppResources` по `Generation.ErrorKind`; у каждого значения `ErrorKind` есть строка в `AppResources.resx`.
- Уже сохранённые в БД сообщения об ошибках (созданные до задачи) показываются без сбоев.
- Тесты `Core`, `Data`, `Presentation`, `Avalonia` зелёные; есть тесты на соответствие «код → строка» для всех значений `ErrorKind`.

## Анализ
- Нарушение подтверждено аудитом (`docs/log/2026-10-28-audit.md`, и проверено по коду сейчас): ядро формирует текст ошибок для пользователя в `AssistantService` (`EmptyResponseText`, `InvalidToolCallText`, `"Assistant error ({ex.Kind}): {ex.Message}"`) и в `GenerationService` (`generation.ErrorMessage = ex.Message`), где текст приходит из `OpenRouterClient` на английском.
- В `Presentation` `MessageItemViewModel` показывает `message.Text`, `GenerationItemViewModel` показывает `generation.ErrorMessage`. Для `GenerationStatus` строки уже есть в `AppResources`, для `ErrorKind` — нет.
- Затрагивается схема: сообщение чата хранит только текст, поэтому код ошибки сообщения нужно сохранить новой миграцией. Номер миграции — следующий свободный в `ImageGen/ImageGen.Data/Migrations/` в момент реализации (сейчас последний `0007_conversation_summaries.sql`).
- Пересечения: `AssistantService` меняла 077, 083; `GenerationItemViewModel` — 080. Все они уже `done`, конфликтов с задачами в работе нет.
- Старые сообщения без кода ошибки должны продолжить показываться: показываем сохранённый текст.

## План

Команды проверки: `dotnet test` для `ImageGen.Core.Tests`, `ImageGen.Data.Tests`, `ImageGen.Presentation.Tests`, `ImageGen.Avalonia.Tests`; `dotnet build … -f net10.0-android -p:AndroidSdkDirectory=$ANDROID_HOME`; `dotnet format` по изменённым проектам; `scripts/check.sh`.

Концепции:
1. Core: `ErrorKind` уже есть; добавить значения для всех ошибок ассистента (пустой ответ, некорректный вызов инструмента, вложение не читается, отказ провайдера по классу `ProviderException.Kind`). `AssistantService` не формирует текст: сохраняет `ChatMessage.ErrorKind` (новое поле). `GenerationService` сохраняет `ErrorKind`; `ErrorMessage` остаётся технической подробностью (ответ провайдера, для окна «Details»).
2. Data: миграция `NNNN_message_error_kind.sql` — колонка `error_kind` в `chat_messages` (NULL для старых сообщений). Репозиторий читает и пишет колонку.
3. Presentation: `ErrorTextOf(ErrorKind?)` → строка из `AppResources` (`ErrorKind_<значение>`); для `null` — сохранённый текст сообщения. `MessageItemViewModel` и `GenerationItemViewModel` берут текст через эту функцию.
4. Тест-кейсы: `ErrorKind` каждого значения имеет строку в `AppResources.resx` (тест перебирает enum); сообщение без кода показывает сохранённый текст; сообщение с кодом показывает строку из ресурсов; старая миграция не ломает чтение.
5. Русский/английский: строки только в `.resx`; текст технической ошибки провайдера не переводится, показывается в окне «Details».

Файлы: `ImageGen/ImageGen.Core/Services/AssistantService.cs`, `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `ImageGen/ImageGen.Core/Domain/Chat/ChatMessage.cs`, `ImageGen/ImageGen.Core/Domain/Generations/ErrorKind.cs`, `ImageGen/ImageGen.Data/Migrations/NNNN_message_error_kind.sql` (новый), `ImageGen/ImageGen.Data/Repositories/ChatMessageRepository.cs`, `ImageGen/ImageGen.Presentation/PageModels/{MessageItemViewModel,GenerationItemViewModel}.cs`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.{resx,cs}`, тесты, `docs/architecture.md` (раздел про ошибки, если описан)

## Отчёт

## Комментарии

### 2026-10-28, ревью (claude)
Пример из первой ручной проверки: при 429 у бесплатной модели пользователь видит «Assistant error (RateLimited): Provider returned error», а причина есть только в `error.metadata.raw` ответа OpenRouter («… is temporarily rate-limited upstream. Please retry shortly…»). При планировании учесть: текст по коду `RateLimited` из `AppResources` с советом выбрать другую модель (задача 049), деталь провайдера — в журнал.
