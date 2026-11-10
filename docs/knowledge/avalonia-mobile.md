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

### Avalonia application icon

The Avalonia Android and desktop icons are rasterized from the MAUI source files `ImageGen/ImageGen.Maui/Resources/AppIcon/appicon.svg` (background) and `appiconfg.svg` (foreground). On Debian or Ubuntu, install the environment tools with `sudo apt install librsvg2-bin icoutils`. Keep generated and intermediate files in the repository's ignored `tmp/` directory. Compose the foreground SVG children over the background with Python's `xml.etree.ElementTree`, then rasterize the composite with `rsvg-convert`; do not rasterize the two source files independently because the foreground is transparent. For example, write the combined SVG to `tmp/omnilite-icon/appicon.svg`, then run:

```bash
mkdir -p tmp/omnilite-icon
python3 - <<'PY'
from xml.etree import ElementTree as ET

ns = "http://www.w3.org/2000/svg"
ET.register_namespace("", ns)
base = ET.parse("ImageGen/ImageGen.Maui/Resources/AppIcon/appicon.svg").getroot()
foreground = ET.parse("ImageGen/ImageGen.Maui/Resources/AppIcon/appiconfg.svg").getroot()
for element in foreground:
    base.append(element)
ET.ElementTree(base).write("tmp/omnilite-icon/appicon.svg", encoding="UTF-8", xml_declaration=True)
PY
rsvg-convert -w 256 -h 256 tmp/omnilite-icon/appicon.svg -o ImageGen/ImageGen.Avalonia/Assets/appicon.png
for spec in mdpi:48 hdpi:72 xhdpi:96 xxhdpi:144 xxxhdpi:192; do
  density=${spec%%:*}
  size=${spec##*:}
  mkdir -p "ImageGen/ImageGen.Avalonia.Android/Resources/mipmap-$density"
  rsvg-convert -w "$size" -h "$size" tmp/omnilite-icon/appicon.svg -o "ImageGen/ImageGen.Avalonia.Android/Resources/mipmap-$density/appicon.png"
done
for size in 16 32 48 256; do
  rsvg-convert -w "$size" -h "$size" tmp/omnilite-icon/appicon.svg -o "tmp/omnilite-icon/icon-$size.png"
done
icotool -c -o ImageGen/ImageGen.Avalonia/Assets/appicon.ico tmp/omnilite-icon/icon-16.png tmp/omnilite-icon/icon-32.png tmp/omnilite-icon/icon-48.png tmp/omnilite-icon/icon-256.png
```

Generate the Android `appicon_round.png` files at the same densities from this composite SVG, wrapped in an SVG group clipped to a circle centered at (228, 228) with radius 228 in its 456×456 viewBox. These round icons have transparent corners. The checked-in raster images keep builds independent of `rsvg-convert` or `icotool`.

## iOS

`ImageGen/ImageGen.Avalonia.iOS` is the iOS head for the simulator on `net10.0-ios`, minimum iOS 15. The entry point uses `UIApplication.Main` and `AvaloniaAppDelegate<MobileApp>`; `MobileApp` creates `MainView` as a single-view lifetime and uses the shared UI from `ImageGen.Avalonia`.

Data is stored in the app's private folder `LocalApplicationData/ImageGen`, keys in Keychain with availability after the first device unlock. Saving an image uses the Avalonia system dialog, logs are sent through the system share sheet. Opening folders and playing video are not supported on iOS. The build, formatting and launch in the simulator are done by the `apple-ci.yml` workflow; distribution of the iOS app is not set up.
