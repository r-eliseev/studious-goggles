# Development environments

A general overview: where each project builds and what each environment can do. The facts below were collected from checks of 2026-10-27 — 2026-10-30; the platform marks keep the check status as of those dates.

- **Windows machine** — the full environment, including the MAUI Windows target.
- **Claude Code cloud session** (a Linux container) — without the MAUI Windows target, with the Android target.
- **Kiro Web** (a browser session, Linux) — Android is only built (no emulator); the root is read-only, the tools were in `/projects/sandbox/.toolcache`. Kiro Web is no longer used.
- **VPS** (a permanent Linux machine) — without the MAUI Windows target, with the Android target; the environment survives the session. Builds and tests the Linux projects, including Avalonia (headless tests and launching the window under `Xvfb`).

Items marked "not checked" have not been checked in a real environment yet.

## What builds where
| Project | Windows machine | Cloud session (Linux) | Kiro Web (Linux) | VPS (Linux) |
|---|---|---|---|---|
| `ImageGen.Core`, `ImageGen.Core.Tests` | yes | yes, checked | yes, checked (59/59 tests) | yes, checked (356/356 tests) |
| `ImageGen.Presentation`, `ImageGen.Presentation.Tests` | yes | yes, checked (112/112 tests) | not checked | yes, checked (277/277 tests) |
| `ImageGen.Avalonia`, `ImageGen.Avalonia.Tests` (desktop, headless) | yes, checked 2026-10-28 (45 tests, 1 skipped — Secret Service; the app launches) | yes, checked (4/4 tests; launching the window without a display not checked) | not checked | yes, checked (118/120 tests, 2 skipped — Secret Service and DPAPI); the app launches under `Xvfb` |
| `ImageGen.Avalonia.Android` | not checked | not checked | not checked | yes, checked 2026-11-06: Debug and Release APK; launch on a device not checked |
| `ImageGen.Data`, `ImageGen.Data.Tests` | yes | yes, checked | yes, checked (16/16 tests) | yes, checked (70/70 tests) |
| `ImageGen.Maui`, the Android target (`net10.0-android`) | yes | yes, checked (APK built from the template) | build yes (not checked on the project); launch/debugging no — no emulator, `adb`, `/dev/kvm` | build yes, checked (APK built from the project); launch/debugging — only on a real device over the network through `adb connect`, the emulator does not work (no KVM) |
| `ImageGen.Maui`, the Windows target (`net10.0-windows…`) | yes | no, checked (the XAML compiler is Windows-only) | no, checked (as in the cloud session) | build yes, checked, but through Wine and unofficially (1 h 45 min); launch and debugging no — WinUI 3 does not work under Wine |

## Live API check (task 170)

The tool uses the real `ImageGen.Core` clients and writes a safe HTTP trace through `LoggingHttpHandler`/`FileDebugTrace`. Never give keys to third parties. Command-line parameters may be kept in the shell history and be visible to local processes; use the environment variables `IMAGEGEN_LIVE_OPENROUTER_KEY`, `IMAGEGEN_LIVE_FAL_KEY`, `IMAGEGEN_LIVE_TOGETHER_KEY` or `--provider-key-<id>` for additional providers.

Before paid requests, limit the budget and the number of calls per provider. The defaults: `$1` and 40 requests. For OpenAI-compatible/Anthropic providers from your own `providers.json` set `--openai-compatible <path>` and `--provider-key-<id> <key>`. Models can be pinned with `--openrouter-image-model`, `--openrouter-chat-model`, `--fal-image-model`, `--together-image-model`; without them the first one from the catalog/config is chosen.

```bash
dotnet run --project ImageGen/ImageGen.LiveCheck -- --budget-usd 1 --max-requests 40 --only openrouter,fal --out tmp/live-check
```

Run the separate Lyria music scenario with `--only music`, a budget of at least `$0.10` and `IMAGEGEN_LIVE_OPENROUTER_KEY`; without a key or budget it is skipped without a request. It saves the clip to `tmp/live-check/<UTC date>/music.mp3` or `music.wav` when retrying after a format/voice error. A live request is billed by the provider.

The report and the debug trace appear under `tmp/live-check/<UTC date>/report.md` and `tmp/live-check/debug/<UTC date>/trace.jsonl`. Check each scenario by hand by the status column, the model and the trace link. Generation scenarios reserve the estimated price before the request and stop if it does not fit into the budget. Catalog prices may be outdated or variable; the report marks them as estimates. The tool's tests work without a network and keys.

### OpenRouter embeddings

A check with two short texts: `dotnet run --project ImageGen/ImageGen.LiveCheck -- --only embeddings --budget-usd 0.01 --max-requests 1 --out tmp/live-check`. Without `IMAGEGEN_LIVE_OPENROUTER_KEY`, LiveCheck marks the scenario as `unverified` and sends no request. With a key, the request goes through `ITextEmbeddingClient` and `OpenRouterClient.EmbedAsync`, that is, the same response parsing the app's index uses; the report contains the HTTP status, the requested and the answered models, the upstream provider, the number and size of the vectors, the presence of `index`, the usage and the parsing result.

