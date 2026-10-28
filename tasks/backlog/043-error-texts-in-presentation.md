---
id: 043
status: todo
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
- У `Generation` код уже хранится (`ErrorKind`, колонка `error_kind` в `0001_initial.sql:43`), значит для генераций достаточно правки `Presentation`.
- У `ChatMessage` кода нет, только `IsError` (`is_error`, `0001_initial.sql:16`): понадобится колонка и миграция `NNNN_<смысл>.sql` (номер — следующий свободный в момент реализации).
- «Пустой ответ ассистента» и «неверный вызов инструмента» сейчас не имеют кода: `EmptyResponse` есть в `ErrorKind`, для неверного вызова инструмента кода нет.
- Открытый вопрос для плана: сохранять ли сообщение провайдера (`ex.Message`) как диагностическую деталь (в журнал или отдельным полем) и показывать ли его пользователю. Ключи в нём не встречаются, но это стоит проверить.
- Пересечение: задача 036 (в работе, ветка `feat/queue-providers-assistant`) меняет `OpenRouterClient` и интерфейс чата ассистента (`IAssistantChatClient`). Предлагаемый порядок: 043 после 036, чтобы не конфликтовать в `AssistantService` и клиентах провайдеров. Задача 028 (ru-локализация) — после 043: тогда переводить нужно только `.resx`.

## План

## Отчёт

## Комментарии

### 2026-10-28, ревью (claude)
Пример из первой ручной проверки: при 429 у бесплатной модели пользователь видит «Assistant error (RateLimited): Provider returned error», а причина есть только в `error.metadata.raw` ответа OpenRouter («… is temporarily rate-limited upstream. Please retry shortly…»). При планировании учесть: текст по коду `RateLimited` из `AppResources` с советом выбрать другую модель (задача 049), деталь провайдера — в журнал.
