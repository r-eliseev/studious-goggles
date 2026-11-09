# 0027. Video and music generation

Status: accepted
Allowed statuses: proposed, accepted, superseded by NNNN, rejected
Date: 2026-11-06

Item 7 for Avalonia is refined by ADR [0028](0028-avalonia-media-playback.md): desktop — LibVLC in a separate window, Android — the built-in Android player (Media3).

## Context
The owner's decision of 2026-10-31: generation of music, sounds and video is for later versions (tasks 166–169). Research 166 is `docs/knowledge/media-generation.md`. The core, storage and UI are designed only for pictures: `Generation` without a result kind, `IQueuedImageProvider`, `IImageFileStore` (png/jpeg/webp, 50 MiB in memory), the `generate_images` tool, a card with a picture. The owner chose on 2026-11-06: everything through OpenRouter; in Avalonia — LibVLC right away.

## Decision
1. **The provider is OpenRouter.** Video — `POST/GET /api/v1/videos` and the `/api/v1/videos/models` catalog. Music — Lyria 3 (`google/lyria-3-clip-preview`, `google/lyria-3-pro-preview`) through `chat/completions` with an audio stream, after a live check of the format (208). OpenRouter has no sound effects — task 168 waits for a model.
2. **The result kind in the domain.** `MediaKind { Image, Video, Audio }` and `Generation.Kind` (`Image` by default, a migration with `DEFAULT 0`), `Generation.DurationSeconds`. The file path is the old `ImagePath` field (renaming is postponed: it would touch all code for the sake of a name).
3. **Files.** Video and audio are written to the store as a stream (`SaveMediaStreamAsync`), not whole in memory, atomically (a temporary file + rename, as for pictures) and right after they are ready. Types: `video/mp4`, `video/webm`, `audio/mpeg`, `audio/wav`, `audio/ogg`, `audio/flac`. Limits: video 500 MiB, audio 100 MiB (`ProviderResponseLimits`); larger is an error, the partial file is deleted.
4. **Video uses the same job queue** as pictures: `JobJson` for recovery after a restart, polling with an increasing interval, statuses are mapped to the common model. OpenRouter has no cancellation: "Cancel" stops waiting and sets `Cancelled`, the card warns that the provider may charge.
5. **The video model catalog** is a separate `VideoModel` type and its own cache (the `video_models` table, the model's JSON). The price before the run is computed from `pricing_skus` for known keys (`duration_seconds*`, `cents_per_*second*`, `minimum_cents_per_generation`); `video_tokens*` and others are "price unknown".
6. **Session and assistant.** `SessionSettings.VideoModels` and `VideoParameters` (duration, resolution, aspect ratio, sound), stored in `settings_json`. A separate assistant tool `generate_video` (prompt; the first frame — by the id of a finished picture of the session). The assistant starts it without confirmation, as for pictures (S2.2); spending warnings (021) come before the message is sent.
7. **Playback.** MAUI — `CommunityToolkit.Maui.MediaElement` 10.0.0 (MIT) in the card. Avalonia — LibVLC (`LibVLCSharp` + `LibVLCSharp.Avalonia`, LGPL-2.1+), first a probe on Avalonia 12 (206); if `VideoView` is incompatible — frame output into a `WriteableBitmap` through LibVLC callbacks, decided after 206. The native libvlc comes from the VideoLAN packages for Windows, macOS, Android; on Linux the system one (the dependency is described in the README).
8. **Spending.** `ExpenseKind.Video` and `ExpenseKind.Music` in the 021 ledger.

## Alternatives
- **Video through OpenRouter, audio through fal** — more audio models and sound effects, but every fal model has its own field schema; rejected by the owner.
- **Everything through fal** — one provider with a queue, but the model schemas differ and prices come only with a key; rejected.
- **The system player in Avalonia** instead of LibVLC — no new packages, but no player in the card; rejected by the owner.
- **Generalize the `Image*` types by renaming** (`ImageModel` → `MediaModel` etc.) — cleaner, but a huge edit without a behavior change; postponed.

## Consequences
- Pictures work as before: `Kind = Image` by default for old records.
- The gallery (022) still shows only pictures (the `Kind = Image` filter) until a separate decision.
- Avalonia packages grow by the size of libvlc (tens of MB per platform); LGPL requires stating the license and does not forbid distribution with a dynamic library.
- Unknown until live requests: the video response fields, the `usage.cost` unit, the file format, the Lyria format and parameters.
