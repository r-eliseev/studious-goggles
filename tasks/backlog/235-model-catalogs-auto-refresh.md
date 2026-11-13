---
id: 235
status: review
assignee:
claimed:
created: 2026-11-07
---

# Model catalogs: automatic refresh at startup

## Description
The owner's question of 2026-11-07: "are the model lists taken from the OpenRouter API at startup?" — they are not. The image (`ModelCatalogService`), video (`VideoModelCatalogService`), audio (`AudioModelCatalogService`) and embedding (`EmbeddingModelCatalogService`) catalogs work from the database cache: they load from the API only when the cache is empty or on Refresh in the session models screen, and never refresh by themselves (the owner's log: "Image models of provider openrouter: 63 from cache"). The chat model catalog (`ChatModelCatalogService`) keeps its cache in memory and in practice refreshes once per launch. A model removed by the provider stays in the list until a manual refresh.

Depends on: 221 and 227 (done).
Environment: Linux or Windows.

## Acceptance criteria
- A new Core service `ModelCatalogRefreshService` with one method `RefreshStaleAsync(CancellationToken)`:
  - image catalog: for every id in `IProviderRouter.ImageProviderIds` whose key is configured (`ApiKeyService.HasKeyAsync`); providers without a key are skipped without any log above Information;
  - video, audio and embedding catalogs: provider `ProviderIds.OpenRouter`, only when its key is configured;
  - a catalog (per provider) is stale when its cached list (`I*ModelRepository.ListAsync`) is empty or the oldest `FetchedAt` is at least 24 hours before `TimeProvider.GetUtcNow()`; a stale one is reloaded through its catalog service with `forceRefresh: true`; a fresh one is not touched (no provider call);
  - the 24-hour threshold is a named constant.
- Failures never break the start and give exactly one Warning per failed catalog and provider: when the cache is not empty the catalog service already returns the cache and logs its own Warning (no second Warning from the refresh service); when the cache is empty the catalog service throws `ProviderException` — the refresh service catches it, logs one Warning naming the catalog and the provider, and continues with the next catalog. Any other exception except cancellation is handled the same way. Cancellation ends the refresh silently.
- Startup, in the background, without delaying the first screen:
  - Avalonia and MAUI: `AppInitializer` (`ImageGen/ImageGen.Presentation/Services/AppInitializer.cs`) starts `RefreshStaleAsync` once, after its initialization finished, without awaiting it (`FireAndForgetSafeAsync`, `ImageGen/ImageGen.Presentation/Utilities/TaskExtensions.cs`); `EnsureInitializedAsync` still completes without waiting for the refresh;
  - Web: `ImageGen/ImageGen.Web/StartupInitializer.cs` starts it after `RecoverInterruptedAsync` without awaiting; `StopAsync` cancels it (a `CancellationTokenSource` owned by the initializer) and awaits it.
- No change-notification event: an open models screen keeps its list; the next opening reads the refreshed cache (amended 2026-11-13, see "Comments").
- The chat model catalog is unchanged.
- Tests (Core): fresh cache → no provider call; stale cache → the provider is called and the cache is replaced; empty cache → loaded; a provider error with a non-empty cache → cache kept, exactly one Warning in total; a provider error with an empty cache → exactly one Warning, the next catalog is still refreshed; a provider without a key → skipped, no Warning; all four catalogs are visited. Presentation: `EnsureInitializedAsync` completes while the refresh is still running (a refresh that blocks on a gate).

## Analysis
- Catalog services: `ImageGen/ImageGen.Core/Services/ModelCatalogService.cs` (`GetImageModelsAsync(providerId, forceRefresh, ct)`), `VideoModelCatalogService.cs` (`GetVideoModelsAsync(forceRefresh, ct)`), `AudioModelCatalogService.cs` (`GetAudioModelsAsync(forceRefresh, ct)`), `EmbeddingModelCatalogService.cs` (`GetModelsAsync(forceRefresh, ct)`). Each catches `ProviderException` only when the cache is not empty, logs a Warning and returns the cache; with an empty cache the exception propagates. `ModelCatalogService.GetAllImageModelsAsync` is not used by the refresh (it warns for a missing key too).
- Repositories: `ImageGen/ImageGen.Core/Storage/IImageModelRepository.cs`, `IVideoModelRepository.cs`, `IAudioModelRepository.cs`, `IEmbeddingModelRepository.cs` (`ListAsync(providerId, ct)`); every model type has `FetchedAt`.
- Keys: `ImageGen/ImageGen.Core/Services/ApiKeyService.cs` (`HasKeyAsync`).
- Registration: `ImageGen/ImageGen.Core/CoreServiceCollectionExtensions.cs` (next to the catalog services). `AppInitializer` is registered in `ImageGen/ImageGen.Avalonia/Composition/AppServices.cs` and `ImageGen/ImageGen.Maui/MauiProgram.cs` as a singleton resolved by the container: a new constructor parameter needs no change there.
- Existing tests to follow: `ImageGen/ImageGen.Core.Tests/Services/ModelCatalogServiceTests.cs`, `VideoModelCatalogServiceTests.cs`; `ImageGen/ImageGen.Presentation.Tests/Services/AppInitializerTests.cs`; `ImageGen/ImageGen.Web.Tests/StartupTests.cs`.

