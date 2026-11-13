---
id: 140
status: approved
assignee:
claimed:
created: 2026-10-29
---

# Manual MAUI check on Android

## Description
Collect in one place the Android checks that were previously added to Windows task 016. A person with a device/emulator performs the check; the agent prepares the APK, reads anonymized logs, and records bugs. This checks existing behavior; it does not implement all listed backlog tasks.

Depends on: —
Environment: Android device or emulator and a person at the screen.

## Acceptance criteria
- For each available scenario, record the APK version/commit, Android/API version, device, result and bug reproduction steps.
- Mark an unimplemented scenario as "waiting for NNN"; do not count it as passed or as a bug.
- Create separate tasks for actual discrepancies; do not publish private keys/requests.

## Analysis
Task 016 retains the Windows scenario and historical comments; task 046 covers Avalonia. The Android check is not a dependency for closing a regular code task; if no device is available, leave it as a manual item here. After task 140 is completed, record new manual items in a new manual task with a current number instead of searching for an archived file.

## Plan
1. Agent: run `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android -c Release` and provide the APK path; the person installs it. Record version/commit/device/API and data state. Do not reset data without the owner's consent.
2. Walk through the main chat scenario in task 016, items 1–10, accounting for current recovery behavior (in-memory OpenRouter becomes Interrupted after restart; fal with a persisted resumable handle already resumes without submission, so tasks 102/129 are not required for this); separate results by provider. Real calls cost money; use as few generations as possible and already-saved images.
3. Input: soft keyboard Send, empty input, hardware Enter/Shift+Enter (050); the keyboard does not cover input/Send and Back hides the keyboard (095); Backspace, long/Cyrillic text, line breaks and cursor positions after 134. Do not count 095/134 as implemented before they are merged.
4. Feed/cards: prompt appears once; live status/price (096); scroll on new rows (097); narrow header (212: one horizontal scrolling row, long assistant id is truncated; long title is hidden in the header and shown in navigation; indicator does not overlap the header); copy (055); swipe to delete (066); "…" button and long press. After 211: tapping an image opens the viewer, "…" opens actions, and long-pressing a card caption opens actions. After 213, check square, landscape and portrait images without cropping. After 216, inspect logcat across multiple card renders: no `Cannot convert Microsoft.Maui.Thickness to type 'System.Double'` or `Cannot convert Microsoft.Maui.Controls.SolidColorBrush to type 'Microsoft.Maui.Graphics.Color'` warnings; borders and prompt text color match the theme. After 218, open a chat with completed images: logcat has no `ERROR_CODE_PARSING_CONTAINER_UNSUPPORTED`; verify video and audio players receive only media of the matching type. After 133, check actual image dimensions/orientation; after 131, check viewer, Back/Close and previous/next; after 137, check system bars, rotation, background/foreground and restoration.
5. Parameters/models: select 2×2, save/Cancel, search typos (101), sticky actions (090), provider selection after 135/136. On Android, check the Change and Remove buttons in a provider key row in Settings (task 153): at least 48 dp high, not clipped for a long provider name, and both announced by TalkBack. Attachments (018) and Describe/Prompt (019): check consent/result/price without publishing keys. For search (101), use the on-screen keyboard with suggestions: type "watte" without pressing Space or selecting a suggestion; after a quarter second, a session containing "water" should be visible. If results appear only after Space or selecting a suggestion, record in this task's "Comments" that this is a separate issue with text as it is typed.
6. Save to gallery: Android 10+ `Pictures/ImageGen`. After saving, show Saved. Check the key after restart and confirm it is absent from regular logs; do not print secret files.
7. MAUI MediaElement (task 205; Android API 26+): on a completed video card, check local playback, pause/resume, seeking, sound and system playback controls; on an audio card, check controls and sound. Navigate to the session list during playback and back; playback must stop and not resume automatically. Media generation costs money; use previously saved video/audio if available.
   After task 223: select an audio model and voice in session settings and check the audio counter in the header; a completed audio card shows the model, price/price unknown and file saving. The MAUI player appears only for completed audio and plays the local file; for Avalonia, verify Play is absent before task 224 and that Save file works.
   After task 225: in a narrow window, check image, video and audio cards appear in one centered column, the border and left edge are fully visible, and the Copy button above the run is not clipped.
8. Record results in comments; the agent writes the report/creates tasks and updates `docs/knowledge/maui.md` with confirmed facts only; run `bash scripts/check.sh`. Do not mark done before the required available human checks have results.
9. After task 228: select another embedding model in Settings, check the estimate and confirmation, and wait for reindexing to finish; run a semantic search and confirm results use the new model. Decline confirmation and verify the previous model remains selected.

### Avalonia Android probe (task 094, not checked)

