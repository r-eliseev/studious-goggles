# 0024. The chat model catalog in the provider configuration

Status: accepted (2026-10-31)
Allowed statuses: proposed, accepted, superseded by NNNN, rejected
Date: 2026-10-31

## Context
The assistant UI must show the chat models of all configured providers that can chat (the owner's decision, task 156). OpenRouter provides a catalog through its API. For OpenAI-compatible and Anthropic-compatible APIs, the `/models` loader does not describe the needed model capabilities; a client request would also need the provider's key.

## Decision
Model lists for `openAiCompatible` and `anthropicCompatible` are set in `providers.json` through `chatModels`. A model entry contains `id`, an optional `name` (defaults to `id`), an optional `supportsImageInput` (defaults to `false`) and an optional positive `contextLength`. A model declared in `chatModels` is considered to support tools; its price is unknown. OpenRouter keeps getting its catalog from the API. The built-in Together stays without `chatModels` and is not offered as an assistant.

Example:

```json
{
  "providers": [{
    "id": "my-chat",
    "kind": "openAiCompatible",
    "displayName": "My Chat API",
    "baseAddress": "https://api.example/v1/",
    "chatModels": [{
      "id": "model-id",
      "name": "Model name",
      "supportsImageInput": false,
      "contextLength": 64000
    }]
  }]
}
```

## Alternatives
- Request `/models`: rejected, because the API does not report model capabilities (tools, images, context), and the endpoint is not verified for Anthropic-compatible servers.
- Add a separate `chat` flag: redundant, since a missing `chatModels` already means the catalog is not set.

## Consequences
The catalogs of configured providers need no HTTP requests or keys. The owner of the configuration is responsible for the accuracy of the capabilities. A successful choice stores the provider id and model id pair; catalog errors of one API do not hide the models of the others.