## Plan
1. Core tests for `ModelCatalogRefreshService` (the cases of the criteria), one at a time; each fails first because the service does not exist, then because the behavior is missing. Use a fake `TimeProvider` (`Microsoft.Extensions.Time.Testing.FakeTimeProvider` if the test project already references it, otherwise the fake used by the existing tests).
2. The service and its registration.
3. `AppInitializerTests`: a new test that the refresh is started after initialization and `EnsureInitializedAsync` does not wait for it; then the change in `AppInitializer`.
4. Web: `StartupTests` — the app starts while the refresh is blocked, and stopping the host cancels it; then the change in `StartupInitializer`.

Files: `ImageGen/ImageGen.Core/Services/ModelCatalogRefreshService.cs` (new), `ImageGen/ImageGen.Core/CoreServiceCollectionExtensions.cs`, `ImageGen/ImageGen.Core.Tests/Services/ModelCatalogRefreshServiceTests.cs` (new), `ImageGen/ImageGen.Presentation/Services/AppInitializer.cs`, `ImageGen/ImageGen.Presentation.Tests/Services/AppInitializerTests.cs`, `ImageGen/ImageGen.Web/StartupInitializer.cs`, `ImageGen/ImageGen.Web.Tests/StartupTests.cs`.

### Verification
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet test --project ImageGen/ImageGen.Web.Tests/ImageGen.Web.Tests.csproj
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Web/ImageGen.Web.csproj --verify-no-changes
bash scripts/check.sh
```

### Execution order
- Read only the files of the "Files" line and the named tests; check signatures against the current `main`.
- Steps strictly in order (ub-mode): a failing test, run it and confirm the stated reason, the minimal fix, green.
- Do not change files outside "Files". If the code differs from the plan, stop and write a question in "Comments".
- If an acceptance or Avalonia test becomes flaky because the background refresh now calls the fake provider, do not change those tests: record it in "Comments" and set `blocked`.
- Run `dotnet` commands one at a time. In the "Report": what changed, commands and results, differences from the plan.

## Report

- Added `ModelCatalogRefreshService`, registered it in Core DI, and refreshes stale image, video, audio and embedding catalogs only for providers with configured keys. Empty and 24-hour-old catalogs are refreshed through existing catalog services; refresh failures are logged per catalog/provider and do not stop later catalogs.
- Avalonia/MAUI startup launches catalog refresh after initialization using `FireAndForgetSafeAsync`; Web startup launches it after recovery and cancels/awaits it at host stop.
- Added Core refresh tests and an initializer test proving startup completion does not wait for the background refresh.
- Verification passed: Core tests (851), Presentation tests (546), Avalonia tests (187 passed, 2 platform skips), Web tests (21), Acceptance tests (50 passed, 1 skip); Core/Presentation/Web `dotnet format --verify-no-changes`; `bash scripts/check.sh`.
- No plan deviations. Avalonia platform skips: Windows DPAPI and unavailable Linux Secret Service D-Bus.
- CI follow-up: the `core` job also formats the Core.Tests project; formatted the new test file and verified it with `dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes --include ImageGen/ImageGen.Core.Tests/Services/ModelCatalogRefreshServiceTests.cs`.

## Comments

### 2026-11-07, claude-audit
Создано по вопросу владельца о загрузке списков моделей при старте (проверено по коду: кэш без автообновления). План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
Предложение одобрено («Да»); план одобрен ранее данным поручением «заапрувь все таски которые будут».

### 2026-11-11, codex-gpt6
Status reconciliation: the existing task branch records a plan mismatch (catalog services absorb provider failures and log independently; Web startup uses `StartupInitializer`), but the base task card still says `approved`. Set this task to `blocked` and clear the stale claim. The plan needs an amendment before implementation can proceed.

### 2026-11-13, claude-planner — plan amendment
Answers the mismatch of 2026-11-10 (recorded on `origin/task/235-model-catalogs-auto-refresh`) and the reconciliation of 2026-11-11. Verified in the code: the catalog services log their own Warning only when they fall back to a non-empty cache, otherwise they throw; the Web host starts through `StartupInitializer`, not `AppInitializer`. The plan now (1) refreshes per provider through the existing catalog services and accepts their own Warning as the single Warning, logging only when the service throws; (2) skips providers without a key through `ApiKeyService.HasKeyAsync` instead of `GetAllImageModelsAsync`; (3) starts the refresh from `AppInitializer` (Avalonia and MAUI) and from `StartupInitializer` (Web); (4) drops the change-notification event: the next opening of the models screen reads the refreshed cache, which is what the original criterion required of the screen. The card is translated to English. The stale task branch holds only the card edits; start from a fresh `origin/main`.
Plan amendment approved (standing instruction of 2026-11-07: "approve all tasks that will come").