Check the `com.eliseev.imagegen.avaloniaprobe` app from `poc/backlog/094-avalonia-mobile-research/ImageGen/ImageGen.Avalonia.Android/` on the same device as MAUI. Record the commit, Android/API and device model before launch. Install the Release `android-arm64` APK built by the command in report 094. Use `adb install -r <apk>` and `adb shell monkey -p com.eliseev.imagegen.avaloniaprobe 1` to install/launch; measure cold start three times using `am force-stop` and `am start -W`.

1. Launch the app and open the session list and chat; create a session, return with the system Back button, and verify Back on the session list minimizes the app.
2. Use diagnostic Activity extras to store a dummy marker in Android KeyStore: `adb shell am start -n <package>/<activity> --ez probe-secret-write true`; then run `adb shell am force-stop <package>` and `adb shell am start -n <package>/<activity> --ez probe-secret-read true`. In anonymized logcat, check `persisted=true` and `file-exists=true`; do not print file contents. Only the owner enters a real key.
3. In chat, check the on-screen keyboard's Send key, sending, and that the input and button remain visible above the keyboard. Check Back with the keyboard open and with an overlay dialog open.
4. Check copying message text, the system image picker, saving a completed image to `Pictures/ImageGen`, deletion with confirmation, and a narrow layout.
5. Use TalkBack on the list, input field, Send and image card; scroll a feed with 10 or more images and assess smoothness.
6. After opening chat, record `dumpsys meminfo` and cold start; repeat MAUI measurements under comparable conditions. Record APK size: report 094 gives 29,152,195 bytes for Avalonia versus 22,183,516 bytes for MAUI on arm64.
7. Keyboard and safe area (task 095, awaiting verification): check Android 15+ and Android 14 or lower with gesture and three-button navigation. In chat, when the field is focused, the entire field, hint, attachment and Send remain visible; the feed shrinks; long input stays visible; closing the keyboard and rotating restore the layout without an extra bar/gap. Also check the API key, model search, parameters and settings. Record Android/API, navigation type and behavior for each scenario.
   8. Run summary (task 096): send a prompt to 2 models × 2; check that the counter and price total update as cards complete and that the summary matches after restart.
   9. Feed (task 097): open a long session and see the last row; send a message and see the message, assistant response and card, with the card not hidden by the input.
   10. During generation, status updates and the image loads; the bottom edge of the last card remains visible while follow is enabled.
   11. During generation, scroll up; updates do not move the feed. Return to the end; following resumes.
   12. At the end of the feed, open the keyboard; the last row remains visible. Close the keyboard; the feed does not get an extra gap.
   13. When the last card is taller than the viewport, scroll up inside it: an update must not pull the viewport down just because the last index is still visible. Scroll to the end; follow is enabled again.
   14. Open and close chat several times, then leave immediately after sending; there is no duplicate scrolling or error from a disappeared page.
   15. Repeat the feed checks with 10+ long runs; record device model, Android/API and observed smoothness.
   16. Image viewer (task 131): open an image and confirm AspectFit without cropping; navigate only through successful images from one run; check there is no wrap, and check Close and system Back. Tapping the bitmap does not close the viewer; tapping the empty background does. Check portrait and landscape images, letterbox areas, padding and toolbar; check the localized message for a missing/corrupt file and TalkBack announcements for buttons/position. After task 146, check failed previous/next: the previous image and position remain, retrying navigation targets the same neighbor, and rapid navigation does not apply a stale load.
   17. Fullscreen viewer (task 137): check the initial visibility of status/navigation bars, their hiding in the viewer and temporary reveal with a system swipe; Close and Back restore the original state, including bars that were hidden before opening the viewer. Minimize/restore the app and check restoration; rotate the screen and verify behavior after handler/activity recreation.

Files: `tasks/backlog/140-android-manual-check.md`, `docs/knowledge/maui.md`, `docs/status.md` (confirmed new questions only), and new tasks for discovered bugs.

### Image comparison (task 023)
Open the menu on a successful card → Compare, then select a second image from this session. Check side-by-side and Overlay, visible model/seed/cost labels and the slider boundary; pinch zooms both images in sync, pan moves both in sync, and portrait/landscape images are not stretched. Back/Close returns to chat. Check the message for a deleted file and close comparison without leaving a stuck modal page.

### Video player (task 219)
A completed video card shows a thumbnail/icon, duration and Play; there is no embedded video player in the feed. Play opens full-screen playback and the video is visible; system Back closes the page and stops the sound. The embedded audio player in the feed continues to work. An Android device or emulator is required.

### Video thumbnail (task 220)
Check that completed videos show a frame from the beginning, variants from one run are distinguishable, the frame aspect ratio is preserved and the card does not crop the image. While generation is in progress, the card uses the selected video aspect ratio. Also check a video with no extractable frame: the icon remains and the app does not report an error.

