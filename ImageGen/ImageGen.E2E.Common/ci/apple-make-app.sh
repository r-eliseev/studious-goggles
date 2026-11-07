#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 4 ]; then
  echo "usage: $0 <publish-dir> <app-path> <bundle-id> <version>" >&2
  exit 2
fi

publish_dir=$1
app_path=$2
bundle_id=$3
version=$4
app_name=ImageGen.Avalonia
executable="$publish_dir/$app_name"

if [ ! -d "$publish_dir" ] || [ ! -f "$executable" ]; then
  echo "publish directory must contain apphost '$app_name'" >&2
  exit 1
fi

mkdir -p "$app_path/Contents/MacOS" "$app_path/Contents/Resources"
cp -R "$publish_dir"/. "$app_path/Contents/MacOS/"
chmod +x "$app_path/Contents/MacOS/$app_name"

icon=$(find ImageGen/ImageGen.Avalonia/Assets -maxdepth 1 -type f -name '*.icns' -print -quit 2>/dev/null || true)
if [ -n "$icon" ]; then
  cp "$icon" "$app_path/Contents/Resources/$(basename "$icon")"
fi

cat >"$app_path/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleIdentifier</key><string>$bundle_id</string>
  <key>CFBundleName</key><string>Omnilite</string>
  <key>CFBundleExecutable</key><string>$app_name</string>
  <key>CFBundleShortVersionString</key><string>$version</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>LSMinimumSystemVersion</key><string>12.0</string>
  <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
PLIST
