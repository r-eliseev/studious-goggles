# Debug trace

Task: 085. Purpose: from a log record, write a unit test that reproduces the problem on a provider mock.

## Enabling

- Off by default. Outside the mode not a single record is created; ordinary logging (045) does not change.
- The `IMAGEGEN_DEBUG_TRACE` variable: `1`, `true`, `on`, `yes` — enable; any other value — disable. It takes precedence over the file.
- Or the `debugTrace: true` field in `logging.json` in the data folder (the same file as for the log level, see `docs/knowledge/dev-environment.md`, section "Logs").
- The log level (`IMAGEGEN_LOG_LEVEL`) does not affect the trace.

## Where it is

`{data folder}/debug/<yyyy-MM-dd>/trace.jsonl` (the UTC date of the record). Image files are next to it.

- One line of the file is one record (JSON, UTF-8).
- An image is a separate file `<blob name>.<extension>` in the same folder, the extension by type (`png`, `jpg`, `webp`, `gif`, otherwise `bin`).

## Format: `debug-trace/1`

Top-level fields of every line:

| Field | Value |
|---|---|
| `schema` | always `debug-trace/1` |
| `time` | record time, ISO 8601 (`O`) |
| `kind` | the kind of record (see below) |
| `fields` | a "key → text" object; the set of keys depends on `kind` |
| `blobs` | an array of `{name, file, mediaType}`; `file` is the file name in the day folder; for `b64_json` `mediaType` is `application/octet-stream` (the type is not taken from the response) |

Record kinds (`kind`):

- `user-input` — input to the assistant. Fields: `session`, `text` (the message text), `attachments` (attachment ids separated by commas; the files themselves are already in the data folder).
- `generation-request` — starting a generation. Fields: `session`, `prompt` (normalized), `models` (`provider/model` separated by commas), `seeds`, `aspectRatio`, `resolution`, `outputFormat`, `quality`, `attachments`.
- `http-request` — a request to a provider. Fields: `id` (links it to the response), `method`, `path` (without the query string), `headers`, `body`.
- `http-response` — a provider response. Fields: `id`, `method`, `path`, `status`, `elapsedMs`, `headers`, `body`.

A key is missing from `fields` if there is no value (for example, `body` of a request without a body).

An example line of a response with a picture (line breaks added for reading):

    {"schema":"debug-trace/1","time":"2026-10-29T09:30:00.0000000+00:00","kind":"http-response",
     "fields":{"id":"a1b2c3d4","method":"POST","path":"/api/v1/chat/completions","status":"200",
               "elapsedMs":"812","headers":"Content-Type: application/json","body":"{\"url\":\"blob:a1b2c3d4-response-0\"}"},
     "blobs":[{"name":"a1b2c3d4-response-0","file":"a1b2c3d4-response-0.png","mediaType":"image/png"}]}

In `body` the picture is replaced by a `blob:<name>` link; its bytes are in the file from `blobs[].file`.

## What is not written

- Headers except the allowlist: `Content-Type`, `Accept`, `User-Agent`, `Content-Length`. `Authorization`, `x-api-key` and any other headers are dropped, not masked.
- For the key check (`GET .../key`) and requests with `SkipResponseBody` the `http-response` record is not created at all (including the status); the key check response body contains the account name.
- Provider keys — in no form (`AGENTS.md`).
- In a body, the values of account fields (`label`, `key`, `api_key`, `*_user_id`) are replaced with `<redacted>`, as in the 045 logs (`BodyRedactor.RedactAccountFields`).
- The response body is written whole within `ProviderResponseLimits` (ADR 0022). If the limit is exceeded, the request ends with an error; a partial record is not saved. `BodyRedactor.MaxLength` applies only to lines of the ordinary 045 log.
- Images do not stay in the text: image data URLs and `b64_json` fields are moved to files. A data URL that could not be decoded stays in the text as is (it is not a key).

## Reproducing in a test

A mock is assembled from an `http-response` record: `body` with `blob:<name>` replaced by a data URL from the file, `status` from the `status` field. Examples: `ImageGen.Core.Tests/Diagnostics/DebugTraceProviderReplayTests.cs` (generation through `OpenRouterClient`, the mock from a record) and `DebugTraceHandlerTests.cs` (restoring the response at the HTTP level). A `generation-request` record gives the models and seeds for a repeated run.

## Implementation

- `ImageGen.Core/Diagnostics`: `IDebugTrace`, `TraceRecord`, `TraceBlob`, `NullDebugTrace` (the default), `TraceRedaction` (the header allowlist, moving pictures out).
- `ImageGen.Core/Http/LoggingHttpHandler` writes `http-request` and `http-response`; `AssistantService` writes `user-input`; `GenerationService` writes `generation-request`.
- `ImageGen.Presentation/Logging`: `DebugTraceSettings` (enabling, `Create`), `FileDebugTrace` (writing to files).
- Heads: `AppServices.Create(debugTrace:)` in Avalonia, `MauiProgram` registers the trace before `AddImageGenCore`; `AddImageGenCore` sets `NullDebugTrace` if no trace is registered.

## Open questions

- The size of the `debug/` folder: there is no cleanup (unlike logs, which keep 14 days). Whether rotation is needed is unknown; for now the trace is enabled by hand and deleted by the user.
- The user's attachment images in `user-input` and `generation-request` are recorded as id references, without copies of the bytes.
