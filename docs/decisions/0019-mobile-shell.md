# 0019. The mobile head

Status: superseded by 0026
Allowed statuses: proposed, accepted, superseded by NNNN, rejected
Date: 2026-10-30

Superseded by [0026](0026-avalonia-android-second-app.md): the owner chose option A with the Avalonia Android head as a second app.

If option B or C were accepted, it would supersede ADR 0006 in the MAUI part.

## Context

Task 094 checked the Avalonia 12.1.4 Android head on Linux, plugging in the app's real screens and models. A Release APK for `android-arm64` builds and is 6 968 679 bytes (6.65 MiB) larger than the MAUI APK — less than the allowed 10 MiB. There was no device, so launching, Android KeyStore after a restart, the Send key, TalkBack, navigation, the gallery, memory and performance were not checked. The iOS head was only studied from the template and the official documentation; building it needs a Mac with Xcode. Details: `docs/knowledge/avalonia-mobile.md`.

Mandatory criteria: the head builds; it launches; a test key survives a restart; the Send key sends a message; the APK is no larger than MAUI + 10 MiB. The build and the size are confirmed. Launch, key and keyboard are not checked.

## Decision

Option A is recommended: keep two heads (MAUI and Avalonia) for now, then revisit the decision after manual Android checks on a device. Before moving to B or C, first check the Avalonia user scenarios on a real Android device, and for B additionally build and check iOS on a Mac.

## Alternatives

- **A — two heads, MAUI and Avalonia.** We gain: we keep the working MAUI mobile and Windows scenarios without deciding on the APK build without runtime data. We lose: we maintain screen markup in both heads. The migration volume is zero now; mobile screens and services stay in MAUI, the Avalonia Android probe stays a research one.
- **B — Avalonia everywhere.** We gain: one markup for Sessions, API key, Chat, Model Picker, Session Models, Session Parameters and Settings on Linux/Windows/macOS/Android/iOS. We lose: we cannot rely on the current manual MAUI checks; Avalonia checks are needed on all mobile platforms. Migration volume: replace the MAUI Android/iOS/Windows heads, finish system Back, the keyboard, dialogs, clipboard and picker; implement and check Android KeyStore, MediaStore, Windows DPAPI/folders, iOS Keychain/Photos, Mac Catalyst and the lifecycle.
- **C — Avalonia on the desktop and Android, MAUI only on iOS.** We gain: we remove the MAUI Android markup while keeping the iOS head until Avalonia iOS is checked. We lose: two mobile UI stacks and double iOS markup remain. Migration volume: replace the MAUI Windows/Android heads with Avalonia, finish/check the Android services and keep MAUI iOS; the shared screen layer stays in Presentation.

## Consequences

Until the manual Android and iOS checks, the recommendation does not change the accepted decision on two heads. The build result lowers the risk for Android packaging and size, but does not confirm work on a device. The unknown checks are listed in `docs/knowledge/avalonia-mobile.md` and handed to manual task 140.