### Logs (task 187)
In Settings, tap Share logs: the system Share sheet opens, the shared file is available in Telegram/"Downloads" and can be read; verify the current log is included. Tap Clear logs and confirm deletion: old files are deleted, the current file remains and new lines continue to be written to it. Separately decline confirmation and verify the files are unchanged.

## Report

## Comments

### 2026-10-30, codex-gpt6
Task 133: on Android, check actual dimensions and the shortened ratio below a successfully generated image, hiding the row when the file is missing, updating it for a new generation, and reading dimensions with TalkBack.

### 2026-10-29, codex-review
Split out during review of PR #42 at the owner's request. This is a work plan, not an execution report. Approval of the original task remains valid for the transferred scope; this PR does not implement code.

### 2026-10-30, codex-gpt6 (task 135)
After tasks 135/136, check provider-neutral keys on Android:
1. Settings shows all configured provider rows with display name, masked key/not configured, Change and Remove; the list is not duplicated when revisiting.
2. Enter keys for two providers (Together after 136, plus OpenRouter or fal) and verify screens/statuses refer to the selected provider and secrets are not mixed. An invalid check does not erase the saved key.
3. Remove on an optional key leaves Settings open and updates only that row. Remove on OpenRouter shows a warning; after returning to Sessions, the OpenRouter form appears again.
4. For a provider without a key, a regular request shows InvalidKey without crashing the app. Regular logs and available logcat do not contain the key/Authorization; do not record secret values.
5. If two providers have models with the same name, the screen shows provider display name/id; TalkBack announces a distinguishable label.

### 2026-10-31, claude-audit
Owner report on release v0.1.5 (screenshots in chat): (1) the chat feed cannot scroll down inside a tall run, so images cannot be viewed — task 159; (2) the keyboard covers the input and the page pans — task 160; (3) Backspace does not delete — task 134. Repeat these checks here after fixes.

### 2026-10-31, claude-audit
Two more owner observations on v0.1.5: old history is not visible after the first generation (task 161, awaiting clarification), and tapping a session in the list reveals Rename/Delete instead of opening it (task 162).

### 2026-10-31, claude-audit — verification of fixes 134, 159, 160, 162 (Galaxy S25, Android 17)
1. Chat, Gboard: Backspace deletes one character at a time and while held; selection and Backspace; emoji; delete to empty and send; Enter on the keyboard sends once (134).
2. Session with a 7 × 1 run: swipe down to the last card during and after generation; when opening the session, the bottom of the last run is visible; swipe up and card updates do not jerk the feed; return to the bottom and the feed follows new rows again; rotate the screen (159).
3. Chat input focus: the whole field and attachment/send buttons stay above the keyboard; the header does not go under the status bar; check long text, hiding the keyboard, the API key field and session list search (160).
4. Session list: on the phone, a short tap in "Today" and "Earlier" opens a session; a long press opens Rename/Delete actions; cancel changes nothing; TalkBack reads the title and long-press hint (214). On desktop, check a short tap, swipe and context menu.
Installation: this build's APK is signed with a different key from the release; installing over the existing app requires uninstalling it first (task 163).

### 2026-11-01, claude-audit (task 088)
Long-pressing a completed chat card opens the action sheet, as does the "…" button; Running/Queued cards do not. In 088, `TouchBehavior` is disabled only on Windows, so Android should not change.

### 2026-11-01, claude-audit (tasks 163, 165 — permanent signing key)
1. On the next release or a manual release workflow run, `apksigner verify --print-certs <apk>` should show `CN=ImageGen, O=eliseev`, SHA-256 `6711f8badc39c087512e78a04ab4d7a8313d9d00b33081bf139867fc55b10277` — the same as run `41339041274`.
   **Completed 2026-11-01 (claude-audit):** release run `41343960926` (commit `fa116cb`, APK `ImageGen-Maui-dev-15-android.apk`) — certificate `META-INF/IMAGEGEN.RSA`: `O=eliseev, CN=ImageGen`, SHA-256 `6711f8ba…10277`, matching. Verified with `openssl pkcs7`/`x509` for the v1 signature; `apksigner` (v2/v3) was not available in this environment.
2. Uninstall the old app (v0.1.5 and earlier used debug signing keys), install the APK with the permanent key, create a session and enter a key; then install the next APK over it. Installation should work without uninstalling, and sessions and keys should remain.

- [ ] Clear history: confirm all sessions and media are deleted; spending and keys remain; create and open a new session. (229)

### 2026-11-10, owner (in the chat, recorded by claude-bdd-plan)
Plan approved ("approve all tasks with plans").
