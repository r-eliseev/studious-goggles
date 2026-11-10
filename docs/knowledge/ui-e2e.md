# Knowledge about MAUI UI E2E

## OpenAI-compatible mock contract (task 243)

The route and payload shapes below are confirmed from `OpenAiCompatibleClient` and `OpenAiChatWire`:

- `GET /v1/models` with `Authorization: Bearer <key>` checks the key. HTTP 200 is valid; 401/403 are invalid; other statuses are network errors. The response body is not read. Configured chat and image models come from `providers.json`; no model-list response is needed.
- `POST /v1/chat/completions` sends `model`, `messages`, and, when present, `tools` plus `tool_choice: "auto"`. The mock response needs `choices[0].message.tool_calls[]` with `function.name` and JSON `function.arguments`; the assistant call is `generate_images` with a `prompt`.
- `POST /v1/images/generations` sends `model`, `prompt`, `n: 1` and optional image-format/seed fields. The supported response is `data[0].b64_json`; URL-only responses are not supported.

The E2E provider config is delivered after each `mobile: clearApp`, because clearing the app removes its private data. The API 33 `google_apis` emulator is already used by `ui-e2e.yml`; task 238 verified that `adb root` and `adb remount` work there. Task 243 uses a one-off root CA and a `localhost` server certificate, installs the CA into the API 33 system trust store, and connects the emulator to the host mock with `adb reverse`. The Release app's trust of this installed certificate and the private-file copy are verified by the integrated CI scenario, not by the local environment.

The Windows scenario uses a short-lived self-signed `localhost` certificate installed in the hosted runner's Local Machine Root store. The test base copies `e2e-providers.json` into the app data directory after clearing it and before launch. The mock process runs alongside the Windows UI tests and is stopped afterward; certificate files and mock logs stay in the runner's temporary/report directories.

## Preflight 238

