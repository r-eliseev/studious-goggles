# Reference: C# and .NET for this project

Written from the model's knowledge, not by reconciling with live documentation in this session. If a fact matters for a decision, check it against learn.microsoft.com and fix the file. Versions: .NET 10 (`10.0.402` is installed on the machine), as in the sample project.

Sources to check against: https://learn.microsoft.com/dotnet/csharp/ , https://learn.microsoft.com/dotnet/core/ , https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/

## Language versions and features (what occurs in the project code)
- Nullable reference types (`<Nullable>enable</Nullable>`): `string?` for an allowed null, `!` only when null is excluded and this is proven, `[NotNullWhen(false)]` and similar attributes describe the flow (as in the sample's `IsNullOrNew`).
- Collection expressions: `List<Tag> Tags { get; set; } = [];` (C# 12).
- Primary constructors for classes and structs (C# 12), `required` members (C# 11), `record` and `record struct` for immutable data, `init` properties.
- Partial properties (C# 13): the declaration `public partial string Name { get; set; }` + the implementation by a generator. This is exactly how `[ObservableProperty]` works in `CommunityToolkit.Mvvm` 8.4 in the sample (instead of `_name` fields).
- The `field` keyword in properties and extension members (C# 14, .NET 10): allowed, but their support by tools and analyzers depends on the SDK version; use them only if the build and `dotnet format` pass.
- Pattern matching: `is`, `switch` expressions, list patterns. Prefer a `switch` expression to an `if/else` table when mapping statuses.
- `global using` and `ImplicitUsings` shorten file headers; our own global usings go into `GlobalUsings.cs`.

## Asynchrony
- `async/await` along the whole chain; return `Task`/`ValueTask`, do not block (`.Result`, `.Wait()` are forbidden, especially on the UI thread).
- Pass the `CancellationToken` down to `HttpClient`; do not turn cancellation into an error (catch `OperationCanceledException` separately).
- A parallel run of several models: a list of `Task`s, `await Task.WhenAll(...)` with independent error handling of each (an error of one must not lose the results of the others: either a `try/catch` inside each task or checking `Task.Status` after `WhenAll`). Limiting concurrent requests per provider: `SemaphoreSlim` or `Parallel.ForEachAsync` with `MaxDegreeOfParallelism`.
- For a stream of status updates from tasks to the UI: `IProgress<T>` or `System.Threading.Channels`; in MAUI update bound properties from the right thread (`MainThread.InvokeOnMainThreadAsync`), CommunityToolkit.Mvvm does not switch threads by itself.
- `async void` only for event handlers. A background call without waiting: the sample's `FireAndForgetSafeAsync` wrapper.
- `ConfigureAwait(false)` in libraries (Core), not in UI code.
- `SemaphoreSlim(1,1)` as an asynchronous lock: `await _lock.WaitAsync(); try { ... } finally { _lock.Release(); }` (as in `SeedDataService` and `ModalErrorHandler`).

## HTTP and providers
- Do not create an `HttpClient` per request. Use `IHttpClientFactory` (`Microsoft.Extensions.Http`): a named or typed client per provider, the base address and authorization headers are set at registration or on the request. Take the key from the secret store at request time, not from static fields.
- Authorization per the specification table: fal — `Authorization: Key <key>`, OpenRouter and OpenAI-compatible providers — `Authorization: Bearer <key>`.
- Resilience: retries only for temporary errors (network, 429, 5xx), with a limited number and a delay with jitter; the `Microsoft.Extensions.Http.Resilience` library (based on Polly). Do not retry requests that create paid generations without idempotency; a retry must fit into the spending limit (F9.3).
- Downloading results: as a stream (`ResponseHeadersRead`, `CopyToAsync` to a file), without loading it into memory whole.
- Test through a substituted `HttpMessageHandler`; there are no real keys in tests.
- Do not log headers and bodies with keys.

## JSON
- `System.Text.Json`. For provider types — `record` models and `[JsonPropertyName]`; for speed and trimming — source generation: `[JsonSerializable]` on a `JsonSerializerContext` (in the sample `JsonContext`).
- Optional fields are nullable properties; unknown fields are ignored by default. For dynamic model parameter schemas (fal, an OpenAI-compatible provider) — `JsonElement`/`JsonNode`, not `dynamic`.
- Validate parsing of user data and throw a clear error (the model is `GetRequiredString` in `SeedDataService`).

## Configuration, secrets, settings
- `Microsoft.Extensions.Configuration` and the Options pattern (`IOptions<T>`) for settings. MAUI does not plug in its own built-in configuration by default; the core gets settings through an interface.
- Development secrets: .NET User Secrets (`dotnet user-secrets`) — only for development, not for storing users' keys.
- The Web head on ASP.NET Core: `Microsoft.AspNetCore.DataProtection` for encrypting the secrets file; the encryption keys must survive a container restart (a Docker volume), otherwise the secrets become unreadable. The login password comes from an environment variable (do not keep it in `appsettings`).
- MAUI: `SecureStorage` (Keychain on iOS and Mac, Android Keystore, Windows DPAPI) — see `docs/knowledge/maui.md`.

## Data storage
- The sample uses `Microsoft.Data.Sqlite` with hand-written ADO.NET. Alternatives: Dapper (a thin layer over ADO.NET) or EF Core with SQLite (migrations, LINQ). The choice is not made, an ADR is needed; criteria: schema migrations, testability, working in all heads.
- SQLite on MAUI requires a native provider (`SQLitePCLRaw.bundle_e_sqlite3`, as in the sample). For Web/Docker the same package is enough.
- Store result files separately from the DB (the DB holds only the path and metadata), the file name by the result id.

## DI
- `Microsoft.Extensions.DependencyInjection`: `Singleton` (shared state, the model catalog cache), `Scoped` (a request in ASP.NET Core), `Transient` (pages with parameters). Do not keep a `Scoped` in a `Singleton`.
- Dependencies through the constructor, on interfaces. Do not use a service locator (`IServiceProvider` in business code).

## Logging
- `ILogger<T>` from `Microsoft.Extensions.Logging`; structured messages (`_logger.LogError(e, "... {Id}", id)`), not interpolation. Do not log keys, passwords or prompt content.

## Tests
- Chosen (ADR 0004): xUnit v3 (`xunit.v3`) and NSubstitute for substitutes. The core is on `net10.0`, the tests do not need MAUI workloads. PageModel tests are possible if navigation and messages are moved behind interfaces.
- xUnit v3 on the .NET 10 SDK works only in the new `dotnet test` mode (Microsoft.Testing.Platform): at the repository root a `global.json` with `{"test": {"runner": "Microsoft.Testing.Platform"}}`, a test project with `<OutputType>Exe</OutputType>`. Otherwise the error "Testing with VSTest target is no longer supported". Run: `dotnet test --project <path to csproj>` (checked 2026-10-27).
- In the Microsoft.Testing.Platform mode `dotnet test --solution <slnx>` builds all projects of the solution, including MAUI; for the core run `--project`.
- HTTP in tests is substituted by our own subclass of `HttpMessageHandler` (NSubstitute does not substitute the protected `SendAsync`).
- On Windows an open SQLite file cannot be deleted (`IOException: … being used by another process`), on Linux it can: a test that deletes the temporary folder while a background generation still writes to the database fails only on Windows. `SqliteConnection.ClearAllPools()` does not help if the connection is busy; you have to wait for the work (`GenerationService.WhenIdleAsync`) (checked 2026-10-28, task 053).
- `CultureInfo.CurrentCulture` on the owner's machine is `pl-PL` (a comma in decimals); CI has an English culture. Formatting "by the current culture" passes in CI and fails locally (checked 2026-10-28).

## dotnet commands
```
dotnet --info                       SDK version and installed workloads
dotnet build <sln>                  build
dotnet test <sln>                   tests
dotnet format <sln> --verify-no-changes   formatting check
dotnet add package <name> --version <v>   a package with an explicit version
dotnet list package --vulnerable    vulnerability check
dotnet user-secrets set <key> <value>     development secrets
```

## Common mistakes
- A forgotten `await` (warning CS4014) → the task is lost together with its exception.
- An `async` lambda in `List.ForEach` → `async void`; use `foreach` with `await` or `Task.WhenAll`.
- A shared `Random`, `HttpClient` or `List` from several threads without synchronization.
- Comparing money and prices in `double`: use `decimal` for money (provider prices, spending sums).
- Date strings without a time zone: store in UTC (`DateTimeOffset`).

## Logging and names (checked 2026-10-28)
- Serilog (`Serilog.Sinks.File`) in the `{Message:lj}` template takes values in JSON form: strings and enums come out in quotes (`is "Succeeded"`). For a readable string use `{Message:l}`.
- `TaskExtensions` in .NET 10 conflicts with `System.Threading.Tasks.TaskExtensions` with `using ImageGen.Presentation.Utilities;` in tests. Reference it through an alias (`using AppTaskExtensions = ImageGen.Presentation.Utilities.TaskExtensions;`).
- In HTTP operational logging at Information do not read the body for its size: use `Content-Length` or record an unknown size. At Debug/trace read only through a bounded reader, discarding an excess before logging a partial body; see `ProviderResponseLimits` and ADR 0022. After a bounded read, replace the content with a buffer with the saved headers if the consumer needs to read the same bytes. Do not include the exception and the provider's text in operational Warning/Error: write safe metadata separately from the exception details at Debug.
