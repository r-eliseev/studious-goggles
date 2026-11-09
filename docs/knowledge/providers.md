# Providers: configuration and verified facts

## Together AI: configuration and the image contract

Reconciled with the official documentation on 2026-10-30. Together AI is built into the app as an image-only OpenAI-compatible provider with the base URL `https://api.together.ai/v1`; a live response and actual billing were not checked. The API uses a Bearer key; the key is stored in the app's encrypted store and is not given in the config.

## Configuration

A user `providers.json` is not needed to enable Together. The built-in catalog contains the four models below; a user entry with `id: together` replaces the built-in one entirely by the common config override rules.

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

`imageResponseFormat` allows `base64` and `b64_json`; `imageOutputFormat` — `png` and `jpeg`. The fields are optional: if they are missing or `null`, the client keeps the old behavior of an OpenAI-compatible provider. Empty strings, a wrong JSON type and values outside the list are forbidden. These fields are available only for `openAiCompatible`.

For images the client sends `POST images/generations`, `model`, `prompt`, `n=1`, an optional `seed` and the configured format parameters. A `response_format=base64` response contains `data[0].b64_json`; the client decodes it locally. `imageOutputFormat` sets the media type of the result (`image/png` or `image/jpeg`). Without the parameters a response with a URL is not supported: the client does not download a second URL.

The key check calls `GET models` with Bearer authorization; the body of the key check response is not read or logged. Secrets must not be put into this example or a user config.

## Available models and price guidelines

The catalog and the price change; the values below are the data of the official catalog on the check date. Prices marked `+` depend on the request parameters and are not the actual cost of a specific result.

| Model | API id | Catalog price | Note |
|---|---|---:|---|
| FLUX.1 Kontext Pro | `black-forest-labs/FLUX.1-kontext-pro` | $0.04/MP | price per megapixel |
| FLUX.1.1 Pro | `black-forest-labs/FLUX.1.1-pro` | $0.04/MP | price per megapixel |
| FLUX.2 Dev | `black-forest-labs/FLUX.2-dev` | $0.0154+/image | the price depends on parameters; starts at the given amount |
| FLUX.2 Flex | `black-forest-labs/FLUX.2-flex` | $0.03+/image | the price depends on parameters; starts at the given amount |
| FLUX.2 Pro | `black-forest-labs/FLUX.2-pro` | $0.03+/image | the price depends on parameters; starts at the given amount |
| FLUX.2 Max | `black-forest-labs/FLUX.2-max` | $0.07/MP at 50 steps | depends on resolution and steps |

Built-in models: FLUX.1.1 Pro, FLUX.2 Dev, FLUX.2 Flex and FLUX.2 Pro. Seed is enabled for each; the initial connection is limited to text-to-image and one picture per request. The Together catalog requires a positive balance. All rates above are a snapshot of the official catalog on the check date, not a guaranteed current price.

The documented image generation response format has no actual cost. The client leaves `CostUsd = null`; a catalog rate with `+` is a minimum/variable price, not the cost of the result. The UI does not show it as the actual price.

## Limits of the initial integration

- Text-to-image only: dimensions, `aspect_ratio`, `steps`, `negative_prompt` and reference images are not passed now; they need a separate capability task.
- `seed` is supported by the contract, but Together describes it as best-effort: repeatability across replicas/model versions is not guaranteed.
- Together describes the safety checker as an HTTP 422 response, but does not define a reliable unique moderation marker in the error body. For now 422 is classified as `Unknown`, not as `Moderated`/`Filtered`.
- A live request with a key and actual billing were not checked; the contract is covered by HTTP stubs.

## Sources

- [OpenAI compatibility: base URL, models and compatibility](https://docs.together.ai/docs/inference/openai-compatibility)
- [Image generation parameters: response/output formats, seed and the safety checker](https://docs.together.ai/docs/inference/images/parameters)
- [Serverless model catalog: models and prices](https://docs.together.ai/docs/serverless/models)
- [Text-to-image API](https://docs.together.ai/docs/inference/images/text-to-image)
- [Error codes](https://docs.together.ai/docs/error-codes)
