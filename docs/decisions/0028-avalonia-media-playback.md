# 0028. Video and audio playback in Avalonia

Status: accepted
Allowed statuses: proposed, accepted, superseded by NNNN, rejected
Date: 2026-11-07

Refines item 7 of ADR 0027 for Avalonia.

## Context
ADR 0027 chose LibVLC for Avalonia. Probe 206: `LibVLCSharp.Avalonia` 3.10.2 with `VideoView` builds on Avalonia 12.1.4 and plays a file on the desktop; on Android `VideoLAN.LibVLC.Android` 3.6.5 adds ~19 MB to the APK and gives XA0141 (native libraries without 16 KB alignment — they will not load on devices with 16 KB pages). The `LibVLCSharp.Avalonia` README lists only Windows, macOS, Linux (task 207 is blocked). On the owner's phone, MAUI `MediaElement` (ExoPlayer) works in a fullscreen window, but inside a scrolling feed the video is black and blinks (task 219). The owner's decision of 2026-11-07: on Android — the built-in Android player.

## Decision
1. A shared contract `IMediaPlayerService.ShowAsync(filePath, accessibleLabel, ct)` (Presentation, task 219): a video or audio card shows Play, playback goes in a separate fullscreen window/screen, not in the feed — in all heads.
2. **Avalonia desktop** (Linux, Windows, macOS): LibVLC (`LibVLCSharp` + `LibVLCSharp.Avalonia` `VideoView`) in a separate player window (modeled on the picture viewer window), not inside the feed: `VideoView` is a native window over the content and in a scrolling list it may be clipped and blink. The native libvlc is `VideoLAN.LibVLC.Windows`/`.Mac`; on Linux the system one (`libvlc5`, `vlc-plugin-base`).
3. **Avalonia Android**: a separate Android activity with `androidx.media3` (`Xamarin.AndroidX.Media3.ExoPlayer` and `.Ui` 1.11.1, MIT AND Apache-2.0; `PlayerView` with controls) for video and audio. LibVLC is not included in the Android head.

## Alternatives
- Desktop only, on Android "Open with" in an external app: no new packages, but watching happens outside the app.
- LibVLC on Android with frame output through callbacks into a `WriteableBitmap`: +19 MB, the 16 KB problem, the cost of copying frames is unknown.
- The native LibVLC `VideoView` on Android through `NativeControlHost`: +19 MB, 16 KB, problems overlaying Avalonia.
- A built-in `VideoView` in the desktop card: clipping and blinking in scrolling are possible; rejected in favor of a window.

## Consequences
- The behavior is the same in all heads: in the feed a poster/icon and Play, viewing is separate.
- The Android head grows by Media3 (on the order of a few MB; the exact size is in the 224 report), without LibVLC.
- Desktop packages grow by libvlc (Windows: ~112 MB for win-x64 per the 206 report).