The owner's live answer of 2026-11-07: `POST /api/v1/embeddings`, two texts, HTTP 200; the answer contained `model: "BAAI/bge-m3"`, `provider: "DeepInfra"`, two vectors of 1024 numbers, the `index` fields and `usage.cost: 1.6e-07 USD` (the same value as `cost_details.upstream_inference_cost`). In another log of the owner OpenRouter returned `model: "parasail-bge-m3"`: the name in the answer may denote the upstream model and differ from the requested `baai/bge-m3`; the app checks the shape and dimension of the vectors, but stores the requested identifier. Running the updated LiveCheck with a key was not repeated in this working session, there is no key. The `usage.cost` unit is confirmed as USD by a live answer.

GUI debugging: the Windows app — only on the Windows machine; live Android (emulator/device, breakpoints, Hot Reload) is unavailable in the cloud session and Kiro Web; on the VPS the emulator does not start, but debugging on a real device over the network is possible.
- Building the whole solution (`dotnet build ImageGen/ImageGen.slnx`) and `dotnet format ImageGen/ImageGen.slnx` do not run on Linux, because the solution includes the MAUI project, and without the MAUI workload it does not load. Formatting and building per project: `dotnet format ImageGen/<Project>/<Project>.csproj --verify-no-changes`.
- In CI on ubuntu-latest installing the `maui-android` workload failed with "Disk quota exceeded", so Android in CI is built on Windows (`.github/workflows/build.yml`, the `maui` job). In the cloud session the Android build is checked, but CI is not set up there.

## Web host

`ImageGen.Web` runs on .NET 10. Startup requires a non-empty `IMAGEGEN_WEB_PASSWORD`; the data folder is set by `IMAGEGEN_DATA_DIR` (`/data` by default) and contains the SQLite database, the encrypted secrets and the ASP.NET Core Data Protection keys.

A local run: `IMAGEGEN_WEB_PASSWORD=test dotnet run --project ImageGen/ImageGen.Web/ImageGen.Web.csproj`. Health endpoint: `GET /api/health`. Integration tests: `dotnet test ImageGen/ImageGen.Web.Tests/ImageGen.Web.Tests.csproj`.

## The "Environment" line in a task
Every task has an "Environment" line in its "Description". The rule in `docs/tasks.md`: an agent without the needed environment does not claim the task.

