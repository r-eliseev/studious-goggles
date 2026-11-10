# 0026. Avalonia on Android — a second app next to MAUI

Status: accepted
Allowed statuses: proposed, accepted, superseded by NNNN, rejected
Date: 2026-11-06

Supersedes ADR 0019 (option A "two heads" stays, the Avalonia Android head is added to it).

## Context
ADR 0019 recommended keeping MAUI on phones until Avalonia is checked on a real Android device. The probe of task 094 (`poc/backlog/094-avalonia-mobile-research/`) built an Avalonia 12.1.4 Android head with the real screens; the APK is 6.65 MiB larger than MAUI and was not launched on a device. On v0.1.7 the owner's Android device does not react to tapping the picture and "…" in the MAUI card, the cause was not found. The owner on 2026-11-06: "plan Android on Avalonia, with builds"; chosen: a second app next to MAUI and publishing in every release.

## Decision
1. A new project `ImageGen/ImageGen.Avalonia.Android` (`net10.0-android`, packages `Avalonia.Android` 12.1.4, `Xamarin.AndroidX.Core.SplashScreen`). `ApplicationId` is `com.eliseev.imagegen.avalonia`, the name "Omnilite Avalonia". It installs next to the MAUI app (`com.eliseev.imagegen`), each has its own data and keys; there is no transfer between them.
2. Shared Avalonia code stays in `ImageGen.Avalonia` and is included in the Android project by file links (`Compile`/`AvaloniaXaml` with `Link`, as in probe 094). The `AssemblyName` of the Android project is `ImageGen.Avalonia`, so that the `avares://ImageGen.Avalonia/...` addresses match. Desktop files (the window, Program, DBus/DPAPI/Keychain, window dialogs) are not included.
3. The container build is split: a shared part (core, storage, Presentation, routes, page models) and registration of platform services by the head. Services that need only a `TopLevel` (clipboard, file picker, saving a picture, opening a folder) take a `Func<TopLevel?>` and are used by both heads.
4. Dialogs, the picture viewer and the comparison on Android are shown over the page (an overlay) inside the single `TopLevel`. These implementations are shared Avalonia code without Android APIs in `ImageGen.Avalonia`, checked by headless tests on Linux.
5. Android-only code: `MainApplication`, `MainActivity`, `MobileApp`, `MainView` (page, overlay, system Back, keyboard), the data folder (`Context.FilesDir`), the key store on Android KeyStore (AES-256-GCM, files per `SecretFileNames`), sending logs through an `Intent` and a `FileProvider`.
6. CI builds the Android head in `build.yml`; `release.yml` signs the APK with the same permanent key as MAUI (the `ANDROID_*` secrets) and attaches `ImageGen-Avalonia-<version>-android.apk` to the release.
7. The decision on replacing MAUI on Android (option C of ADR 0019) is a separate ADR after a manual check of both builds on a device.

## Alternatives
- **Replace MAUI on Android right away (option C of ADR 0019).** Rejected by the owner: compare on a device first; data and keys would have to be moved between different secret stores.
- **A separate `ImageGen.Avalonia.Shared` library instead of file links.** Cleaner, but moving the files changes resource addresses, tests and the desktop project; not justified for a second head at the comparison stage. Revisit if the Android head stays.
- **Only a CI build, no release.** Rejected by the owner: the APK is needed for checking on a phone.

## Consequences
- A new file in `Views/` or the shared services automatically gets into the Android build; if it uses a desktop API, the Android CI build fails — a signal to move the dependency behind an interface.
- Screen markup is shared for Linux/Windows/macOS and Android; the MAUI markup is still maintained separately.
- The release contains two APKs. Both are signed with one key, but they are different apps: an update of each installs only over itself.
- Unknown until a device check: launch, KeyStore after a restart, the keyboard and Send, system Back, TalkBack, memory and cold start (a manual task).
