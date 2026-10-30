---
id: 150
status: in-progress
assignee: codex-gpt6
claimed: 2026-10-30T16:52:12Z
created: 2026-10-30
---

# Учитывать whitespace при ограничении base64 изображений

## Описание
DecodeImage в OpenRouter и OpenAI-compatible сравнивает raw base64.Length с max encoded length. Convert.FromBase64String допускает ASCII whitespace, поэтому валидный payload с переносами отвергается даже при decoded content меньше50MiB и JSON меньше72MiB. План112 шаг5 требовал проверку по длине без whitespace.

Зависит от: — (112 возвращена из done аудитом; это её остаток).
Среда: Linux или Windows.

## Критерии готовности
- Допустимые base64 whitespace не учитываются в pre-allocation size guard; decoded50MiB принимается, 50MiB+1 отклоняется Unknown.
- Лимиты generated JSON72MiB и binary50MiB не увеличены; invalid base64 остаётся Unknown.
- Оба клиента используют одинаковую проверку; нет дополнительной копии всей строки ради удаления whitespace.
- Ошибка/логи не содержат base64 или ключей.

## Анализ
Evidence: OpenRouter/OpenRouterClient.cs DecodeImage:186+, OpenAiCompatible/OpenAiCompatibleClient.cs:100+. Сама Convert.FromBase64String уже игнорирует пробельные символы; ошибка в предварительном guard. Верхний JSON reader остаётся bounded, не надо менять HTTP pipeline. Тест нужен по фактическому результату клиента; маленький helper с configurable limit позволит проверить математику без больших аллокаций, но хотя бы один end-to-end boundary test оставляет production constants.

## План
1. Прочитать оба DecodeImage и ProviderResponseLimits, тесты response size112. Зафиксировать какие whitespace принимает Convert.FromBase64String: ASCII space/tab/CR/LF; не использовать char.IsWhiteSpace, который включает другие Unicode символы.
2. Создать failing tests для обоих clients: валидный encoded content, допустимые whitespace добавляют raw length сверх guard, decoded length не превосходит Image. JSON сериализовать настоящим serializer, а не вручную вставлять CR/LF в JSON string.
3. Вынести общий internal bounded base64 decoder в Providers/BoundedBase64ImageDecoder.cs; production передаёт ProviderResponseLimits.Image, unit tests могут передать маленький byte limit. Один проход без новой строки считает значащие символы; guard сравнивает их с ((Image+2)/3)*4. Convert.FromBase64String делает фактическую проверку символов/padding, после decode остаётся проверка decoded length<=Image.
4. Unit tests малого limit: whitespace во всех разрешённых позициях, exact boundary, limit+1, padding0/1/2, пустые/invalid символы и Unicode NBSP не становятся допустимыми. FormatException не менять в другой тип на уровне direct client: как сейчас, адаптер queued переводит её в Unknown. Проверить этот mapping через InMemoryQueuedImageProvider; direct client regression на invalid base64 ожидает FormatException. Отсутствующий/пустой b64_json сохраняет существующий EmptyResponse.
5. Проверить production limit с50MiB и50MiB+1 без whitespace, и wrapped вариант:49MiB нулевых байтов, CRLF каждые76 символов, сериализация JSON обычным serializer, Assert JSON<72MiB. Нулевые байты исключают случайное увеличение JSON из-за escaping символа +. Большие tests обоих clients и имеющиеся size tests объединить в непараллельную xUnit collection; последовательных dotnet команд недостаточно для запрета параллельных test classes. Не хранить постоянные static buffers.
6. Дописать evidence в112; вернуть её в done только после150+152 (там недостающая provider matrix), не после одной150. Не менять143/127/debug logging в этом фиксе.

Файлы: `ImageGen/ImageGen.Core/OpenRouter/OpenRouterClient.cs`, `ImageGen/ImageGen.Core/OpenAiCompatible/OpenAiCompatibleClient.cs`, `ImageGen/ImageGen.Core/Providers/BoundedBase64ImageDecoder.cs` (новый), соответствующие Core.Tests, `tasks/backlog/112-provider-response-size-limits.md`.

Проверка: Core.Tests; sequential форматирование Core и Core.Tests; harness.

### Команды проверки

Выполнять по одной, из корня репозитория:

```bash
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
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