Since 2026-10-28 (the owner's decision: as much development on Linux as possible) this split applies:
- **Linux or Windows** — everything checked by builds and tests: the core, storage, `Presentation`, Avalonia (build, headless tests, launching the window on a Linux desktop), MAUI through the Android target (build and `dotnet format` with the Android SDK). For such tasks the MAUI Windows target is checked by CI (the `maui` job on `windows-latest`).
- `ImageGen.Avalonia.Android` builds on Linux with the Android SDK and the `android` workload: `dotnet workload install android`, then `dotnet build ImageGen/ImageGen.Avalonia.Android/ImageGen.Avalonia.Android.csproj -c Release`. Debug and Release were checked on the VPS on 2026-11-06; launching on a device is not checked yet (task 198).
- **Windows only** — what cannot be checked otherwise: launching and manually checking the MAUI app on Windows, behavior that does not exist on Linux (DPAPI, WinUI specifics), edits of `Platforms/Windows`.
- **A human at the screen** — manual scenarios (tasks 016, 046).

A task with MAUI markup goes to Linux by default: the screen logic is in `Presentation`, its UI can be seen and clicked in Avalonia on Linux (the same scenario), and the MAUI markup is checked by the Android build and CI. If such a task has only a check left that the environment cannot do (a Windows launch, macOS, a manual screen), it is not kept `in-progress`: see `docs/tasks.md`, "Finishing".

## Logs
The app log is written to `imagegen-yyyyMMdd.log` files in the `logs` folder inside the data folder (ADR 0010). The first line of every start names the version, the head, the OS and the folder path.
- **Avalonia, Linux:** `~/.local/share/ImageGen/logs` (checked, `Environment.SpecialFolder.LocalApplicationData`, headless tests).
- **Avalonia, Windows:** `%LOCALAPPDATA%\ImageGen\logs` (checked 2026-10-28: the first line of a start — version, head, OS, data and log folders).
- **Avalonia, macOS:** unknown, the path is not checked (no macOS machine).
- **Avalonia Android:** `<FilesDir>/ImageGen/logs` (inside the app's private Android data folder).
- **MAUI:** the app's `AppDataDirectory` folder (`FileSystem.Current.AppDataDirectory`), the `logs` subfolder. On Android the files can be handed over through Settings → Share logs: the system "Share" menu opens with copies of the files, including the current open log. The APK's data folder is closed to other apps; `run-as` is unavailable for a release APK, `allowBackup=false`. Messages are also written to the `Debug` output (logcat). Handing over and clearing wait for a manual check on a device in task 140.
- Level: `IMAGEGEN_LOG_LEVEL=Trace|Debug|Information|Warning|Error` or `logging.json` `{"level": "Debug"}` in the data folder; by default Debug in a Debug build, Information in Release.
- The debug trace (task 085) is enabled separately from the level: `IMAGEGEN_DEBUG_TRACE=1` or `{"debugTrace": true}` in `logging.json`. It writes to `{data folder}/debug/<date>/trace.jsonl` with pictures next to it; there is no cleanup (unknown whether it is needed). Specification: `docs/specs/debug-trace.md`.
- The Avalonia data folder is set by the `IMAGEGEN_DATA_DIR` variable (a second profile for manual checks). The `ImageGen.Avalonia.Tests` tests do not use it: they work in a temporary folder and with an in-memory key store (`App.HostOverrides`, task 048).

## Release
- The `.github/workflows/release.yml` workflow runs on a push (and PR merge) to `main` that changes the shipped code — `ImageGen/**` except the `*.Tests` and `ImageGen.E2E.*` projects, or `global.json` (a `paths` filter, the owner's decision of 2026-11-09; documentation, tasks, scripts and workflows alone do not release) — and on a `vMAJOR.MINOR.PATCH` tag (GitHub does not apply path filters to tags) (for example, `v0.1.0`) and creates a GitHub Release. A push to `main` releases the next patch after the latest stable tag (`v0.1.7` → `v0.1.8`): the tag is created on this commit by `gh release create` itself (the owner's decision of 2026-11-06). If the commit already has a stable tag, the push run publishes nothing — the tag run makes the release. Runs on `main` go one after another (`concurrency`), so numbers do not repeat; minor/major are changed by setting a tag by hand. The release is created with notes from `scripts/release-notes.sh`. The notes list the tasks that became `done` after the previous `vX.Y.Z` tag by version (by the task files and `tasks/DONE.md` in the tag's version; the task folder does not matter), and end with a link to the commit comparison; the first tag lists all finished tasks. The same text locally: `git fetch --tags && scripts/release-notes.sh <tag> [<previous tag>]`. A manual run (`workflow_dispatch`, "Run workflow" in Actions) builds the same packages as run artifacts, without a release, with the version `0.0.<run number>`: this is how the workflow is checked without releasing.
- The shell regression of the release notes (including an empty previous set of done tasks): `bash scripts/tests/release-notes-empty-previous.sh`.
- Packages (the owner's decision of 2026-10-29: both heads for all platforms):
  - Avalonia, self-contained: `linux-x64`, `linux-arm64` (`.tar.gz`), `win-x64`, `win-arm64` (`.zip`), `osx-x64`, `osx-arm64` (`.tar.gz`, built on a macOS runner: `osx-arm64` does not start without an ad-hoc signature, and the SDK signs the apphost only on a Mac).
  - MAUI Windows: `win-x64`, `win-arm64`, without MSIX (`WindowsPackageType=None`), with the Windows App SDK inside, `.zip`.
  - MAUI Android: `.apk`, installed by hand. Signing: the permanent key from the repository secrets `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASSWORD`, `ANDROID_KEY_ALIAS`, `ANDROID_KEY_PASSWORD` (task 163; certificate `CN=ImageGen, O=eliseev`, SHA-256 `6711f8badc39c087512e78a04ab4d7a8313d9d00b33081bf139867fc55b10277`). Without the secrets the build uses a debug key with a warning; such an APK does not install over the previous release. Up to and including v0.1.5 different debug keys were used, so the app has to be uninstalled together with its data. All MAUI targets use `com.eliseev.imagegen` and show the name Omnilite.
  - Avalonia Android: `ImageGen-Avalonia-<version>-android.apk`, built and included in every release separately from MAUI; installs next to MAUI and has its own application ID, data folder and provider keys. The workflow uses the same signing secrets; the certificate of the release APK must be confirmed on a manual workflow run.
  - MAUI Mac Catalyst and iOS are not published (the owner's decision of 2026-10-29). The current `maui` workload (Mac Catalyst SDK 27.0) requires Xcode 27.1, while the GitHub `macos-latest` and `macos-26` runners had only Xcode 26.x (up to 26.6) on 2026-10-29. On a Mac there are the Avalonia `osx-x64` and `osx-arm64` packages. iOS, moreover, does not install on a device without an Apple certificate and profile.
  - If Mac Catalyst is brought back: on macOS install the whole `maui` workload, not `maui-maccatalyst` (restore checks all target platforms of the project, otherwise `NETSDK1147`), and a runner with the needed Xcode.
- MAUI version: `ApplicationDisplayVersion` from the tag (or the one computed for a push to `main`), `ApplicationVersion` — the workflow run number (Android requires an integer that grows from release to release).
- MAUI Windows is published with `-p:ImageGenWindowsRid=<rid>`; the project passes it into `RuntimeIdentifier` only for the Windows TFM, without affecting project references and the other platforms. The `win-arm64` archive of release `v0.1.4` actually contained x64 PE files. The workflow checks the Machine field of the PE headers before packaging; details and the check number are in the [MAUI reference](maui.md#publishing-maui-for-windows-with-a-rid).
- Checked by the `v0.1.3` tag run (2026-10-29): all Avalonia packages, MAUI Windows `win-x64`/`win-arm64` and Android build. Locally on Windows the published MAUI `win-x64` launches. The arm64 and macOS packages were not launched on devices.
- The first successful release is `v0.1.2` (2026-10-29, only Avalonia `linux-x64` and `win-x64`); `v0.1.0` and `v0.1.1` failed at the `win-x64` publish step, `v0.1.3` at the Mac Catalyst build (no release).


## The Windows development machine

A description of the environment where the MAUI Windows target is built and the UI is checked. A general overview of environments: `docs/knowledge/dev-environment.md`. Recorded 2026-10-27 from the earlier description of the environment. This machine was not checked in the cloud session revision: everything below is "not checked" on the machine itself.

## What is needed
- Windows with the MAUI workloads installed (`maui-windows`, `maui-android`), the .NET 10 SDK.
- For the Android target: the Android SDK and a JDK (not checked on this machine).
- Checking the skeleton (task 016) needs a human at the screen: the UI is checked by hand.

## What builds only here
- The `net10.0-windows10.0.19041.0` target of `ImageGen.Maui`. It does not build on Linux: the WinUI XAML compiler works only on Windows (checked in the cloud session, see the "Claude Code cloud session" section of this file).
- The manual check of the skeleton UI (task 016) — only on Windows.

## Commands
- Windows build: `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-windows10.0.19041.0`
- Android build: `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android`
- Launch: `ImageGen/ImageGen.Maui/bin/Debug/net10.0-windows10.0.19041.0/win-x64/ImageGen.Maui.exe`
- Formatting per project: `dotnet format ImageGen/<Project>/<Project>.csproj --verify-no-changes`


## Claude Code cloud session (check of 2026-10-27)

What really works in the cloud session (the Claude Code Linux container) and how to install what is missing. The revision was done on 2026-10-27 on this machine (Ubuntu 24.04, x86_64, 4 CPU, 15 GB RAM, about 23 GB free on the disk). Items are marked "checked", "does not work", "not checked". A general overview of environments: `docs/knowledge/dev-environment.md`. The Windows machine is described in the "Windows development machine" section of this file.

## Works (checked)
- **.NET SDK 10.0.403:** `dotnet build`, `dotnet test`, `dotnet format` pass for the core and storage.
- **NuGet:** `api.nuget.org` is reachable.
- **MAUI, the Android target:** `dotnet build -f net10.0-android` builds an APK from a fresh `dotnet new maui` template. The Android API 36 platform is needed.
- **Android SDK:** the command-line tools install from `dl.google.com`, licenses are accepted through `yes | sdkmanager --licenses`.
- **JDK 21:** installed, suitable for MAUI Android.
- **Chromium through Playwright:** the installed browser version depends on the session image. Do not run `playwright install`.
- **Node/npm:** Node.js 22.12+ is required for Vite 8 and Vitest 5; `registry.npmjs.org` is reachable. In this working session (2026-11-02) `/usr/bin/node` was v18.19.0, so Node 22.23.4 was temporarily installed into `tmp/node22` inside the repository for the checks. The Playwright browser binary was not found in `/root/.cache/ms-playwright` and `/opt`, so E2E cannot be run in this session without installing a browser; the project instruction forbids `playwright install`.
- **Python 3.13, git 2.43, curl:** installed; `pypi.org` is reachable.
- **GitHub through MCP** (`mcp__github__*`): reading PRs, check runs, comments, creating PRs and replying in threads.
- **Harness:** `scripts/check.sh` passes.

## Works with caveats
- **Docker:** the CLI is installed (29.9.2), the daemon does not start automatically. Start: `dockerd > ~/dockerd.log 2>&1 &`. Docker Hub answers `429 Too Many Requests` to anonymous requests, images for the build are not pulled.
- **`gh`:** installed, not authenticated (`GH_TOKEN` is rejected). Use MCP for GitHub.
- **Background processes:** a foreground `sleep` is blocked. Wait through `run_in_background` or `until <condition>; do sleep 3; done` in the background.

## Does not work (checked)
- **MAUI, the Windows target:** `dotnet build -f net10.0-windows10.0.19041.0` does not pass on Linux. Without a workaround — `NETSDK1100`. With `-p:EnableWindowsTargeting=true` and an explicit TFM it fails at XAML compilation (`XamlCompiler output file … was not created`). The WinUI XAML compiler is a Windows tool. So tasks 012–016 and the Windows part of 034 are not done here.
- **The sqlite3 CLI:** not installed. Not needed: the DB goes through `Microsoft.Data.Sqlite`. Installing `apt-get install sqlite3` was not checked.
- **`chromium` in PATH:** no. Use the Playwright version from `/opt/pw-browsers`.
- **The Android emulator (launching the app):** `/dev/kvm` is missing, the CPU has no `vmx`/`svm` flags. Measured 2026-10-27 (emulator 37.2.12, Android 36 Google APIs, 4 vCPU, 3 GB RAM, `-no-accel -gpu swiftshader_indirect`):
  - the `arm64-v8a` image does not start: `System image must match the host architecture`;
  - the `x86_64` image starts, but booting did not finish in 31 minutes: `sys.boot_completed` is not set, `bootanim` is `running`. The emulator was stopped.
  - Conclusion: debugging the launch and the UI of the MAUI app is not done in the cloud session. The build, APK analysis and logic tests are done.

## How to install (needed in every new session: the environment is not kept)
The state does not survive the session: the SDK, the Android SDK and a running `dockerd` are installed again.

```bash
# .NET 10 SDK
curl -sSL https://dot.net/v1/dotnet-install.sh -o ~/dotnet-install.sh
bash ~/dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet"
export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1

# The MAUI workload for Android
dotnet workload install maui-android --skip-manifest-update

# Android SDK (the MAUI template requires API 36)
mkdir -p ~/android-sdk/cmdline-tools && cd ~/android-sdk
curl -sSL -o clt.zip https://dl.google.com/android/repository/commandlinetools-linux-11076708_latest.zip
python3 -c "import zipfile;zipfile.ZipFile('clt.zip').extractall('cmdline-tools-tmp')"
mv cmdline-tools-tmp/cmdline-tools cmdline-tools/latest && chmod +x cmdline-tools/latest/bin/*
export ANDROID_HOME="$HOME/android-sdk"
yes | cmdline-tools/latest/bin/sdkmanager --licenses
cmdline-tools/latest/bin/sdkmanager "platform-tools" "platforms;android-36" "build-tools;36.0.0"
# Android build: dotnet build -f net10.0-android -p:AndroidSdkDirectory=$ANDROID_HOME

# Docker (optional)
dockerd > ~/dockerd.log 2>&1 &
```

To reproduce the Android check: `dotnet new maui -n Probe`, then `dotnet build -f net10.0-android` in the project folder.

## Cloud session quirks
- **`dotnet format` for the MAUI project:** without the Android SDK the project does not load (`The Android SDK directory could not be found`), and `dotnet format` prints "Required references did not load". With the SDK from the "How to install" section `dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes` passes (checked 2026-10-27). Run `dotnet restore` on the same project first.
- Tests do not go to the network: OpenRouter is substituted by `StubHttpMessageHandler`, no key is needed.
- The task scripts (`scripts/claim-task.sh`, `scripts/release-task.sh --publish`, `scripts/new-task.sh --publish`) write to the origin branch with the same name as the current one, so the cloud session does not need to push to `main`. The task branch must be published before `claim-task.sh` (`git push -u origin task/NNN-name`), and `git pull --ff-only` done after it. The order and limits: `docs/tasks.md`, "Starting work", and `AGENTS.md`.


## VPS (checks of 2026-10-27 — 2026-10-29)

A permanent Linux machine (VPS), the fourth environment of the repository. A general overview of environments: `docs/knowledge/dev-environment.md`. Recorded 2026-10-27, all items checked on the machine that day.

The difference from the other Linux environments: the environment **survives the session** — the installed SDK and Android SDK stay in place, there is no need to install them again. The recipe below is needed only when the machine is recreated.

The machine: Ubuntu 24.04.5 LTS, x86_64, AMD EPYC 9355P, 4 vCPU, 15 GB RAM, about 185 GB free on the disk. It runs under a KVM hypervisor itself.

## What works (checked)
- **.NET SDK 10.0.403** — the same version as in the "Claude Code cloud session" section of this file. Installed in `/usr/share/dotnet` (in the cloud session it was `~/.dotnet`).
- **Core and storage:** `dotnet build` and `dotnet test` pass. On `main` of 2026-10-29: Core.Tests 356 tests, Data.Tests 70 — all pass.
- **Presentation:** `ImageGen.Presentation` and `ImageGen.Presentation.Tests` build and are tested on the VPS: 277/277 tests.
- **Avalonia:** `ImageGen.Avalonia` builds, the headless tests pass — 118 of 120, two skipped (Secret Service: needs a session keyring; DPAPI — Windows only). **The app launches** under the virtual display `Xvfb` (see below).
- **Avalonia desktop media playback:** Linux playback requires the system packages `libvlc5` and `vlc-plugin-base`; without them the player service reports unsupported and hides Play. Windows and macOS desktop publishes include their LibVLC runtime NuGet packages.
- **`dotnet format --verify-no-changes`** passes for all projects: Core, Core.Tests, Data, Data.Tests, Presentation, Presentation.Tests, Avalonia, Avalonia.Tests, Maui.
- **MAUI, the Android target:** `dotnet build -f net10.0-android` builds an APK (`com.companyname.imagegen.maui.apk` and `-Signed.apk`), 0 warnings. The `maui-android` workload is installed.
- **Android SDK:** the command-line tools from `dl.google.com`, licenses accepted, `platforms;android-36`, `build-tools;36.0.0`, `platform-tools` 37.0.1, the `emulator` package.
- **JDK 21** (openjdk-21-jdk-headless, `/usr/lib/jvm/java-21-openjdk-amd64`) — suitable for MAUI Android.
- **Xvfb** — a virtual display for launching the Avalonia window on a machine without a screen.
- **Docker:** 29.9.2, the daemon is running, overlayfs, cgroup v2. Unlike the cloud session, **Docker Hub answers**: an anonymous `docker pull` passes (checked on `hello-world`).
- **GitHub:** `gh` 2.103.3 is authenticated through OAuth (device flow), a token with the scopes `repo`, `read:org`, `gist`; `gh auth setup-git` is configured, the HTTPS remote works with the token. Git 2.43.0, identity `r-eliseev <47721526+r-eliseev@users.noreply.github.com>`.
- **Harness:** `scripts/check.sh` passes.

## What works with caveats
- **The `C.UTF-8` locale and Cyrillic ranges in `grep -E`.** Under `C.UTF-8` an expression like a Cyrillic letter range gives "Invalid collation character". This used to silently skip check 5 in `scripts/check-docs.sh`; on 2026-10-28 the range was removed from the script, and the check works under any locale. Do not use Cyrillic ranges in new scripts (the "Latin only" check uses `grep -P` with Unicode properties under an explicit `C.UTF-8`). `export LC_ALL=en_US.UTF-8` in the machine's `~/.bashrc` may stay.
- **The `gh` token is stored in plain text** in `/root/.config/gh/hosts.yml` (mode 600): there is no keyring daemon in the container. This matters for a machine that is saved into an image or a snapshot.
- **The Android emulator is installed but does not start** — see below.

## Does not work (checked)
- **The Android emulator — no.** `emulator -accel-check` answers `accel: 3`, "KVM requires a CPU that supports vmx or svm". The cause: the VPS itself runs under KVM, but **nested virtualization is not passed through** — `/proc/cpuinfo` has neither `vmx` nor `svm`, `/dev/kvm` is missing, `modprobe kvm_intel` gives "Operation not supported". The emulator is possible only with full software emulation (TCG). This is the same class of environment as the cloud session, where the measurement gave "did not boot in 31 minutes" (see the "Claude Code cloud session" section of this file).
- **The MAUI Windows target — no.** Checked on this machine: without flags — `NETSDK1100`; with `-p:EnableWindowsTargeting=true` — `NETSDK1005` (the TFM is not in the project's `TargetFrameworks` on Linux); with a hard `-p:TargetFrameworks=net10.0-windows10.0.19041.0` the flag leaks into `ImageGen.Core` and breaks its restore. The result matches the entry in the "Claude Code cloud session" section of this file.
- **Debugging the UI in the emulator** — because there is no KVM. The build, APK analysis and logic tests are done.

## Why the Windows target does not build on Linux (the exact cause)

Checked on the VPS, 2026-10-27. Useful because the old wording "the XAML compiler is a Windows tool" does not say what exactly breaks.

1. `NETSDK1100` — Windows targets cannot be built without `-p:EnableWindowsTargeting=true`.
2. `NETSDK1005` — even with this flag the TFM is not in the project's `TargetFrameworks` on Linux: in `ImageGen.Maui.csproj` it is added under the condition `$([MSBuild]::IsOSPlatform('windows'))`. To get further, the TFM must be added to `TargetFrameworks` by hand (then the flag leaks into `ImageGen.Core` and breaks its restore).
3. Then the actual point of failure:
   ```
   .../microsoft.windowsappsdk.winui/1.8.260528001/tools/net472/XamlCompiler.exe: Exec format error
   error : XamlCompiler output file "obj/Debug/net10.0-windows10.0.19041.0/win-x64/output.json" was not created.
   ```
   `XamlCompiler.exe` is .NET Framework 4.7.2, a Windows PE. Linux cannot execute a PE, hence "Exec format error".
4. The package also contains a cross-platform implementation of the same compiler: `tools/net6.0/` — only managed assemblies (`Microsoft.UI.Xaml.Markup.Compiler.dll` and others), without an `.exe`. But `Microsoft.UI.Xaml.Markup.Compiler.interop.targets` (line 27) with `MSbuildRuntimeType == 'Core'` sets `UseXamlCompilerExecutable=true`, that is, `dotnet build` always chooses the branch with the Windows `.exe`.
5. With `-p:UseXamlCompilerExecutable=false` MSBuild takes the in-process branch, but the task goes out of process (the AppDomain mechanism is only for .NET Framework), and the build fails with `MSB4061: ... "Type must be a type provided by the runtime"`. Additionally `Microsoft.Windows.SDK.BuildTools` is needed — it has Windows PEs (`signtool.exe`, `mt.exe`, `mdmerge.exe`, `winmdidl.exe`).

Result: the Windows target does not build on Linux, and this cannot be bypassed with one flag.

## Wine on the VPS: what it gives and what it does not

Wine 9.0 is installed, the prefix `/root/.wine-igp` is created. Analyzed in detail, 2026-10-27.

**Wine does not give debugging.** WinUI 3 requires DirectWrite, DXGI, DirectComposition and the Windows App SDK runtime — Wine does not provide them. The app will not start under Wine: no breakpoints, no Hot Reload, no UI check. This limit cannot be bypassed.

**The build — yes, checked fully.** Wine removes the first barrier (a Windows PE runs, "Exec format error" disappears) and allows going through the whole Windows target: `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-windows10.0.19041.0` on the VPS gives **Build succeeded, 0 warnings, 0 errors** and the output `ImageGen.Maui.exe` (PE32+ GUI x86-64). The barriers and workarounds worked through (checked 2026-10-27…28):

1. **.NET Framework is needed in the prefix.** `XamlCompiler.exe` is a .NET Framework 4.7.2 app: `err:mscoree:CLRRuntimeInfo_GetRuntimeHost: Wine Mono is not installed`. Fixed by `winetricks dotnet48`, but installing it needs `wine32:i386` (`dpkg --add-architecture i386` + `apt-get install wine32:i386`).
2. **The prefix must be filled with 32-bit DLLs.** `wineboot` ran before `wine32` was installed, so `syswow64` had no built-in 32-bit DLLs, and 32-bit apps crashed with "Library shlwapi.dll not found". Fixed by copying the missing builtins: `cp -n /usr/lib/i386-linux-gnu/wine/i386-windows/*.dll $WINEPREFIX/drive_c/windows/syswow64/`.
3. **MSXML6 is needed.** `makepri.exe` parses PRI through MSXML: `fixme:msxml:SAXErrorHandler_fatalError ... "Not implemented."` → `PRI175: 0x80004001`. The msxml6 built into Wine is not enough; the native `winetricks msxml6` helps.
4. **shcore!PathIsNetworkPathW is needed.** `makepri.exe` imports `SHCORE.dll` by ordinal 170 (this is `PathIsNetworkPathW`), and Wine implements it as a stub that ends the process: `wine: Call from ... to unimplemented function shcore.dll.PathIsNetworkPathW, aborting`. Workaround: copy the built-in `shcore.dll` into the prefix and **patch the copy** — replace the beginning of the function with `xor eax,eax; ret` (returns FALSE). Important: the `/usr/lib/.../wine/*-windows/shcore.dll` itself must not be patched — Wine stops loading it ("Library SHCORE.dll not found"). The workaround works with `WINEDLLOVERRIDES=shcore=n` (exactly `n`: with `n,b` Wine takes the `SHCreateStreamOnFileW` stub from the built-in DLL and makepri crashes on it).
5. **Case and paths.** The SDK tools (59 `.exe` files in `microsoft.windows.sdk.buildtools`) are run through `wine` wrappers; in the arguments absolute POSIX paths are converted to `Z:\...` (Wine maps `Z:` to `/`), otherwise Windows tools treat `/foo` as a switch and complain "Unexpected/Unknown option".
6. **MSBuild generates POSIX paths**, while the XAML compiler and `makepri` expect Windows paths: an `ItemSpec` like `Platforms/Windows/App.xaml` collapses into `App.xbf` (backslashes are needed), and absolute paths in `input.json`, `output.json` and `*.resfiles` lead to a doubled path (`Z:\root\proj\root\proj\obj\...`). Fixed by editing these JSON files/lists before running the tools.

**Separately — case in the Microsoft packages (a workaround with symlinks in the NuGet cache):**
- `microsoft.windowsappsdk.winui`: the targets file looks for `tools\Msix\...`, and the package puts `tools/MSIX/` → on Linux the glob does not match, 0 items. A symlink `tools/Msix -> tools/MSIX` is needed.
- There too the glob is `*.Msix`, and the files are named `*.msix` → a symlink is needed for each file.
- `microsoft.windowsappsdk.runtime`: the PRI maps reference `NoiseAsset_256X256_PNG.png`, and in the MSIX the file is named `NoiseAsset_256x256_PNG.png` → a symlink with another case is needed.

**Result:** the Windows target builds on the Linux VPS, but the price is Wine with .NET Framework, MSXML6, a patched copy of shcore, wrappers for 59 SDK tools and three symlinks in the NuGet cache. The path works but is fragile and unofficial: it breaks when the WinAppSDK packages are updated and is **slow** — every `makepri` under Wine runs for minutes, the whole PRI deployment step takes tens of minutes. The working script: `setup-win-target-wine.sh` on the machine.

**Practical conclusion.** CI already builds the Windows target (`.github/workflows/build.yml`, the `maui` job on `windows-latest`) — for free, correctly and without fragility. Debugging in any case needs a real Windows machine (the "Windows development machine" section of this file), which can be driven remotely from the VPS (SSH/WinRM/PowerShell remoting). So Wine on the VPS is a way to catch compilation errors of the Windows target locally, but not a way to debug, and CI is better for compilation.

## Debugging Android: a real device over the network

A working path that does not need KVM: a real phone reachable from the VPS over the network.

```bash
export ANDROID_HOME=$HOME/android-sdk
export PATH=$ANDROID_HOME/platform-tools:$PATH
adb connect <phone ip>:5555     # on Android 11+ — "Wireless debugging", otherwise adb tcpip 5555
adb devices
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android -t:Run
```

The debugger and Hot Reload work over this channel. The UI can be watched through `scrcpy` (over `adb`).

Checked on this machine: `adb` 1.0.41 / 37.0.1 is installed, the server starts on `127.0.0.1:5037`. **Deploying to a real device itself was not checked** — there was no device at hand.

## Launching the Avalonia window on the VPS (a virtual display)

The machine has no screen, but the head's window launches: `Xvfb` and the `DISPLAY` variable are needed.

```bash
apt-get install -y xvfb
Xvfb :99 -screen 0 1280x900x24 &
export DISPLAY=:99
export IMAGEGEN_DATA_DIR=~/igp-avalonia-data     # a second profile, the data folder
dotnet run --project ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj -c Release
```

Checked 2026-10-29: the app starts, creates the DB (`imagegen.db`, schema version 8 — the migrations are applied), writes the log to `$IMAGEGEN_DATA_DIR/logs/imagegen-yyyyMMdd.log` with a first line with the version, head, OS, data and log folders, goes to the sessions screen and the key screen. Without `IMAGEGEN_DATA_DIR` the default data folder is used (`~/.local/share/ImageGen`, see `docs/knowledge/dev-environment.md`, section "Logs").

A caveat: this is a launch, not a manual UI check — the screen is not in front of your eyes. You can look at the UI with a snapshot (for example, `import`/`xwd` from `x11-apps`), but you still cannot click the window. Task 046 (the manual check of Avalonia on a Linux desktop) stays with a human on the VPS.

## How to install (when the machine is recreated)

```bash
# .NET 10 SDK
curl -sSL https://dot.net/v1/dotnet-install.sh -o ~/dotnet-install.sh
bash ~/dotnet-install.sh --channel 10.0 --install-dir /usr/share/dotnet
ln -sf /usr/share/dotnet/dotnet /usr/local/bin/dotnet
export DOTNET_ROOT=/usr/share/dotnet PATH=$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH

# JDK 21
apt-get install -y openjdk-21-jdk-headless
export JAVA_HOME=/usr/lib/jvm/java-21-openjdk-amd64

# A virtual display for launching the Avalonia window (a machine without a screen)
apt-get install -y xvfb

# Android SDK
mkdir -p ~/android-sdk/cmdline-tools && cd ~/android-sdk
curl -sSL -o clt.zip https://dl.google.com/android/repository/commandlinetools-linux-11076708_latest.zip
python3 -c "import zipfile;zipfile.ZipFile('clt.zip').extractall('cmdline-tools-tmp')"
mv cmdline-tools-tmp/cmdline-tools cmdline-tools/latest && chmod +x cmdline-tools/latest/bin/*
export ANDROID_HOME="$HOME/android-sdk"
yes | cmdline-tools/latest/bin/sdkmanager --licenses
cmdline-tools/latest/bin/sdkmanager "platform-tools" "platforms;android-36" "build-tools;36.0.0" "emulator"

# The MAUI workload for Android
dotnet workload install maui-android --skip-manifest-update

# GitHub CLI and login
apt-get install -y gh          # or the official repository cli.github.com
gh auth login --web            # device flow: a code on the screen, entered at https://github.com/login/device
gh auth setup-git

# Locale: a UTF-8 locale for the harness checks
export LC_ALL=en_US.UTF-8 LANG=en_US.UTF-8
```

## Environment quirks
- **The "Environment" rule in tasks.** The VPS is a Linux environment, so tasks 001–011 (`tasks/v0.1/`) are done here. Tasks 012–016 and the Windows part of 034 — only on the Windows machine (the "Windows development machine" section of this file). The Android part of 034 builds but is not debugged (no emulator).
- **The `gh` token and the SSH key.** An SSH key is generated (`~/.ssh/id_ed25519_github`, fingerprint `SHA256:7ESBH3Cxonxswz7PDptNqSS8A2rPyeSgqeVU7u+W4hw`), but not uploaded to GitHub: the token lacks the `admin:public_key` scope. Uploading the key requires `gh auth refresh -h github.com -s admin:public_key`. Git meanwhile works over HTTPS with the token.
- **Docker Hub answers** (unlike the cloud session, where there were 429s). So images for task 033 can be pulled.
- **The environment is permanent.** Unlike the cloud session, the state survives an agent restart: there is no need to install the SDK again. `~/.bashrc` sets `DOTNET_ROOT`, `ANDROID_HOME`, `JAVA_HOME` and the locale.
