# Specification: video and audio generation

Status: in progress
Code: `ImageGen/ImageGen.Core/` and `ImageGen/ImageGen.Data/`
Decisions: ADR 0027; API research — `docs/knowledge/media-generation.md`.

## Goal

Generate video through OpenRouter and save the result locally without loading large files into memory whole. Model, parameters, status, actual price and result belong to the app's ordinary runs and generations.

## Requirements

- `Generation.Kind` sets Image, Video or Audio. Existing rows and runs keep Image by default.
- The OpenRouter catalog contains only models with a supported duration. Parameters and `pricing_skus` are cached locally. The price is estimated only for known units; for video-token SKUs the result is unknown.
- A video run creates one generation per selected model. `Run` stores the shared video settings and the id of the optional first frame.
- A session stores the list of video models and parameters; an old `settings_json` without these fields is read with an empty list and default parameters. On save, the model's presence in the catalog and whether the chosen duration is allowed for each model are checked.
- `generate_video` is available to the assistant only when video models are selected. Explicit requests for a video, a clip or an animation go to this tool; pictures keep using `generate_images`. The cost estimate sums the known prices of the selected video models, lists unknown prices separately and does not mark the estimate as approximate.
- The session models screen allows choosing several video models; the price shows an estimate for 5 seconds or "price unknown", and models with a sound track are marked. Video parameters are limited to the intersection of the durations, resolutions and aspect ratios of the selected models; a missing value means auto. The sound generation switch is available if at least one selected model supports it.
- The next run estimate in the chat sums the known prices of pictures and videos and separately shows the number of unknown ones. A video card shows the media type, status, model, duration, price and actions; MAUI plays the finished file through MediaElement with system controls, Avalonia through the player of task 207. For video and audio the picture viewer, Describe, Compare and Run on model are not shown.
- The first frame can be taken only from a successful picture of the same session. The seed is sent only if the catalog reports support.
- A video is sent as an asynchronous OpenRouter job. The id and the polling address are saved before waiting for the result, so that polling continues after a restart without a repeated generation.
- Provider statuses are mapped to the common model. Polling starts right after sending, then uses an increasing interval from 5 to 30 seconds. A job error keeps the provider's code and text.
- After success the video is opened as a stream and saved through `IImageFileStore.SaveMediaStreamAsync` with the `ProviderResponseLimits.Video` limit. The DB records the path, MIME type, price and finish time before the update event is published.
- A download or save error moves the generation to Failed; the path of a partially written file is not saved. An error in the final DB write deletes the file just created.
- OpenRouter provides no cancellation for video. The local watcher can be stopped, but this does not confirm that the paid operation stopped on the provider's side.
- After sending, cancellation stops the local watcher, saves `Cancelled` and returns `StoppedWaiting`; the UI warns that the provider may still charge. Before sending, cancellation returns `Cancelled`. Repeat and Edit keep the model, seed, parameters and first frame of the source run; New seed keeps the other settings and gets a new seed. Run on model is not supported for video.
- The supported streaming MIME types and file limits are set in ADR 0027 and `ProviderResponseLimits`; pictures keep their old execution path and the gallery keeps showing only pictures.
- The OpenRouter audio catalog loads every model with an `audio` or `speech` output from `GET /models?output_modalities=all` and caches it locally. A model advertising both is treated as chat audio. Chat audio uses streaming `chat/completions` and decodes base64 audio chunks; speech uses streaming `audio/speech` response bytes. The shared response exposes the audio stream, detected media type, transcript and provider-reported cost. Chat audio retries once with WAV when a 400 response mentions an unsupported format or voice. Speech requests use MP3 and include `voice` only when supplied; their cost is estimated separately by the caller from the catalog's price per input character.
- An audio run creates one generation per selected audio model. Sessions store their selected audio models and shared optional voice; each run stores the voice used. `generate_audio` is available only when audio models are selected. Its required `prompt` describes the sound; optional `speech_text` supplies exact speech text and becomes the generation input when present. Audio files are saved as streams with the audio response limit and their returned media type and cost. Audio generations recover as interrupted after restart, can be cancelled locally with `StoppedWaiting`, and support Repeat and Edit with the original voice. New seed and Run on model are not supported. Speech estimates use price per input character and the exact text length; chat-audio prices remain unknown.

## Open checks

The shape of the real response and a live paid video job were not checked with a key. A manual live check is done separately from the automated tests.
