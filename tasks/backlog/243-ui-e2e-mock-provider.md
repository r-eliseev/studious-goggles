---
id: 243
status: done
assignee:
claimed:
created: 2026-11-07
---

# UI tests: local provider mock and “session → generation” scenario

## Description
Owner decision from 2026-11-07: after smoke, run scenarios through a local mock OpenAI-compatible provider (ADR 0029, `docs/specs/ui-e2e.md`). Without a key, the app cannot get past the first-run screen (`SessionsPageModel.OnAppearingAsync` → `ApiKeyPage`; the key is checked over the network), so the mock is the only way to continue without real keys and spending money. The app already supports custom OpenAI-compatible providers through `providers.json` in the data folder (`ProviderConfigLoader.UserFileName`), and the first-run screen lets the user select a provider (`apikey.provider`).

Unknown as of 2026-11-07; determine these first:
- How to put `providers.json` in the Release APK's data folder. `AppDataDirectory` is internal storage, and `adb run-as` works only for Debug builds. Preferred approach (discussed with the owner on 2026-11-07): use a `google_apis` emulator image (without Play) that permits `adb root`, then copy the file directly to `/data/data/com.eliseev.imagegen/files/`, with the same owner and permissions as the app folder, and run `restorecon`. This does not change the build. Verify that it works.
- Whether a Release build accepts the HTTPS mock with a test root certificate installed in the emulator's system store. Owner decision from 2026-11-07: the mock uses HTTPS, not HTTP. This avoids both an Android cleartext exception and an app change. Release apps with targetSdk 24+ trust only system certificates, not user certificates, so install the certificate under `/system/etc/security/cacerts`. From API 34 onward, system certificates are in the `com.android.conscrypt` APEX and are harder to replace. Therefore use an API 33 emulator (`google_apis`, x86_64, `-writable-system`); the app's minSdk is 26, so this is supported.
- Which requests the app sends to the provider for key validation, model listing, chat with a generation tool call, and image generation (from `OpenAiCompatibleClient` and the chat client code).

Depends on: 240 (and 241 for the Windows version of the scenario).
Environment: Linux (mock and its unit tests); GitHub Actions (scenario).

## Acceptance criteria
- Step 1, findings: record in “Comments” and `docs/knowledge/ui-e2e.md`:
  - app requests to the OpenAI-compatible provider (method, path, required response fields);
  - how to deliver `providers.json` on Android, in order of preference:
    1. Use `adb root` on a `google_apis` emulator and `adb push` into `files/`. Commands: `adb root`; `adb shell mkdir -p /data/data/com.eliseev.imagegen/files`; `adb push e2e-providers.json /data/data/com.eliseev.imagegen/files/providers.json`; `adb shell chown -R $(adb shell stat -c %u:%g /data/data/com.eliseev.imagegen) /data/data/com.eliseev.imagegen/files`; `adb shell restorecon -R /data/data/com.eliseev.imagegen/files`. The fixture repeats this after every `mobile: clearApp`, because `clearApp` removes the file.
    2. Use a Release build with a debug property, if .NET Android provides one (verify in documentation), and `run-as`.
    3. Use a separate Debug build, `-c Debug -p:EmbedAssembliesIntoApk=true`.
  - The mock key cannot be seeded: Android `SecureStorage` encrypts it with the device's Android Keystore key. Enter the key through the first-run screen; this also tests first run.
  - HTTPS (primary path):
    1. At job start, create a temporary root certificate and server certificate with `openssl` in the runner's temporary folder (`$RUNNER_TEMP`). The server certificate SAN must include `DNS:localhost`. Validity is two days. Private keys must not appear in the repository or artifacts.
    2. The mock listens at `https://0.0.0.0:5443` with this certificate: configure `Kestrel:Certificates:Default:Path` / `KeyPath` through environment variables.
    3. In the emulator, run `adb root`, `adb remount` (if it requests a reboot, run `adb reboot`, wait for `sys.boot_completed`, then run `adb root` and `adb remount` again). Then run `adb push <ca>.pem /system/etc/security/cacerts/<hash>.0`, where `<hash>` = `openssl x509 -subject_hash_old -noout -in <ca>.pem`, followed by `chmod 644` and `restorecon`. Install before starting the app and do not reboot again; a fresh app process will check trust in the CA.
    4. Run `adb reverse tcp:5443 tcp:5443`; set `baseAddress` to `https://localhost:5443/v1`.
    Check: `adb shell` and `curl` are unavailable in the emulator, so the first scenario step validates this by accepting the key without a network error. If `remount` does not work on API 33, record the output and try fallback B.
  - Fallback B: HTTP at `http://localhost:5080`. `ProviderRegistry` permits `http` only for `localhost`. Determine whether Release permits cleartext to `localhost`. If it does not, use `network_security_config` only for `localhost` and only with build property `ImageGenE2E=true`, so ordinary Release remains unchanged. This changes the build; first set status to `blocked` and ask the owner.
  - Windows (241): install the root certificate on the runner with `Import-Certificate -CertStoreLocation Cert:\LocalMachine\Root`; the runner has administrator permissions.
  Record the selected approach in ADR 0029 by adding a dated item to “Decision”.
  If config delivery or HTTP requires changes to ordinary Release, stop with status `blocked` and ask the owner.
