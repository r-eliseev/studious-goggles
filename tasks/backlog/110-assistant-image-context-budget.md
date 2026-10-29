---
id: 110
status: approved
assignee:
claimed:
created: 2026-10-29
---

# Выполнить C1.6: убирать картинки при нехватке контекста

## Описание
`assistant-context.md` помечен реализованным, но C1.6 отсутствует: при переполнении сначала убрать last-run images, затем user attachments, warning один раз. Сейчас sources объединяются и уменьшается только text history budget.

Зависит от: —
Среда: Linux или Windows.

## Критерии готовности
- Initial cap сохраняется: `userImages.Take(4)`, затем last-run до общего 4.
- Для каждого image candidate сначала пересобирается максимально возможная history; картинка удаляется только если request всё ещё не помещается из-за обязательного current message.
- Удаление по одной с конца last-run, затем с конца user; первый fitting candidate побеждает.
- Full corrected estimate = reserve + corrected fixed/summary/images + corrected selected messages; если fixed+current+reserve физически не помещаются, current остаётся, images=0, один warning.
- `BuiltRequest.EstimatedTokens` остаётся raw estimate без reserve/factor для `TokenCorrection.Record`.
- Один Warning содержит structured SessionId/ProviderId/ModelId/ImagesKept/ImagesRemoved, без user text/data URL.

## Анализ
`ContextBuilder.Select` умножает message estimates на correction factor, но `ContextBudget` не корректирует fixed/summary/images. Candidate algorithm обязан использовать одну формулу, иначе удалит image вместо старой history. Existing `AssistantAcceptsImages` задаёт ContextLength=1000 при reserve=4096; legacy image tests нужно перевести на достаточный default.

Пересечение: 123 использует тот же AssistantService и logger double; порядок 110 → 114 → 123 → 117.

## План
Для Haiku/Luna: TDD pure budget helper; не менять image token price, cap 4 и current-message rule.

Прочитай: C1/C2, `AssistantService.BuildRequestAsync`, `ContextBudget`, `ContextBuilder`, `TokenCorrection`, AssistantService tests/logger double.

1. Изменить test helper на `AssistantAcceptsImages(int contextLength = 32_000)`; legacy tests используют default.
2. Добавить pure `AssistantRequestBudget` helper: fixed reserve не умножается; `ceil(factor * (fixed+summary+images))` вычитается из context; selected message costs считаются тем же per-message rounding, что ContextBuilder. Helper возвращает available history и full corrected estimate.
3. Test helper `AssertFitsContext` использует production helper/formулу, а не упрощённую сумму texts.
4. Tests: all fit; 2 user+2 last-run и удалить ровно последнюю last-run; удалить все last-run и user с конца; 4 user means last-run outside cap and no budget warning; factor=2.0 retains images by dropping older history.
5. Impossible test: fixed+summary+current+reserve > context без images → current полный, images 0, exactly one warning.
6. В BuildRequest хранить sources отдельно. Initial candidate cap как критерий. Для candidate: budget → ContextBuilder.Select → full check; если only mandatory current causes overflow, remove next image and repeat.
7. Warning один после выбора, removed count относительно capped candidate. Не считать last-run, отброшенные штатным cap.
8. Расширить Core `CapturingLogger`: configurable minimum level и structured key/value state; regression existing logger tests. Проверить properties и отсутствие content.
9. Raw `BuiltRequest.EstimatedTokens` считать по фактически выбранным texts/images без factor/reserve.
10. Existing 500-message test дополнить full corrected estimate assertion.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Context/AssistantRequestBudget.cs` (новый), `ImageGen/ImageGen.Core/Context/ContextBudget.cs` (удалить/делегировать, только если helper заменяет его без дублирования), `ImageGen/ImageGen.Core/Services/AssistantService.cs`, `ImageGen/ImageGen.Core.Tests/Services/AssistantServiceTests.cs`, `ImageGen/ImageGen.Core.Tests/Context/AssistantRequestBudgetTests.cs` (новый), `ImageGen/ImageGen.Core.Tests/TestDoubles/CapturingLogger.cs`

## Отчёт

## Комментарии
