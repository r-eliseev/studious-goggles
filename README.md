# ImageGen

A client for generating images from one prompt in several models. Results and session history are stored locally. The source is written in C# / .NET 10; MAUI, Avalonia and Web (with a Vue UI) heads are available.

## Currently in the source code

- Built-in image providers: OpenRouter, fal.ai and Together AI. Additional OpenAI-compatible providers can be defined in the config.
- An OpenRouter key is needed for the assistant and is requested on first launch. fal.ai and Together AI keys are optional; they can be added in Settings. Together is available only for images and is not offered as an assistant model.
- The Together catalog contains FLUX.1.1 Pro, FLUX.2 Dev, FLUX.2 Flex and FLUX.2 Pro. Live generation and actual Together billing have not been verified yet; the actual cost remains unknown.
- fal.ai is built in as a generation provider, but configuring it is not a required first-launch step.

These features describe the current branch of the source code; a release is published automatically after every change in `main`.

## Download

The latest release is on the [Releases](https://github.com/r-eliseev/studious-goggles/releases/latest) page; a new one is published automatically after every change in `main` (the next patch number). Package names (`<version>` is, for example, `v0.1.13`):

| Head | OS / architecture | Package | Format | Launch check |
|---|---|---|---|---|
| Avalonia | Linux x64 / arm64 | `ImageGen-Avalonia-<version>-linux-x64.tar.gz`, `…-linux-arm64.tar.gz` | `.tar.gz` | Built by CI; launch on a target device is manual task 046 |
| Avalonia | Windows x64 / arm64 | `ImageGen-Avalonia-<version>-win-x64.zip`, `…-win-arm64.zip` | `.zip` | Built by CI; manual launch — 046 |
| Avalonia | macOS x64 / arm64 | `ImageGen-Avalonia-<version>-osx-x64.tar.gz`, `…-osx-arm64.tar.gz` | `.tar.gz` | Built by CI; launch on a Mac not verified |
| Avalonia | Android | `ImageGen-Avalonia-<version>-android.apk` | `.apk` | Installs next to MAUI; its own data and keys; device check — 198 |
| MAUI | Android | `ImageGen-Maui-<version>-android.apk` | `.apk` | Signed with a permanent key; device check — 140 |
| MAUI | Windows x64 / arm64 | `ImageGen-Maui-<version>-win-x64.zip`, `…-win-arm64.zip` | `.zip` | PE architecture is checked in the release workflow; manual launch — 016 |

The release workflow publishes Avalonia self-contained archives, the Windows MAUI unpackaged ZIP and Android APKs for MAUI and Avalonia. The Avalonia Android APK installs next to MAUI and uses its own application ID, data folder and provider keys. The Android APK is meant for manual installation; there is no store publishing, MSIX or auto-update. APKs up to and including v0.1.5 were signed with different debug keys: an update does not install over them and requires uninstalling the app together with its data. Starting with the next release the Android package is `com.eliseev.imagegen`: it installs as a separate app (uninstall the old ImageGen by hand and enter the provider keys again). Since the first release after v0.1.5 the APK is signed with a permanent key (certificate `CN=ImageGen, O=eliseev`, SHA-256 `6711f8ba…10277`): moving to it from v0.1.5 or earlier requires uninstalling the app once; after that updates install over it. MAUI for iOS and Mac Catalyst is not released.

Building the packages in CI confirms the build and, where the workflow checks PE, the architecture. By itself it does not confirm that the app works on a device. Current manual platform checks are listed in [`docs/knowledge/dev-environment.md`](docs/knowledge/dev-environment.md) and tasks [`016`](tasks/v0.1/016-skeleton-manual-check.md), [`046`](tasks/backlog/046-desktop-manual-check.md) and [`140`](tasks/backlog/140-android-manual-check.md).

## First launch and keys

The assistant uses the selected chat provider; on first launch you can save the key of another configured chat provider and choose its model. fal.ai and Together AI keys for image generation are configured later in Settings. If you choose a provider's model without its key, the run ends with an ordinary key error. Together's capabilities and limits are described in [`docs/knowledge/providers.md`](docs/knowledge/providers.md).

## Data and privacy

- Sessions, the SQLite database and image files are stored locally in the app's data folder. The app does not encrypt the whole database or all image files.
- API keys are stored in the head's system secret store. In Avalonia, if the system store is unavailable, the key stays only in memory until the app is closed.
- Android is excluded from automatic backup; deny-all rules are also configured for cloud backup and device transfer. The behavior of OEM transfer tools on a real device has not been verified.
- Debug logs and the separately enabled debug trace may contain user prompts and provider data. Do not publish them without checking and cleaning. More: [`debug trace`](docs/specs/debug-trace.md).

## Development

You need the .NET 10 SDK. On Linux the MAUI Windows target and the whole solution do not build; details on environments and platforms are in [`docs/knowledge/dev-environment.md`](docs/knowledge/dev-environment.md).

The Web client also needs Node.js 22.12+ and npm. From `ImageGen/ImageGen.Web/ClientApp` run `npm ci`, `npm run typecheck`, `npm run lint`, `npm test` and `npm run build`. The build writes the Vue assets to `ImageGen/ImageGen.Web/wwwroot`. For development set `IMAGEGEN_WEB_PASSWORD` and `IMAGEGEN_DATA_DIR`, run `ASPNETCORE_URLS=http://localhost:5080 dotnet run --project ImageGen/ImageGen.Web/ImageGen.Web.csproj`, and in another console `npm run dev` from `ClientApp`; Vite proxies `/api` to the Web host.

Main build and tests of individual projects:

```sh
dotnet build ImageGen/ImageGen.Core/ImageGen.Core.csproj
dotnet build ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
```

### Running the Web host in Docker

With Docker Compose installed, set `IMAGEGEN_WEB_PASSWORD` and run `docker compose up -d --build`. The service is available at `http://127.0.0.1:8080`; data is kept in the named volume `imagegen-data`. Instructions for backups, bind mounts and changing the password are in [`docs/knowledge/docker.md`](docs/knowledge/docker.md).

Running Avalonia needs a graphical session:

```sh
dotnet run --project ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj
```

## Documentation and plans

- [Architecture](docs/architecture.md), [product goals](docs/product.md) and [specification](docs/specs/image-client.md).
- [Sessions with the assistant](docs/specs/chat-sessions.md) and [debug trace](docs/specs/debug-trace.md).
- [Documentation index](docs/index.md), [project status](docs/status.md) and [archive of finished tasks](tasks/DONE.md).

The Web API and the Vue client are part of the current source code. The Web host Docker image is not yet included in the published release.

## License

[MIT](LICENSE).
