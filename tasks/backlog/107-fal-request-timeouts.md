---
id: 107
status: planned
assignee:
claimed:
created: 2026-10-29
---

# Применить timeout провайдера ко всем запросам fal

## Описание
Найдено ревью 2026-10-29. Общий `HttpClient.Timeout` бесконечный, потому что timeout должен задавать клиент провайдера. `ProviderClientBase.SendJsonAsync` это делает, но `FalImageProvider.SendFalJsonAsync`, `DownloadAsync` и `CancelAsync` вызывают `HttpClient.SendAsync` напрямую. При `CancellationToken.None` из `GenerationService` status/result/download могут зависнуть навсегда и оставить generation незавершённой.

Зависит от: —
Среда: Linux или Windows.

## Критерии готовности
- `Config.Timeout` ограничивает submit, status, result, image download и cancel у fal.
- Истечение внутреннего timeout становится `ProviderException(ErrorKind.Timeout)`; внешняя отмена остаётся `OperationCanceledException` и не маскируется как timeout.
- Нет второго независимого значения timeout в `FalImageProvider`.
- Тесты используют зависающий `HttpMessageHandler` и завершаются быстро, без реального ожидания 180 секунд.

## Анализ
- `CoreServiceCollectionExtensions` задаёт `Timeout.InfiniteTimeSpan` всем клиентам.
- `ProviderClientBase.CreateTimeout` сейчас private, поэтому подкласс не может переиспользовать правильную семантику.
- Submit использует `SendJsonAsync` и уже ограничен. Остальные прямые вызовы находятся в `ImageGen/ImageGen.Core/Fal/FalImageProvider.cs`.
- Пересекается с 106 и 109 по `FalImageProvider`, с 108 по передаче cancellation token. Рекомендуемый порядок: после 106, до 108 и 109.

## План
Для исполнителя (план рассчитан на Haiku/Luna): TDD, один сценарий за шаг. Не добавляй timeout в `HttpClient` и не дублируй `CancelAfter`. Если доступный .NET API отличается, не угадывай: запиши ошибку компиляции в «Комментарии», поставь `blocked`.

Прочитай: `ProviderClientBase.cs`, `FalImageProvider.cs`, `FalImageProviderTests.cs`, `CoreServiceCollectionExtensions.cs`.

1. Разрешить подклассам безопасно создавать linked timeout token через существующий механизм `ProviderClientBase`; сохранить различение внутреннего timeout и внешней отмены.
2. Добавить в `FalImageProviderTests` helper-конфигурацию с коротким timeout и handler, который ждёт отмены токена.
3. Падающие тесты: status timeout, result timeout, download timeout и cancel timeout → `ProviderException` с `ErrorKind.Timeout`.
4. Падающий тест: заранее отменённый внешний токен → `OperationCanceledException`, а не `ProviderException(Timeout)`.
5. Применить общий timeout ко всем прямым HTTP-вызовам fal. Не менять timeout submit, который уже работает через базовый класс.
6. Убедиться, что логирование тел и подробностей ошибок на уровне Debug осталось без изменений.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Providers/ProviderClientBase.cs`, `ImageGen/ImageGen.Core/Fal/FalImageProvider.cs`, `ImageGen/ImageGen.Core.Tests/Fal/FalImageProviderTests.cs`

## Отчёт

## Комментарии
