# 0020. Общий in-memory adapter для синхронных image transports

Статус: предложено
Допустимые статусы: предложено, принято, заменено на NNNN, отклонено
Дата: 2026-10-30

## Контекст
ADR 0005 описывает отдельный `OpenRouterJobRunner` с собственным semaphore и локальной очередью. Задачи 111/102/128/129 требуют общего пути для синхронных image transports OpenRouter и OpenAI-compatible, а semaphore провайдера должен принадлежать `GenerationService`. Одновременное владение semaphore и разные очереди создадут конфликт; queue position до фактического submit также нельзя достоверно обещать.

## Решение
Предлагается заменить ADR 0005 следующим контрактом:

1. Один `InMemoryQueuedImageProvider` адаптирует синхронные image transports OpenRouter и OpenAI-compatible к queued-контракту. Adapter не владеет semaphore и не обещает queue position.
2. Единственный per-provider semaphore принадлежит `GenerationService`. Задание ждёт slot до submit и держит его от submit до terminal state или локальной отмены. До submit database generation может иметь статус Queued; adapter handle появляется только после submit.
3. `GenerationService` владеет CTS локального job; adapter владеет CTS активного transport. Отмена связывает эти уровни через handle по контракту задачи 121.
4. Adapter хранит terminal snapshot в памяти процесса 10 минут. Проверка срока выполняется при операциях adapter; отдельного фонового timer нет. Удаление происходит при elapsed ≥ TTL. Неизвестный после перезапуска process-local handle опрашивается как Interrupted; запрос повторно не отправляется.
5. Poll не задерживает первый запрос. Для native fal интервал ограничен 1–5 секундами; для in-memory adapter — 50–250 миллисекундами. Существующие network backoff/retry fal сохраняются. Локальный режим определяется wrapper-ом connection `InMemoryQueuedImageConnection`, а не provider id, поэтому действует и для custom compatible ids.
6. Composite ProviderRouter и единый queued path GenerationService в production включаются атомарно задачей 129. Задача 128 интегрирует composite router, но не включает queued capability в production dispatch.

## Альтернативы
- Сохранить OpenRouter-specific runner из ADR 0005: он дублирует очередь и semaphore и не покрывает прочие OpenAI-compatible transports.
- Оставить обычный синхронный путь для части провайдеров: GenerationService пришлось бы ветвить по transport/provider, сохраняя разные lifecycle и cancellation semantics.
- Хранить отдельную локальную queue position: до submit неизвестно фактическое положение относительно provider semaphore и других источников работы; это обещало бы недостоверное значение.
- Удалять terminal snapshot сразу после первого poll: клиент, восстановившийся с задержкой, увидит Interrupted вместо сохранённого результата; TTL даёт ограниченное окно на завершение обработчика.

## Последствия
Один владелец ограничивает конкурентность провайдера, а общий adapter унифицирует локальные submit/poll/cancel без отдельных очередей для каждого transport. Terminal snapshots и bytes требуют ограниченной памяти и своевременной очистки; process-local jobs не переживают перезапуск и отображаются как Interrupted. Реализация распределена по задачам 102, 128, 129 и lifecycle/cancellation задачам.
