---
id: 198
status: approved
assignee:
claimed:
created: 2026-11-06
---

# Manual check of the Avalonia Android head

## Description
ADR 0026: decide whether to replace MAUI on Android after manually checking both builds on a device. A person with a phone (the owner), not an agent, performs the check. Use the APK from the release after 197 (`ImageGen-Avalonia-<version>-android.apk`).

Depends on: 195, 197 (196 for the logs item).
Environment: the owner's Android phone; the MAUI app `com.eliseev.imagegen` may remain installed.

## Acceptance criteria
For each item, record the result (works / does not work / not checked), phone model and Android version, and APK version. File any bugs as separate tasks.

## Analysis
The items repeat task 140 (MAUI) to compare the shells and cover unknowns from ADRs 0019/0026.

## Plan
1. Installation: install the APK alongside the MAUI app; the home screen shows "Omnilite Avalonia" with the round Omnilite icon. Install the next release over it without uninstalling.
2. First launch: the OpenRouter key screen appears; the key is saved and remains after fully closing and reopening the app (Android Keystore).
3. Sessions: create, open, rename and delete sessions; scroll the list with a finger.
4. Chat: type with the keyboard; the keyboard does not cover the input; Send/Enter sends and Backspace deletes; receive an assistant response and generate an image; scroll the feed inside a tall run.
5. Card: tapping an image opens the full-screen viewer; Previous/Next work; Back closes the viewer; "…" and a long press open the actions; try Repeat, New seed, Edit, Save, Copy prompt, Compare (both images, drag with a finger), and Cancel during generation. On Android 10+, Save puts the image in the gallery; on Android 9, it opens the system file save dialog and writes the image to the selected location.
6. Dialogs: confirm deletion, enter text (Edit prompt), choose an option; Back closes the dialog as "Отмена".
7. System Back on each screen returns to the previous screen; on the session list it minimizes the app.
8. Settings: theme and style; provider keys; default assistant model; Share logs (196) — confirm the startup log says the temporary directory is under CacheDir, then share the file with Telegram/"Downloads"; Clear logs.
9. Semantic search: enable it, check indexing status, and search in Meaning mode.
10. Attach an image to a message (file picker).
11. TalkBack: card and dialog buttons are announced.
12. Compare with MAUI on the same phone: cold start time (seconds), feed smoothness, installation size (Settings → Apps).
13. Activity recreation (task 210): open a screen over the session list, rotate the device, enable/disable split screen, and change the system font size; the current screen stays visible, Back returns to the previous page, and an open overlay closes without hanging.
14. After task 224: open a finished video card with Play; the full-screen AndroidX Media3 player shows video with sound, and pause and seek work; Back stops the sound and returns to the chat. Check the audio player controls. Rotate the device during playback; the position must not reset. Verify the player closes when its token is cancelled.

Files: — (record results in this task's "Report").

## Report

## Comments

### 2026-11-06, claude-audit
Created at the owner's request together with 191–197. This is a manual task for the owner to perform on a phone.

### 2026-11-10, owner (in the chat, recorded by claude-bdd-plan)
Plan approved ("approve all tasks with plans").
