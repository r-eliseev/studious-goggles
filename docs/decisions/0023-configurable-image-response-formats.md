# 0023. Configurable image generation response formats

Status: accepted (2026-10-31)
Date: 2026-10-30

## Context

`OpenAiCompatibleClient` supported only JSON with `data[0].b64_json` and always reported the media type `image/png`. Together AI uses the same `/images/generations` endpoint, but returns a URL by default; with `response_format=base64` it returns `b64_json`. The file format can be requested separately through `output_format` (`png` or `jpeg`). Different OpenAI-compatible implementations need a shared configuration rather than a separate branch for Together.

## Decision

Add optional `ImageResponseFormat` and `ImageOutputFormat` to `ProviderConfig`. The JSON fields `imageResponseFormat` (`base64`, `b64_json`) and `imageOutputFormat` (`png`, `jpeg`) are allowed only for `openAiCompatible`; a missing or `null` field keeps the current behavior. The values are checked both by the JSON loader and by `ProviderRegistry`, so that creating a configuration directly does not bypass the whitelist.

The client sends the configured values in `response_format` and `output_format`. A successful result is still taken from `data[0].b64_json`; the expected media type is determined by `imageOutputFormat`. Without the field the old request and `image/png` stay.

URL answers are not downloaded. A Together 422 without a verified moderation marker stays `Unknown`. The HTTP status mapping is extended with shared client rules: 413 → `BadRequest`; 404 → `ModelUnavailable`; 504/524 → `Timeout`; the existing mappings of 408, 502 and moderation 403 stay.

## Alternatives

- A separate Together provider kind or a special branch in the client: rejected, because the request format is a configuration capability of the shared OpenAI-compatible API.
- Download a URL answer automatically: postponed; it is a second HTTP request with separate limits, checks and a host policy.
- Treat HTTP 422 as filtering: rejected until an unambiguous safe marker appears in the official contract or a verified live fixture.

## Consequences

Together can be connected as an ordinary `openAiCompatible` provider with a fixed JSON example and no key in the config. The whitelisted parameters limit the space of possible values. The configuration does not define sizes, additional model parameters or a reliable result price; these capabilities remain separate work.
