# Avalonia mobile

## Android

`ImageGen/ImageGen.Avalonia.Android` is the Android head for the shared `ImageGen.Avalonia` pages and the `ImageGen.Presentation` models. The target platform is `net10.0-android` (SDK API 36), the minimum Android version is API 23. Building on Linux needs the Android SDK and the .NET `android` workload:

```bash
dotnet workload install android
dotnet build ImageGen/ImageGen.Avalonia.Android/ImageGen.Avalonia.Android.csproj -c Debug
dotnet build ImageGen/ImageGen.Avalonia.Android/ImageGen.Avalonia.Android.csproj -c Release
```

The package id is `com.eliseev.imagegen.avalonia`, the name "Omnilite Avalonia". Navigation is shown by `Views/MainView`: `PageHost` for pages and `OverlayHost` for dialogs, viewing and comparison. The Android composition calls `AppServices.CreateShared` and registers the overlay services; the Avalonia pages and resources are included as sources from the desktop project, the separate desktop head and the fullscreen controller are not included.

Data is stored in `<FilesDir>/ImageGen`. Secrets are encrypted with AES-256-GCM using an Android Keystore key; the IV is stored before the ciphertext, writing goes to a temporary file with an atomic rename. The log folder is `<FilesDir>/ImageGen/logs`; the format, retention and levels are shared with the other heads. A manual check of installation and navigation on a device is described by task 198 and has not been done yet.

The VPS check of 2026-11-06: the Debug build and the Release build passed without warnings and errors. A signed Release APK of 60,556,659 bytes (about 57.7 MiB) was built with a local Android debug certificate; it is not meant for updating or publishing over the official release. The build used API 36, min API 23. Launching on a device was not checked.

## iOS

`ImageGen/ImageGen.Avalonia.iOS` is the iOS head for the simulator on `net10.0-ios`, minimum iOS 15. The entry point uses `UIApplication.Main` and `AvaloniaAppDelegate<MobileApp>`; `MobileApp` creates `MainView` as a single-view lifetime and uses the shared UI from `ImageGen.Avalonia`.

Data is stored in the app's private folder `LocalApplicationData/ImageGen`, keys in Keychain with availability after the first device unlock. Saving an image uses the Avalonia system dialog, logs are sent through the system share sheet. Opening folders and playing video are not supported on iOS. The build, formatting and launch in the simulator are done by the `apple-ci.yml` workflow; distribution of the iOS app is not set up.
