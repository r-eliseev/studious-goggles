---
id: 110
status: done
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

Пересечение: 123 использует тот же AssistantService и logger double; порядок 110 → 114 → 123; 117 отдельно исправляет уже существующее описание контекста. `ContextBudget` также используют `ConversationSummaryService` и XML-doc `SummaryPlanner`: его не удалять и не менять в этой задаче. Необходимость: реализовать уже принятое C1.6, иначе изображения вытесняют обязательное сообщение и запрос не помещается. Общий тестовый `CapturingLogger` используют несколько suites: расширение должно сохранить прежние свойства и конструктор без аргументов.

## План
Для Haiku/Luna: TDD pure budget helper; не менять image token price, cap 4 и current-message rule.

Прочитай: C1/C2, `AssistantService.BuildRequestAsync`, `ContextBudget`, `ContextBuilder`, `TokenCorrection`, AssistantService tests/logger double.

1. Изменить test helper на `AssistantAcceptsImages(int contextLength = 32_000)`; legacy image tests используют default. `ContextBudget`, summary service и их существующие тесты остаются без изменений.
2. Добавить отдельный pure record `AssistantRequestBudget(int ContextLength, int FixedTokens, int ImageTokens, int SummaryTokens, double Factor)` в `Core/Context`. Члены: `HistoryBudget` (int) и `CorrectedEstimate(IReadOnlyList<ContextMessage> selected)` (int). Резерв — `TokenEstimator.ReserveForOutput`, не умножать на Factor. Fixed cost = ceil(Factor × (FixedTokens + ImageTokens + SummaryTokens)); HistoryBudget = max(0, ContextLength − reserve − fixed cost). Full estimate = reserve + fixed cost + сумма ceil(Factor × Estimate(message.Text)) отдельно по каждому selected message. Так округление совпадает с `ContextBuilder.Select`.
3. Unit-тесты `HistoryBudget_ExcludesReserveAndCorrectedFixedParts`, `CorrectedEstimate_RoundsEachSelectedMessage`, `HistoryBudget_NeverNegative` задают небольшие входные числа и ожидаемые числа вручную. Интеграционный `AssertFitsContext` независимо складывает токены фактического request; не проверять helper вызовом самого себя.
4. В `BuildRequestAsync` хранить два списка: user candidate = первые 4 user images, last-run candidate = первые (4 − user count) last-run images. Для каждой попытки создать budget с текущим количеством images, заново вызвать `ContextBuilder.Select(AfterCovered(...), HistoryBudget, factor)` и проверить Full estimate. Если не помещается — удалить последнюю last-run image; после исчерпания last-run удалять последнюю user image. Остановиться на первом fitting candidate или при images=0. Старую history всегда пробовать сократить до удаления изображения; current message не сокращать.
5. Именованные сервисные тесты: `ImageBudget_AllFit_KeepsFour`, `ImageBudget_RemovesLastRunFromEnd`, `ImageBudget_ThenRemovesUserFromEnd`, `ImageBudget_FourUserImages_DoesNotWarnForCap`, `ImageBudget_CorrectionDropsOldHistoryBeforeImages`. Подобрать ContextLength из фактических costs так, чтобы каждый тест пересекал ровно нужную границу; фиксировать порядок оставшихся data URLs.
6. `ImageBudget_MandatoryRequestTooLarge_KeepsCurrentAndWarnsOnce`: fixed + summary + current + reserve не помещаются даже без images. Сохранить current полный, удалить images, не зацикливаться, один Warning даже при изначально нулевом числе images. При обычном image removal также ровно один Warning на request. ImagesRemoved считать от capped candidate, обычный cap предупреждения не даёт.
7. Warning имеет только SessionId/ProviderId/ModelId/ImagesKept/ImagesRemoved. Для проверки расширить `CapturingLogger<T>` необязательным constructor argument `minimumLevel = LogLevel.Trace`; `IsEnabled` и `Log` соблюдают порог. Сохранить `Entries`, `AllText` и positional constructor `LogEntry(Level, Message, Exception)`; structured state добавить отдельным свойством `State` типа `IReadOnlyDictionary<string, object?>`, копировать пары при Log, для state без пар — пустой dictionary. Добавить `CapturingLoggerTests`: default сохраняет Debug, Information исключает Debug, State содержит template properties, старые свойства доступны. Полные suites проверяют совместимость существующих callers.
8. `BuiltRequest.EstimatedTokens` считать из фактически выбранных texts/images: raw fixed + summary + сумма Estimate(text) + EstimateImages(count), без reserve/factor. Тест после reply с PromptTokens проверяет, что correction использует этот raw estimate. Existing 500-message test дополнить независимой проверкой full corrected estimate.
9. После проверок заполнить «Отчёт» (изменения, команды, ограничения), выполнить `scripts/release-task.sh 110` и `bash scripts/check.sh`; обновить PR по `docs/tasks.md`. Код менять маленькими шагами TDD.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Context/AssistantRequestBudget.cs` (новый), `ImageGen/ImageGen.Core/Services/AssistantService.cs`, `ImageGen/ImageGen.Core.Tests/Services/AssistantServiceTests.cs`, `ImageGen/ImageGen.Core.Tests/Context/AssistantRequestBudgetTests.cs` (новый), `ImageGen/ImageGen.Core.Tests/TestDoubles/CapturingLogger.cs`, `ImageGen/ImageGen.Core.Tests/TestDoubles/CapturingLoggerTests.cs` (новый)

## Отчёт
C1.6 реализована: при нехватке контекста картинки убираются, история сокращается в первую очередь, предупреждение ровно одно.

Изменения:
- `ImageGen/ImageGen.Core/Context/AssistantRequestBudget.cs` (новый): чистый бюджет запроса. `HistoryBudget` = context − резерв − исправленная фиксированная часть (`ceil(Factor × (fixed + картинки + сводка))`, всегда ≥ 0). `CorrectedEstimate(selected)` = резерв + исправленная фиксированная часть + сумма `ceil(Factor × Estimate(text))`, посчитанная по каждому сообщению отдельно — так округление совпадает с `ContextBuilder.Select`. Резерв `TokenEstimator.ReserveForOutput` на фактор не умножается.
- `ImageGen/ImageGen.Core/Services/AssistantService.cs`: картинки разделены на два кандидата (свои пользователя и последнего прогона), каждый ограничен общим пределом четыре (свои пользователя — первыми). Пока запрос не помещается, история пересобирается заново под текущий бюджет; если не помогло — убирается последняя картинка прогона, затем последняя картинка пользователя. Первая помещающаяся комбинация и есть ответ; при картинках = 0 текущее сообщение сохраняется целиком и пишется одно предупреждение. Предупреждение содержит только SessionId, ProviderId, ModelId, ImagesKept, ImagesRemoved — без текста пользователя и без data URL.
- `ImageGen/ImageGen.Core.Tests/TestDoubles/CapturingLogger.cs`: необязательный порог `minimumLevel` (по умолчанию Trace), `IsEnabled` и `Log` его соблюдают; у записи появилось свойство `State` (`IReadOnlyDictionary<string, object?>`) с копией пар при логировании; `{OriginalFormat}` — маркер расширений логирования, а не свойство вызывающего, поэтому в `State` не попадает. `Entries`, `AllText` и позиционный конструктор `LogEntry(Level, Message, Exception)` сохранены.
- `ImageGen/ImageGen.Core.Tests/Context/AssistantRequestBudgetTests.cs` (новый), `ImageGen/ImageGen.Core.Tests/TestDoubles/CapturingLoggerTests.cs` (новый) — шесть тестов.
- `ImageGen/ImageGen.Core.Tests/Services/AssistantServiceTests.cs`: `AssistantAcceptsImages(int contextLength = 32_000)` (было жёсткое 1000 — с ним обязательная часть не помещается и картинки выбрасывались бы), шесть именованных тестов на бюджет картинок, независимая проверка полной оценки в тесте на 500 сообщений.
- `docs/specs/assistant-context.md`: статус «в работе» (поставлен задачей 117 из-за отсутствия C1.6) → «реализовано», с пометкой, что C1.6 сделана. Файл не был указан в «Файлах» плана, но статус спецификации обязан соответствовать коду.

`BuiltRequest.EstimatedTokens` не менялся: это и раньше была «сырая» оценка (fixed + сводка + сумма Estimate(тексты) + картинки) без резерва и без фактора — именно её `TokenCorrection.Record` и использует.

Проверка тестов не пустая: с прежней логикой `AssistantService` (без C1.6) из шести новых тестов падают четыре — те, что проверяют удаление картинок и предупреждение; два оставшихся («всё помещается» и «четыре картинки пользователя не дают предупреждения») проходят и раньше, это страховки.

Границы (числа посчитаны для тестовой модели: fixed = 325, картинка = 1600, резерв = 4096):
- `ImageBudget_AllFit_KeepsFour` — контекст 32 000: четыре картинки (одна своя + три прогона) помещаются, предупреждений нет.
- `ImageBudget_RemovesLastRunFromEnd` — контекст 8000: три картинки требуют 9229 токенов, две помещаются (7629) — уходит последняя картинка прогона; в предупреждении ImagesRemoved = 1, ImagesKept = 2.
- `ImageBudget_ThenRemovesUserFromEnd` — контекст 7000: сначала уходит картинка прогона (7629 > 7000), потом последняя картинка пользователя (6029 ≤ 7000) — остаётся первая.
- `ImageBudget_FourUserImages_DoesNotWarnForCap` — четыре свои картинки занимают весь предел, картинки прогона не влезают: это сам предел, а не потеря, предупреждения нет.
- `ImageBudget_CorrectionDropsOldHistoryBeforeImages` — ответ модели с `PromptTokens = 1602` при сырой оценке 801 даёт фактор 2; при контексте 12 000 старое сообщение (1650 символов) выходит за бюджет и выбрасывается, а обе картинки остаются — история сокращается раньше картинок.
- `ImageBudget_MandatoryRequestTooLarge_KeepsCurrentAndWarnsOnce` — контекст 4300: обязательная часть не помещается даже без картинок; текущее сообщение сохраняется целиком, картинок ноль, предупреждение ровно одно (ImagesKept = 0, ImagesRemoved = 2).

Проверено:
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj        → 384 теста, 0 упавших
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj        → 70 тестов, 0 упавших
dotnet test --project ImageGen/ImageGen.Presentation.Tests/…                         → 281 тест, 0 упавших
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/…                             → 120 тестов, 0 упавших (2 пропущены)
dotnet format ImageGen/ImageGen.Core/… --verify-no-changes                          → без изменений
dotnet format ImageGen/ImageGen.Core.Tests/… --verify-no-changes                    → без изменений
bash scripts/check.sh                                                                 → все проверки пройдены
```

Ограничения: поведение переноса картинок на живом устройстве не проверялось (тесты работают на подменённых данных); `ContextBudget` не тронут — его по-прежнему использует `ConversationSummaryService`.

## Комментарии
