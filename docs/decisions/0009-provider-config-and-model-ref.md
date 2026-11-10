# 0009. Provider config in a file and the "provider:model" model reference

Status: accepted
Allowed statuses: proposed, accepted, superseded by NNNN, rejected
Date: 2026-10-28

Task: `tasks/backlog/036-provider-config-compatible-api.md`. The owner's answers are in its "Comments" (2026-10-28).

## Context
The OpenRouter address was a constant in `OpenRouterClient`, one provider for the whole container, `ImageModel` and `Generation` had no provider, and `SessionSettings` stored models as strings without a provider. Connecting an OpenAI- or Anthropic-compatible API without changing code was impossible. The owner: provider config in a file (JSON without keys), an OpenAI-compatible kind for images and chat, Anthropic-compatible only for chat, https addresses only (except localhost), the model reference `provider:model`.

## Decision
1. **Config.** `providers.json` without keys. A built-in file (`Providers/builtin-providers.json`, an assembly resource) and an optional user file in `IAppPaths.DataDirectory`. A user entry with the same `id` replaces the built-in one, new ones are added. Unknown fields are rejected by name; fields that look like a key or password are rejected with a hint not to keep them in the config.
2. **Check at startup.** `ProviderRegistry` checks the id (lowercase letters, digits, `-`, uniqueness), the address (absolute https, http only for localhost), a timeout greater than zero, `imageModels` only for `openAiCompatible`. An error is a `ProviderConfigException` with the name of the provider and the field. `AppInitializer` takes the registry in its constructor, so an invalid config stops the startup.
3. **Provider kinds.** `ProviderKind`: `openRouter`, `openAiCompatible` (images and chat), `anthropicCompatible` (chat only). Fal and Replicate are added as their own kinds in tasks 025 and 026.
4. **Model reference.** `ModelRef(ProviderId, ModelId)`, text `provider:model`. Text without a prefix is an OpenRouter model (old sessions). It is a prefix only if a provider id (`[a-z0-9][a-z0-9-]*`) stands before the first colon: in free OpenRouter models `…:free` the colon comes after a slash, so it is not a prefix. JSON converter `ModelRefJsonConverter`; `settings_json` keeps the old property names (`imageModelIds`, `assistantModelId`).
5. **Clients.** Singleton services do not hold an `HttpClient`: each request takes a client from `IHttpClientFactory`. The timeout is set per request from the config (`CancellationTokenSource`); named clients have the `HttpClient` timeout disabled. This closes task 037.
6. **Assistant chat.** `IAssistantChatClient` and neutral `AssistantChat*` types instead of `IOpenRouterChatClient`. The `chat/completions` format is shared by OpenRouter and OpenAI-compatible providers (`OpenAiChatWire`). Anthropic: the Messages API, system messages in the `system` field, pictures as base64 blocks. The API version in the `anthropic-version` header is `2023-06-01` (not verified against the real API).
7. **Keys.** A provider key lives in `ISecretStore` under the name `SecretNames.ForProvider(id)`. For `openrouter` the name is the old one (`openrouter_api_key`), saved keys work without a migration.
8. **Catalog and storage.** `image_models` gets the key `(provider_id, id)`, the migration `0002_providers.sql` copies old rows as `openrouter`. `generations.provider_id` is a column with `openrouter` by default. The catalog of one provider does not touch the others.
9. **The model list of an OpenAI-compatible provider** comes from `imageModels` in the config: the `/models` API does not describe image capabilities (seed, number of pictures).

## Alternatives
- **Config in code** (a `ProviderConfig` list in `CoreServiceCollectionExtensions`). Simpler, but a new API needs a build. Rejected by the owner.
- **The reference as an object** `{"provider":…,"model":…}` in JSON. Parses more reliably, but breaks compatibility with already saved strings and takes more space. Rejected.
- **A `/` or `|` separator.** Conflicts with model ids that contain a slash. Rejected.
- **A model list from `/models` of an OpenAI-compatible API.** Gives no image capabilities. Rejected.
- **An Anthropic-compatible kind for images.** The Messages API does not return pictures (checked against the documentation, 2026-10-28). Rejected by the owner.

## Consequences
Easier: a new OpenAI- or Anthropic-compatible API is added by an entry in a file; old sessions, the catalog and the OpenRouter key work unchanged; 037 is closed. Harder: two places define providers (the built-in and the user file, merge rules); a prefixed model reference is a parsing rule to remember (`ModelRef.Parse`); the OpenAI image format (`images/generations`, base64 only) and the Anthropic headers are checked only against stubs — real answers are checked by hand by the owner (`docs/status.md`).
