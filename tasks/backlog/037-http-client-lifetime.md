---
id: 037
status: todo
assignee:
claimed:
created: 2026-10-27
---

# HttpClient OpenRouter в singleton-сервисах

## Описание
`AddImageGenCore()` регистрирует `OpenRouterClient` типизированным клиентом (`AddHttpClient<OpenRouterClient>`, время жизни transient), а его забирают singleton-сервисы (`ApiKeyService`, `ModelCatalogService`, `GenerationService`, `AssistantService`). Каждый из них навсегда держит один `HttpClient` с одним обработчиком, поэтому ротация обработчиков `IHttpClientFactory` до них не доходит: при долгой работе приложения смена DNS у `openrouter.ai` не подхватится. Нужно, чтобы соединения обновлялись, не меняя времени жизни сервисов.

Источник: ревью задачи 011 (замечание в её «Отчёте»).
Среда: Linux или Windows (нужен только .NET 10 SDK).

## Критерии готовности
- Соединения с OpenRouter периодически пересоздаются, хотя сервисы остаются singleton.
- Тест композиции (`ImageGen.Data.Tests/CompositionTests.cs`) проходит; решение описано в «Отчёте».

## Анализ
- Рекомендованный Microsoft вариант для типизированного клиента в singleton: основной обработчик `SocketsHttpHandler` с `PooledConnectionLifetime` (например, 2 минуты) и бесконечным `HandlerLifetime`, чтобы фабрика не держала устаревшие обработчики. Альтернатива — `OpenRouterClient` берёт `IHttpClientFactory` и создаёт клиента на запрос. Выбор делается при планировании.

## План

## Отчёт

## Комментарии