- Mock project `ImageGen/ImageGen.E2E.MockProvider`:
  - ASP.NET Core minimal API, `net10.0`, address and port from `--urls`, HTTPS certificate from environment variables (step 1).
  - Return fixed data for the requests from step 1: key `e2e-mock-key` is valid; other keys return 401.
  - One chat model `e2e/assistant` and one image model `e2e/image`.
  - For a message, return one chat completion with a `generate_images` tool call (prompt from the user message, model `e2e/image`). Do not return a second response after the tool call: `AssistantService` starts generation and ends the turn after the tool call (clarified 2026-11-07).
  - Image is a 64×64 PNG embedded in the project.
  - Do not rewrite the prompt or substitute a model; the mock responds only with its own models.
  Test the mock unit tests with xUnit v3, like other non-E2E tests: use `WebApplicationFactory` and check every route.
- E2E `providers.json` is `ImageGen/ImageGen.E2E.Common/e2e-providers.json`: provider `e2e-mock`, `baseAddress` points to the mock, models use the `builtin-providers.json` format; it passes `ProviderRegistry` validation (test in `ImageGen.Core.Tests`: load through `ProviderConfigLoader` and assert there are no errors).
- Android test `MockSessionTests.FirstRunWithMock_GeneratesImage` in `ImageGen.E2E.Android`:
  1. Deliver `providers.json` using the method from step 1, select provider `e2e-mock`, enter its key, and select the assistant model.
  2. On the sessions screen, tap `sessions.new`; in chat, enter “a red square” in `chat.input` and tap `chat.send`.
  3. Within 60 seconds, `chat.feed` shows a run card with a completed image (state “готово”, not “ошибка”). Do not wait for a text response from the assistant.
  If the card has no id, add it as a separate step with a constant in `TestIds` and a check in `AutomationIdsTests` (as in 239).
- Workflow: start the mock in `test-android` before tests (`dotnet run --project ImageGen/ImageGen.E2E.MockProvider -- --urls https://0.0.0.0:5443 &` with the certificate from step 1; wait for readiness with `curl --cacert`), install the root certificate in the emulator, and run `adb reverse tcp:5443 tcp:5443`. Use API 33 with `emulator-options: -writable-system -no-window -gpu swiftshader_indirect -noaudio -no-boot-anim`; upload mock logs as an artifact.
- Windows: run the same full scenario in `ImageGen.E2E.Windows`: trust a temporary HTTPS certificate on the runner, write `providers.json` to AppData, complete first run, create a session, and verify the completed image from the mock.

## Analysis
Reference: the owner's original specification is in `docs/specs/ui-e2e-original/` (docx and md); ADR 0029 and `docs/specs/ui-e2e.md` take precedence if they differ.
- Core: `ImageGen/ImageGen.Core/Providers/ProviderConfigLoader.cs`, `ProviderRegistry.cs`, `builtin-providers.json` (format), `ImageGen/ImageGen.Core/OpenAiCompatible/OpenAiCompatibleClient.cs`, OpenAI-compatible chat client (`rg -n "chat/completions" ImageGen/ImageGen.Core`).
- Presentation: `ImageGen/ImageGen.Presentation/PageModels/ApiKeyPageModel.cs` (`SaveAsync`, provider/model selection on first run), `SessionsPageModel.cs` (`OnAppearingAsync`).
- MAUI: `ImageGen/ImageGen.Maui/Services/MauiAppPaths.cs` (`FileSystem.Current.AppDataDirectory`), `ImageGen/ImageGen.Maui/Platforms/Android/AndroidManifest.xml`.

