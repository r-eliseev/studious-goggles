# 0025. The ASP.NET Core Web host

Status: accepted
Allowed statuses: proposed, accepted, superseded by NNNN, rejected
Date: 2026-11-01

Task: `tasks/backlog/031-web-host-and-api.md`.

## Context

The app needs a Web head with an API and a browser UI. The host must reuse Core and Data without adding ASP.NET Core dependencies to the core. The first stage must check startup, migrations and the recovery of interrupted generations.

## Decision

Use an ASP.NET Core minimal API in a separate `ImageGen.Web` on `net10.0` with the ASP.NET Core shared framework and references to Core/Data. Integration tests go to `ImageGen.Web.Tests` with `Microsoft.AspNetCore.Mvc.Testing` 10.0.14 (MIT). The data folder is chosen through `IMAGEGEN_DATA_DIR`, `/data` by default. DB initialization and recovery run in a hosted service before requests are handled. Login is a cookie session with antiforgery; the event stream is SSE with a separate bounded subscription per client.

For the browser head use Vue 3, Vue Router, Vite and TypeScript. Test components and the API client with Vitest and Vue Test Utils, the UI with Playwright; do not add a UI framework or a global state manager. `npm run build` puts the static files into `ImageGen.Web/wwwroot`, and the Web host serves them and `index.html` for client routes. Package versions are pinned in `package-lock.json`; the checked licenses are MIT and Apache-2.0.

## Alternatives

- ASP.NET Core types in Core: breaks the layer boundary.
- One shared event channel for all clients: subscribers would take events from each other.
- New server-side business logic: duplicates the Core rules and raises the risk of differences.

## Consequences

The Web host stays a separate head. The test factory allows checking the real HTTP pipeline without starting an external server. A local run requires a non-empty `IMAGEGEN_WEB_PASSWORD`; a persistent data folder must keep the DB, the secrets and the Data Protection key ring.

The client keeps only UI state; business rules, sessions, generations and secrets stay on the server. Before every mutating API request the client gets an antiforgery token; the provider key is cleared from the field after sending and is not kept by the browser. Live updates use one SSE stream per open session, and after a reconnect or `resync` they re-read the server timeline.
