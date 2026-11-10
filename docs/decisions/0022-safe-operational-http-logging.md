# 0022. Safe and bounded HTTP logging

Status: accepted (2026-10-31)
Date: 2026-10-30

## Context
The HTTP logging handler sits before the provider response parsers. Without a limit it pre-buffers the whole request and response regardless of Content-Length. Operational entries may also keep the provider's exception text, although bodies and headers contain user data and secrets. ADR 0010 defines the Serilog sink and the levels, but does not separate operational metadata from exception details.

## Decision
At Information/Warning/Error write only safe structured operational metadata: method, path without query, status, duration and the known size or "unknown". Do not attach the exception object/message and do not read the body at Information. Keep exception details and redacted bodies for Debug. Debug/trace use a shared request/response limit and do not write a partial body when it is exceeded. Key checks and requests with the `SkipResponseBody` flag do not read the response body for logging. Headers stay out of the logs.

## Alternatives
- Read all bodies for an exact size and later re-reading: rejected, because this lets untrusted responses exhaust memory.
- Cut and write the beginning of an oversized body: rejected, because the partial text may contain secrets and complicates replaying the trace.

## Consequences
Ordinary operational diagnostics keep method/path/status/duration and the declared size. Bodies and exceptions require Debug. Debug/trace are limited by the shared provider response limits and a separate request body limit. This rule applies here to the provider HTTP transport and does not claim an audit of all existing app warnings.