The first two runs through `workflow_dispatch` on the branch `task/217-embedding-provider-model-name`, commit `fa3187de662ca9fa6a0e25a78466c746245fac79`: [run 42366477520](https://github.com/r-eliseev/studious-goggles/actions/runs/42366477520), [run 42366566469](https://github.com/r-eliseev/studious-goggles/actions/runs/42366566469). The Android APK and the unpackaged Windows app were built in both runs. The Windows preflight succeeded in both. The Android preflight of the second run installed Appium 3.9.0 and UiAutomator2 8.7.3, but doctor exited with code 1, because the emulator was not yet in PATH before emulator-runner. Fixed by running doctor after the emulator starts.

### Windows

Checked in runs 42366477520, 42366566469 and 42367302330. Runner `runneradmin`, an interactive `console` session ID 2 Active; resolution 1024×768. The MAUI window and the AutomationId `apikey.entry` were found. The frame is not uniform: the shares of pixels outside the mean color are 0.992126 and 0.994568 in the two saved reports (>1% in all successful runs). FFmpeg gdigrab recorded an openable 10-second MP4. Data folder: `C:\Users\runneradmin\AppData\Local\User Name\com.eliseev.imagegen\Data`.

### Android — a successful check

[Run 42367302330](https://github.com/r-eliseev/studious-goggles/actions/runs/42367302330), commit `58a2a0d32a40693808d372ac236b4010635fe753`: the APK `com.eliseev.imagegen-Signed.apk` was built, installed and launched; the screenshot shows the Connect OpenRouter screen. The `AutomationId` is found in the XML as `resource-id="com.eliseev.imagegen:id/apikey.entry"`. `adb root` and `adb remount` succeed; a screenshot and a recording were obtained, the recording is 19.842 s long. The workflow finished successfully, the Windows preflight succeeded too. The artifacts are in `preflight-android-reports` and `preflight-windows-reports` (14 days).

## The Allure report

The workflow assembles the Android and Windows Allure results into HTML and uploads `allure-report` for 14 days. Download the artifact from the Artifacts section of the needed Actions run and unpack it; to view, run `python3 -m http.server 8000 --directory <report folder>` and open `http://localhost:8000`. `file://` is not verified: there is no browser in the local environment.

## Integrated run 42374038475

The Android APK and unpackaged Windows app builds passed; the Android preflight installed and launched the APK on API 33. The Android UI test did not start because of an extra `--` in the `dotnet test` syntax with MTP. The Windows UI test really launched the window through FlaUI and ran 3 scenarios: one passed, two failed on accessibility/status checks in the tests. The fixes and the full result are below.

## Full UI E2E run 42378967997

On commit `13db79eb50bfa527997703fedad3768f623028a8` passed: the Android APK build, the Android preflight, three Android smoke tests, the Windows unpackaged build, the Windows preflight, two Windows UI smoke tests, the `VideoRecorder` test and the shared report job. The TRX shows all 3 Android tests and all 3 Windows fixture tests with the status `Passed`; the Allure HTML contains the results and attachments of both platforms.

The `android-e2e` artifact: three per-test MP4 H.264 320×640, checked with `ffprobe`; also logcat, metadata and Allure attachments. The `windows-e2e` artifact: two per-test MP4 H.264 1024×768, checked with `ffprobe`; also app/FFmpeg logs, metadata and Allure attachments. The `allure-report` artifact contains a suite summary with five passed UI tests and the passed FFmpeg arguments test. All workflow artifacts are kept for 14 days.

## How to watch the videos and screenshots of runs

An agent cannot play a video, but it can see pictures. So a video is split into frames with `ffmpeg`/`ffprobe`: in a cloud session they are in `/usr/bin`. Temporary files only in `tmp/`.

1. **Find the run and the artifacts.** GitHub MCP: `actions_list` → `list_workflow_runs` (workflow `ui-e2e.yml` or `apple-ci.yml`, branch `main`), then `list_workflow_run_artifacts`. The needed artifacts:
   - Android — `android-e2e`;
   - Windows — `windows-e2e`;
   - iOS — `ios-smoke-maui`, `ios-smoke-avalonia`;
   - macOS — `macos-e2e-smoke`;
   - the report — `allure-report`.
2. **Download.** `actions_get` → `download_workflow_run_artifact` gives a temporary link (about 10 minutes). Then `curl -sS -o tmp/runs/<name>.zip '<link>'` and `unzip -q tmp/runs/<name>.zip -d tmp/runs/<name>`. For a human it is simpler: the run page → Artifacts → download.
3. **A summary of a clip:**
   ```
   ffprobe -v error -show_entries format=duration:stream=width,height -of csv=p=0 <file>.mp4
   ```
   Then compare it with `durationMs` from `metadata.json` in the same folder.
4. **A contact sheet (one PNG per clip, 8 frames with a timestamp):**
   ```
   d=$(ffprobe -v error -show_entries format=duration -of csv=p=0 rec.mp4)
   ffmpeg -v error -y -i rec.mp4 -vf "fps=$(python3 -c "print(8/max($d,0.5))"),scale=240:-1,drawtext=text='%{pts\:hms}':x=5:y=5:fontcolor=yellow:fontsize=18:box=1:boxcolor=black,tile=8x1" -frames:v 1 sheet.png
   ```
   For Windows (1024×768) `scale=480:-1,tile=4x2` is more convenient.
5. **The end of the clip** — it holds the state the test checked:
   ```
   ffmpeg -v error -y -sseof -1.2 -i rec.mp4 -vf "fps=4,scale=320:-1,tile=5x1" -frames:v 1 last.png
   ```
6. **Zoom into an area** (for example, the status text):
   ```
   ffmpeg -ss <start> -t <length> -i rec.mp4 -vf "crop=W:H:X:Y,fps=10,tile=4x2" -frames:v 1 zoom.png
   ```
   Coordinates are in pixels of the original frame.
7. **Look at the PNG** (with the file reading tool) and write the conclusion into the task. Look at the `screenshot.png` files from the artifacts the same way.

### What the recordings show (run [42398374909](https://github.com/r-eliseev/studious-goggles/actions/runs/42398374909), 2026-11-07)

- **Android: the recording stops before the end of the test.**
  - `InvalidKey_ShowsError`: the test took 24.9 s, the clip 6.4 s, logcat 7 s. The last frame is "Checking the key...", and the error text the test checked is not in the video.
  - `Rotate_KeepsKeyScreen` ends on the session list: the key screen is not in the recording.
  - The emulator video has a variable frame rate and a low resolution (320×640). The last fractions of a second before `StopRecordingScreen` do not get into the file.
  - Conclusion: the test's final state must be captured separately — a pause before stopping the recording and a screenshot always, not only on failure. Task 251.
- **Android: `Rotate_KeepsKeyScreen` rotates the wrong screen.** The rotation runs right after launch, while the session list is open. Only then does the app go to the key screen, and `Find` finds it. The scenario "rotation does not break the key screen" is not checked this way. Task 251.
- **Android, first launch.** For about 1.6 s the session list (with examples) is visible, then the app goes to "Connect OpenRouter".
- **Windows.**
  - The whole 1024×768 desktop is recorded. Behind the app window the runner's console with its JSON logs is visible; no secrets are seen in them.
  - The error "OpenRouter rejected this key." is in the recording.
  - The MAUI key field on Windows shows the entered key in plain text (`sk-or-e2e-invalid`) for about 0.2 s and only then masks it. The key was entered by fast FlaUI typing. How the field behaves on paste is not checked. Task 251.

### UI E2E after task 251

Run [42808851378](https://github.com/r-eliseev/studious-goggles/actions/runs/42808851378) passed on both platforms. Android artifacts now include `screenshot.png` for every test. The final frames of `InvalidKey_ShowsError` show “OpenRouter rejected this key.”; `Rotate_KeepsKeyScreen` waits for the key field before rotating and its recording shows that screen in landscape. The Windows `InvalidKey_ShowsError` recording and screenshot also show the final provider error.

The temporary paste check passed in run [42806400823](https://github.com/r-eliseev/studious-goggles/actions/runs/42806400823). A contact sheet from its video showed the pasted key masked in every sampled frame. The temporary test was removed after recording this observation in `docs/knowledge/maui.md`.
