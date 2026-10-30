# Together AI: конфигурация и контракт изображений

Проверено по официальной документации 2026-10-30. API использует Bearer key и OpenAI-совместимый base URL `https://api.together.ai/v1`; ключ хранится в зашифрованном хранилище приложения и не указывается в JSON.

## Конфигурация

```json
{
  "providers": [
    {
      "id": "together",
      "kind": "openAiCompatible",
      "displayName": "Together AI",
      "baseAddress": "https://api.together.ai/v1/",
      "timeoutSeconds": 180,
      "imageResponseFormat": "base64",
      "imageOutputFormat": "png",
      "imageModels": [
        {
          "id": "black-forest-labs/FLUX.2-dev",
          "name": "FLUX.2 Dev",
          "supportsSeed": true
        }
      ]
    }
  ]
}
```

`imageResponseFormat` допускает `base64` и `b64_json`; `imageOutputFormat` — `png` и `jpeg`. Поля опциональны: если их нет или они `null`, клиент сохраняет прежнее поведение OpenAI-compatible провайдера. Пустые строки, неправильный JSON-тип и значения вне списка запрещены. Эти поля доступны только для `openAiCompatible`.

Для изображений клиент отправляет `POST images/generations`, `model`, `prompt`, `n=1`, опциональный `seed` и заданные параметры формата. Ответ `response_format=base64` содержит `data[0].b64_json`; клиент декодирует его локально. `imageOutputFormat` задаёт media type результата (`image/png` или `image/jpeg`). Без параметров ответ с URL не поддерживается: клиент не скачивает второй URL.

Проверка ключа вызывает `GET models` с Bearer authorization; тело ответа проверки ключа не читается и не журналируется. Секреты нельзя помещать в этот пример или пользовательский конфиг.

## Доступные модели и ориентиры цен

Каталог и цена меняются; значения ниже — данные официального каталога на дату проверки. Цены, помеченные `+`, зависят от параметров запроса и не являются фактической стоимостью конкретного результата.

| Модель | API id | Цена из каталога | Примечание |
|---|---|---:|---|
| FLUX.1 Kontext Pro | `black-forest-labs/FLUX.1-kontext-pro` | $0.04/MP | цена за мегапиксель |
| FLUX.1.1 Pro | `black-forest-labs/FLUX.1.1-pro` | $0.04/MP | цена за мегапиксель |
| FLUX.2 Dev | `black-forest-labs/FLUX.2-dev` | от $0.0154/изображение | зависит от параметров; выбрана для примера |
| FLUX.2 Flex | `black-forest-labs/FLUX.2-flex` | от $0.03/изображение | зависит от параметров |
| FLUX.2 Max | `black-forest-labs/FLUX.2-max` | $0.07/MP при 50 шагах | зависит от разрешения и шагов |

В документированном формате ответа image generation фактическая стоимость отсутствует. Клиент оставляет `CostUsd = null`; цена каталога не выдаётся за цену результата.

## Ограничения начальной интеграции

- Только текст-в-изображение: dimensions, `aspect_ratio`, `steps`, `negative_prompt` и reference images сейчас не передаются; они требуют отдельной capability задачи.
- `seed` поддерживается контрактом, но Together описывает его как best-effort: повторяемость между репликами/версиями модели не гарантируется.
- Together описывает safety checker как ответ HTTP 422, но не задаёт надёжный уникальный маркер moderation в error body. Пока 422 классифицируется как `Unknown`, не как `Moderated`/`Filtered`.
- Живой запрос с ключом и фактическое списание не проверялись; контракт покрыт HTTP заглушками.

## Источники

- [OpenAI compatibility: base URL, models и совместимость](https://docs.together.ai/docs/inference/openai-compatibility)
- [Image generation parameters: response/output formats, seed и safety checker](https://docs.together.ai/docs/inference/images/parameters)
- [Serverless model catalog: модели и цены](https://docs.together.ai/docs/serverless/models)
- [Error codes](https://docs.together.ai/docs/error-codes)
