# 0006. Avalonia as the third head, a shared presentation layer

Status: accepted
Allowed statuses: proposed, accepted, superseded by NNNN, rejected
Date: 2026-10-28

Extends ADR 0001 (core and heads). Revises the owner's decision of 2026-10-27 "Linux: postponed" (`docs/status.md`).

## Context
MAUI does not support Linux, and its Windows target builds only on Windows (`docs/knowledge/dev-environment.md`). So all screen tasks currently need a Windows machine, while the cloud sessions and the VPS where agents work cannot build the UI. The owner (2026-10-28) decided to add a third head on Avalonia and answered the questions:
- Avalonia platforms are desktop: Linux, Windows, macOS;
- MAUI stays on all its platforms (Windows, macOS, Android, iOS), so Windows and macOS are served by both heads;
- screen code is shared through a common project, not duplicated;
- keys go to the system stores; without them the key lives only in memory;
- the work goes right after task 035, before the rest of the queue.

Facts (checked 2026-10-28 on nuget.org): Avalonia 12.1.4 (updated 2026-10-09) builds for `net10.0`; `Avalonia.Headless.XUnit` 12.1.4 depends on `xunit.v3.extensibility.core` (>= 3.2.2), that is, it targets xUnit v3 like the project's tests. Compatibility with the xUnit v3 version pinned in the project was not checked.

## Decision
1. **A shared presentation layer `ImageGen.Presentation`** (`net10.0`, without MAUI and Avalonia): page and row models (PageModels, ItemViewModels), the interfaces `INavigationService`, `IDialogService`, `IMainThread`, routes (`Routes`), one-time initialization (`AppInitializer`), UI strings (`.resx` and the accessor class). Dependencies: `Presentation → Data → Core`, `Presentation → Core`; the `CommunityToolkit.Mvvm` package. Tests are `ImageGen.Presentation.Tests`; linking MAUI files into test projects (`MauiLinked/`) is removed.
2. **Heads keep only their own things:** page markup, converters, implementations of navigation, dialogs, the UI thread and platform services (`IAppPaths`, `ISecretStore`). `ImageGen.Maui` and `ImageGen.Avalonia` reference `Presentation`.
3. **`ImageGen.Avalonia`** is the desktop (Linux, Windows, macOS), Avalonia 12.x, the Fluent theme, compiled bindings by default, DI through `Microsoft.Extensions.DependencyInjection`. Navigation is our own router over the same `Routes` strings as Shell in MAUI (a page stack in the main window). UI tests are `ImageGen.Avalonia.Tests` on `Avalonia.Headless.XUnit`, without a screen, on Linux and in CI.
4. **Keys in Avalonia:** Windows — DPAPI (a file in the data folder encrypted for the current user), macOS — Keychain, Linux — Secret Service (gnome-keyring, KWallet) through D-Bus. If there is no store (for example, Linux without Secret Service), the key is kept only in process memory, and the user sees that the key will have to be entered again after closing. The key is never written to disk in plain text. The choice of libraries is a separate ADR after proof-of-concept code.
5. **Every screen feature is done in both heads:** the logic once in `Presentation`, the markup in MAUI and in Avalonia.

## Alternatives
- **Avalonia instead of MAUI on the desktop** (MAUI only for phones): less duplicated markup, but the owner kept MAUI on all platforms.
- **Avalonia everywhere, without MAUI:** one UI stack, but mobile Avalonia needs a separate maturity check.
- **Own page models in each head:** more freedom, but the screen logic and tests are duplicated.
- **MAUI code on Linux through the Avalonia MAUI Backend (`Avalonia.Controls.Maui`, full hosting with `UseAvaloniaApp`)** — added 2026-10-28 after acceptance, does not change the decision: one markup instead of two and the browser through WebAssembly, but as of 2026-10-28 only a prerelease (`11.0.0-rc.3`), it needs .NET 11 and MAUI 11, and the backend has known limits for our APIs (`MainThread`, `SecureStorage`, accessibility, Shell). Analysis and conditions for revisiting: `docs/knowledge/avalonia-maui-backend.md`.
- **A key in a file encrypted with a master password:** works without Secret Service, but requires entering the password on every start and a cryptography decision; the owner chose memory as the fallback.

## Consequences
Easier: the UI builds and is tested on Linux and in a cloud session; the screen logic lives in one place and is tested by ordinary unit tests; a Linux desktop appears. Harder: every screen feature is marked up twice; Windows and macOS have two heads with the same functionality; there is nowhere to check the macOS build and Keychain while the project has no macOS machine (unknown whether it will). Task order: `docs/status.md`, "Order of work".
