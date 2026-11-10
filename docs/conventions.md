# Conventions

References: `docs/knowledge/csharp.md`, `docs/knowledge/maui.md`. This file holds only the rules agents break most often.

## Language
- Everything in the repository is in English: documentation, tasks, code, identifiers, comments, test names and wait descriptions, log messages, Gherkin scenarios, script messages, commit messages (owner's decision of 2026-11-09).
- `scripts/check-docs.sh` (check "Latin only") fails when a checked path contains letters of a non-Latin script (Cyrillic and others). Symbols such as `×`, `→`, `—`, `≈` are allowed. The checked paths are listed in the script; task 268 extends them until the whole repository is covered.
- Exceptions are only test data that verify non-English input (for example, Russian text for search tests or localized resources). Such files are listed explicitly in the script's exception list with a reason; do not add Cyrillic anywhere else.
- Quoted UI texts are the English texts from `AppResources`.

## Style and naming
- .NET 10, C# with `Nullable` and `ImplicitUsings` enabled (as in the sample). Do not suppress nullable warnings; fix them.
- One type per file, the file name equals the type name (the sample breaks this: `TaskRespository.cs`; do not repeat it).
- Namespaces follow folders. `GlobalUsings.cs` is allowed for our own namespaces, as in the sample.
- Asynchronous methods end with `Async` and take a `CancellationToken` (mandatory in the core; in a PageModel where cancellation exists).
- Formatting is checked by `dotnet format ImageGen/<Project>/<Project>.csproj --verify-no-changes` for each changed project (do not format the whole solution: on Linux the MAUI project does not load; `AGENTS.md`, "Commands"). `.editorconfig` at the root is the main source of formatting rules.

## Layers (details in `docs/architecture.md`)
- The core does not reference MAUI, ASP.NET Core or UI types. Platform specifics come through core interfaces.
- Models and DTOs do not contain `Brush`, `Color`, `ImageSource` or other UI types: conversion happens in a PageModel or a converter.
- Providers are hidden behind one interface; code outside `Providers/` does not know which provider is behind it.

## MAUI and MVVM
- MVVM on `CommunityToolkit.Mvvm`: `[ObservableProperty]` on partial properties, `[RelayCommand]` on methods (the sample's style, C# 14).
- A page gets its PageModel through the constructor and assigns `BindingContext`. Registration in `MauiProgram`: main pages `AddSingleton`, detail pages `AddTransientWithShellRoute<Page, PageModel>("route")`.
- Only compiled bindings: every page and template has `x:DataType`. `MauiXamlInflator=SourceGen` is enabled.
- Navigation: Shell with routes; parameters through `IQueryAttributable` and `ShellNavigationQueryParameters`, return with `..` or `..?refresh=true`.
- Accessibility is mandatory: `SemanticProperties.Description` on interactive elements, `SemanticScreenReader.Announce` for important changes, a touch target of at least 44 (as in the sample's styles). Status is never conveyed by color alone (a specification requirement).
- Adaptivity: `OnIdiom` and `OnPlatform` in XAML, not branches in code. One code base for phone and desktop.
- Background async calls from synchronous places: `FireAndForgetSafeAsync()`; the error is logged through `TaskExtensions.Logger`, not a bare `async void`.
- Errors in dialogs are shown only through `ErrorAlert`: error codes are localized in Presentation; never show `Exception.Message` to the user.
- Never block the UI thread: no `.Result`, no `.Wait()`.

## Secrets and data
- Provider keys and the Web password are stored only in encrypted storage; never in `Preferences`, a JSON config, the repository, logs or exports. Do not print keys in logs or error messages (show the last 4 characters).
- Logging: `ILogger<T>` with message templates (not interpolation), identifiers as arguments. Operational Information/Warning/Error entries contain only safe structured metadata, without request/response content, provider text or exception details; error details and redacted bodies are available separately at Debug. At Information HTTP bodies are not read: the size comes from `Content-Length` or is reported as unknown. Debug/trace read bodies with a bounded reader and do not write bodies that partly exceed the limit. Do not log HTTP headers (the key is there). The body of the key check response (`GET .../key`) and requests with `SkipResponseBody` are never read for logging at any level. `BodyRedactor` replaces the values of the fields `label`, `key`, `api_key`, `*_user_id` with `<redacted>`. File and levels: ADR 0010 and 0022.
- SQL only with parameters (`@name`), as in the sample. Values (user input, data from the DB and from providers) are never put into SQL text, neither by concatenation nor by interpolation. Only code constants may be inserted: column and table names from `const`, the schema version number (`PRAGMA user_version = {int}`) (owner's decision of 2026-10-29).
- DB schema: changes to an already released schema only through a migration; `DROP TABLE` as in the sample's `SeedDataService` is forbidden in the product.
- A generation result is saved immediately when it is received, before it is shown to the user.

## Dependencies
- Package versions are pinned explicitly; the single list is `Directory.Packages.props`.
- A new package only after checking its license and a decision in `docs/decisions/`. `Syncfusion.Maui.Toolkit` (MIT) is not used yet; when charts, shimmer or pull-to-refresh are needed, it is the first choice unless there is a better one (`docs/status.md`); adopting it requires an ADR.

## Tests
- The core must be covered by tests: translating parameters into provider requests, mapping statuses to the common model, price calculation, retries and cancellation. Provider calls in tests go only through a substituted `HttpMessageHandler`; real keys in tests are forbidden.
- Test names: `Method_Condition_Result`. Run: `dotnet test --project <path to the test csproj>` (see `docs/knowledge/csharp.md`, section "Tests").
- Libraries: xUnit v3 and NSubstitute (ADR 0004). Exception: UI tests of the built app `ImageGen.E2E.*` use NUnit with Allure (ADR 0029, `docs/specs/ui-e2e.md`). Test projects: `ImageGen.Core.Tests`, `ImageGen.Data.Tests`, `ImageGen.Presentation.Tests`, `ImageGen.Avalonia.Tests`, `ImageGen.Acceptance.Tests` (after task 252).
- Acceptance scenarios (ADR 0031): Gherkin on Reqnroll, only in `ImageGen.Acceptance.Tests` and, after task 266, in `ImageGen.E2E.*`. Scenarios are in English, have a permanent id and requirement tags from `docs/specs/acceptance-scenarios.md`; the catalog is the source of truth for what is covered. User flows are tested as scenarios; new flow tests in `ImageGen.Avalonia.Tests/Flows` are not added (they are being migrated, task 267).
- Page models are written in `ImageGen.Presentation` without UI framework types (`IQueryAttributable`, `MainThread`, `ImageSource`, `Shell`, Avalonia): such types stay in the page or in the head's implementation of an interface (`IMainThread`, `INavigationService`, `IDialogService`). Tests are in `ImageGen.Presentation.Tests` (`docs/architecture.md`).
- Tests do not depend on the machine's culture and OS: numbers and prices in the UI are formatted with `CultureInfo.InvariantCulture` until localization (task 028); where culture matters, the test sets it itself (`CultureInfo.CurrentCulture = new CultureInfo("pl-PL")` in `try`/`finally`). Tests that delete a temporary folder with a database first wait for background work (`GenerationService.WhenIdleAsync`): on Windows an open file cannot be deleted. Test shells also pump UI callbacks after generations and wait for `IImageFileStore.WhenIdleAsync`: `ChatPageModel` starts reading dimensions after the result is saved, and Windows does not allow deleting the folder while the PNG is open. CI runs the tests on `windows-latest` too (job `windows-tests`).

## Rules for the v0.1 skeleton tasks
Mandatory for every task in `tasks/v0.1/`. If the task text differs from this section, the task wins; if the task is silent, this section applies. If neither fits, do not invent: write a comment in the task and set `blocked`.
- Namespace = `ImageGen.<Project>.<Folder>` (for example `ImageGen.Core.Domain`), file-scoped declaration: `namespace X;`.
- One public type per file, file name = type name. Classes are `sealed` unless inheritance is needed.
- Domain classes are `sealed class` with `{ get; set; }` properties; mandatory properties have the `required` modifier.
- Asynchronous methods: the `Async` suffix, the last parameter is `CancellationToken cancellationToken`. In core interfaces it has no default value.
- Time only through `TimeProvider` (`timeProvider.GetUtcNow()`), not `DateTime.Now` and not `DateTimeOffset.UtcNow`.
- Argument checks: `ArgumentNullException.ThrowIfNull`, `ArgumentException.ThrowIfNullOrWhiteSpace`, `ArgumentOutOfRangeException.ThrowIfLessThan` and the like.
- Logging: `ILogger<T>`, structured messages. Do not write keys or prompt text to the log.
- Packages only from `ImageGen/Directory.Packages.props`; do not change versions or add new ones.
- Signatures, file names and texts given in the task are copied verbatim.
- In tests pass `TestContext.Current.CancellationToken` to every method with a `CancellationToken` (otherwise the xUnit v3 analyzer warns, and warnings are build errors).
- Tests: a `<Type>Tests` class in the test project, folder path as for the type (`ImageGen.Core.Tests/Services/ApiKeyServiceTests.cs`); substitutes through NSubstitute; HTTP through `StubHttpMessageHandler` from task 006.
- Before finishing: the commands from the task's "Verification" section, then `scripts/check.sh`.

## Prohibitions
- Do not bypass provider moderation and do not change the prompt or model in secret (F9.4).
- Do not commit `bin/`, `obj/`, `.vs/`, keys or secret files.