## Plan
1. Establish the facts (step 1) and decide on config delivery and HTTP; use `blocked` if necessary.
2. Implement the mock with TDD, one route at a time.
3. Add `e2e-providers.json` and its validation test.
4. Add the Android scenario and workflow; run it in CI.
5. Add the Windows scenario: use shared `WindowsTestBase.cs`, add Windows CI steps to trust the certificate and start the HTTPS mock, and verify first run and image generation.

Files:
- Mock projects: `ImageGen/ImageGen.E2E.MockProvider/` (new) and `ImageGen/ImageGen.E2E.MockProvider.Tests/` (new), including their `.csproj` files, endpoint code, embedded PNG resource, and `WebApplicationFactory` tests.
- `ImageGen/ImageGen.E2E.Common/e2e-providers.json` (new), `ImageGen/ImageGen.E2E.Common/TestIds.cs`, `ImageGen/ImageGen.E2E.Common/ci/android-mock-e2e.sh` (new), `ImageGen/ImageGen.E2E.Common/ci/android-preflight.sh`.
- Tests: `ImageGen/ImageGen.Core.Tests/Providers/E2eProvidersConfigTests.cs` (new), `ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj`, `ImageGen/ImageGen.Presentation.Tests/E2E/AutomationIdsTests.cs`.
- E2E: `ImageGen/ImageGen.E2E.Android/MockSessionTests.cs` (new), `ImageGen/ImageGen.E2E.Android/ImageGen.E2E.Android.csproj`, `ImageGen/ImageGen.E2E.Windows/MockSessionTests.cs` (new), `ImageGen/ImageGen.E2E.Windows/WindowsTestBase.cs`.
- If using network config: `ImageGen/ImageGen.Maui/ImageGen.Maui.csproj`, `ImageGen/ImageGen.Maui/Platforms/Android/Resources/xml/e2e_network_security_config.xml` (new).
- `ImageGen/ImageGen.Maui/Controls/SessionsListView.xaml` (test id for the list-pane new-session action), `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml` (card id).
- Shared: `ImageGen/Directory.Packages.props`, `ImageGen/ImageGen.slnx`, `.github/workflows/ui-e2e.yml`.
- Documentation: `docs/knowledge/ui-e2e.md`, `docs/decisions/0029-ui-e2e-tests.md`.

### Verification
```
dotnet test --project ImageGen/ImageGen.E2E.MockProvider.Tests/ImageGen.E2E.MockProvider.Tests.csproj
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format ImageGen/ImageGen.E2E.MockProvider/ImageGen.E2E.MockProvider.csproj --verify-no-changes
bash scripts/check.sh
# GitHub Actions: test-android job with MockSessionTests
```

### Execution order (for Luna)
- First read only the files in the “Files” line and the named tests; compare signatures with current `main`.
- Follow the steps in order (ub-mode): failing test → run it and confirm it fails for the described reason → minimal fix → green test.
- Do not change files outside the “Files” line, rename existing members beyond the plan, or fix neighboring findings; those belong to separate tasks.
- If the code differs from the plan (missing method, different signature, test already exists), do not improvise; stop and write a question in “Comments”.
- Run `dotnet` commands one at a time, not in parallel. After each changed project, run `dotnet format <project> --verify-no-changes`.
- In the “Report”, record what changed, commands and results, and differences from the plan.

## Report

### 2026-11-10, codex-gpt6 (in progress)
- Recorded the OpenAI-compatible request/response contract in `docs/knowledge/ui-e2e.md` and the HTTPS-only API 33 emulator choice in ADR 0029. Existing task 238 evidence confirms root/remount on that emulator; Android release trust and config delivery remain for integrated CI verification.
- Added the minimal API mock with bearer-key validation, fixed chat/image model IDs, an echoed `generate_images` tool call, and an embedded 64×64 PNG. The four WebApplicationFactory tests failed first with 404 against the empty host, then passed after endpoint implementation.
- Added and validated `e2e-providers.json` through `ProviderConfigLoader`; added `chat.run` and `chat.generation` automation IDs. The automation ID test failed first for the missing generation-card ID, then passed after the XAML change.
- Added the Android first-run scenario and per-reset provider config installation. `dotnet build ImageGen/ImageGen.E2E.Android/ImageGen.E2E.Android.csproj --no-restore` and Android-target MAUI build succeeded (0 warnings/errors). The hosted emulator scenario has not run yet; this local environment has no emulator binary or connected device.
- Added the Windows first-run/generation scenario, optional provider-config seeding in `WindowsTestBase`, and CI setup for a trusted localhost certificate and HTTPS mock process. Windows E2E project build succeeded (0 warnings/errors); the Windows UI scenario requires the hosted interactive runner.
- `git diff --check` passed and the workflow YAML parses. The Windows E2E CI run is pending after this change.
- The first hosted Android attempt failed before test discovery: `android-emulator-runner` executes each `script` line in a separate shell, so its multiline loops were split and Bash reported `expecting "done"`. Moved the Android sequence to `ci/android-mock-e2e.sh` and made the workflow invoke that script as one command; `bash -n`, YAML parsing, and `git diff --check` pass. Re-running CI.
- The next hosted run confirmed Windows certificate trust and HTTPS startup, but the mock provider finished its first `dotnet run` compilation only after the 120-second readiness loop expired. CI now builds the mock before starting either UI runner and launches it with `--no-build`; this avoids compiling in the background readiness window. Re-running CI.
- A repeat Windows run still exhausted the readiness loop even though the mock eventually answered once. Replaced `dotnet run` with direct execution of the built DLL and changed the Windows health probe to `curl.exe`, recording the last exit code/status and detecting an exited mock process. The next hosted run will confirm readiness and UI behavior.
- The hosted Windows run then passed certificate setup, mock readiness, and server requests, but could not find the provider option because WinUI exposes the Picker popup outside the app window's UIA subtree. The screenshot shows the popup on screen. Added a desktop-level name lookup in `WindowsTestBase` so native popup entries can be selected; local Windows E2E project build passes (0 warnings/errors). Re-running CI.
- The next Windows run selected the provider and assistant model and reached the Sessions page. FlaUI did not expose the `sessions.new` AutomationId even though the button is visible, so the scenario now selects the button by its accessible name (`New session`) through the desktop-level lookup. The next CI run will validate the rest of the flow.
- The latest Android run installed the system CA, connected to the HTTPS mock, and passed the existing first-run tests; the new scenario failed because the native Picker row text is `E2E Mock (e2e-mock)`, not exactly `E2E Mock`. Changed the locator to match the visible prefix. Windows will use the same PR's updated button-name lookup.
- The latest Windows run reached the session page but the desktop UI tree contains controls that do not expose the UIA Name property. The desktop lookup now skips those unsupported properties while scanning for the requested accessible name.
- The following hosted Windows run showed `NullReferenceException` in the same lookup: some UIA elements return a null `Name` instead of throwing `PropertyNotSupportedException`. The screenshot confirms the `New session` button is visible. Updated the lookup to ignore null names too; awaiting the next hosted run.
- The next Windows run passed the first-run provider/key/model selection and reached the session model picker. Its screenshot shows provider groups, with `e2e (1)` collapsed; the test incorrectly expected an `All models` option. Changed the scenario to expand the e2e provider group and select its image model.
- The next Windows screenshot confirmed that the e2e group expanded and the image model was selected, but the test stayed on the model page because it did not press `Save`. Added the save action before looking for the chat input.
- The Android run then failed at the `New session` locator. Its captured UI hierarchy exposes the visible button with `content-desc="New session"`, while XPath by visible text returned no element. Changed the Android test to locate it by Appium AccessibilityId.
- The next Android run passed the `New session` step and reached session model selection. The model search element became stale during page refresh; the report screenshot shows the e2e provider group collapsed. Added a bounded retry using the search bar accessibility name and expanded the provider group before selecting the model.
- On the following Android run, Appium still failed to locate `New session` by accessibility description although its captured UI tree includes the button. Added a dedicated `sessions.list.new` automation id for this pane action and changed the scenario to use it.
- The next Android run reached the session model page with the search query applied, but its failure screenshot showed the `e2e (1)` image group still collapsed. The description selector may have matched a hidden group; restrict it to displayed elements and assert the expanded state before selecting the image model.
- The next Windows run reached the chat with the prompt visible, but its send button remained disabled after FlaUI `Enter`. Changed prompt entry to real keyboard input and added a wait/assertion for the send command to become enabled.
- The following CI run passed Windows but its Android emulator step remained `in_progress` for over 45 minutes without making the job logs available. Canceled that hung run; added per-command ADB deadlines, bounded Appium/preflight/test commands, and reduced the Android job timeout to 25 minutes so a repeat hang fails promptly with uploaded reports when possible. The exact stalled command is unknown until the bounded rerun reports it.
- Passed targeted `dotnet test` for `E2eProvidersConfigTests` and `AutomationIdsTests`, all 4 mock endpoint tests, and `dotnet format --verify-no-changes` for the mock, mock tests, E2E Common, E2E Android, Core.Tests, Presentation.Tests and MAUI projects. CI is pending.
- CI run [42966980208](https://github.com/r-eliseev/studious-goggles/actions/runs/42966980208): the Windows first-run and image-generation E2E passed. On Android, CA installation without a second reboot worked: first-run key validation reached the session model page and loaded `e2e/image`. The test then failed in the external `uiautomator dump`/XML parsing used to tap the provider group. The captured screenshot and UI hierarchy show that the group was already expanded and its model row visible, so removed that unnecessary dump/tap helper and retained an explicit wait for the expanded state. Android generation rerun pending. `dotnet format ImageGen/ImageGen.E2E.Android/ImageGen.E2E.Android.csproj --verify-no-changes --no-restore`, project build, and `scripts/check.sh` pass after the change.
- CI run [42969061955](https://github.com/r-eliseev/studious-goggles/actions/runs/42969061955): Windows E2E passed again. Android reached the chat and the mock returned 200 for models, chat completion and image generation, but the test's prompt wait read `IWebElement.Text`; MAUI exposes the prompt through `contentDescription`. The new session also carries the unavailable default OpenRouter model, so this test run has a second generation that correctly errors for lack of an OpenRouter key alongside the mock generation. Updated the prompt wait to inspect the content description and the success assertion to scroll the feed while looking for the completed `e2e/image` card. The Android project build, formatter and harness pass; hosted rerun pending.
- CI run [42974250705](https://github.com/r-eliseev/studious-goggles/actions/runs/42974250705): Android and Windows build, preflight and full UI E2E passed; the first run confirms Android system-CA trust and successful image generation from the HTTPS mock. The Windows run confirms its runner certificate setup, HTTPS mock and image-generation flow. Android's successful card was below the default OpenRouter error card; the scroll gesture direction was corrected based on the failure screenshot. Related build and harness workflows also passed (runs 42974250779, 42974250756 and 42974250767). `dotnet format ImageGen/ImageGen.E2E.Android/ImageGen.E2E.Android.csproj --verify-no-changes --no-restore`, Android E2E project build and `scripts/check.sh` pass.

## Comments

### 2026-11-07, claude-audit
Created following the owner's “Smoke, then mock” decision. Code check confirms the app cannot get past the first-run screen without a key; OpenAI-compatible providers can already be configured through `providers.json`. Android config delivery and HTTP behavior were unknown and are the first task step. Plan for Luna.

### 2026-11-07, owner (in chat, recorded by claude-audit)
The owner provided the UI testing specification and chose NUnit + Allure, smoke first and then the mock. The plan was approved under the earlier instruction “approve all tasks with plans”. The owner explicitly requested the `.github/workflows/` change in this specification (ADR 0029).

### 2026-11-07, codex-gpt6
Step 1 found a mismatch between the plan and current `AssistantService`: after a chat completion with `generate_images`, the app starts generation and ends the turn; it does not send the tool result back to the chat model or receive a second text response. The mock plan required a second `Done` response. No code was changed. Clarification requested: should the test cover the actual flow “tool call → generated image/run message”, or should a separate feature add a second assistant call returning `Done`? The task was blocked pending an answer.

### 2026-11-07, claude-audit (reply to codex-gpt6's question)
Confirmed from the code: after `generate_images`, `AssistantService` starts a run and ends the turn without sending the tool result back to the model. The plan was wrong. Test the actual flow: tool call → run card → completed image. A second assistant call returning “Done” is not needed; it is not a feature. The mock criteria and scenario step 3 were corrected. Status returned to `approved`, with codex-gpt6 retaining the claim.

### 2026-11-10, codex-gpt6
Removed the claim during a status review: there was no confirmed branch or current implementation for this task. Dependencies 240 and 241 are complete, so the task remains `approved` and available to claim.

### 2026-11-10, codex-gpt6
Re-claimed after confirming tasks 240 and 241 are done in main. Source inspection confirms `GET /v1/models` key-check semantics, configured (not fetched) model catalogs, `POST /v1/chat/completions` tool-call fields, and `POST /v1/images/generations` base64 response. Selected the planned API 33 system-CA + HTTPS + `adb reverse` route; Release trust and app-data delivery still require the integrated CI check. The local executor has no Android emulator binary, so device behavior must be verified by GitHub Actions.

### 2026-11-11, owner
Approved adding a complete Windows scenario to the plan, including `WindowsTestBase.cs` and Windows CI steps for the certificate and mock.
