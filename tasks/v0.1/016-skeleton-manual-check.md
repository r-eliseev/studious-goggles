---
id: 016
status: approved
assignee:
claimed:
created: 2026-10-27
---

# Manual Windows check of the application skeleton

## Description
Check the working application skeleton end to end with a real OpenRouter key and record the result. A person performs the check (the key and app interaction are required); the agent prepares the build, reads the log, and files tasks for defects. This task does not change code.

Depends on: 015.
Environment: Windows only (run the app and perform the manual check).

## Acceptance criteria
- Mark every scenario item in “Report” as passed or failed, with a description of any discrepancy.
- Create a task for every discrepancy with reproduction steps using `scripts/new-task.sh --publish backlog <name>`; list the task numbers in “Report”.
- Record any non-obvious findings (for example, behavior of the free assistant model) in `docs/knowledge/`.

## Analysis
- This is the first live OpenRouter call: there was no key while the tasks were prepared, and the client was tested only with a fake HTTP handler.
- Generations cost money: the default model `black-forest-labs/flux.2-klein-4b` costs about $0.014 per megapixel. The scenario below costs less than $0.20.
- The free assistant model may return 429 (rate limit); this is expected and should appear as an error in chat.

## Plan
After “Refresh”, OpenRouter model rows show a price (“$0.014 / MP” for FLUX.2 Klein 4B), while fal models show “price unknown”. Reopening the screen reads prices from cache without delay.

In MAUI on Windows, the wide window keeps the existing header; long names are truncated and chips wrap onto a second line.

Also check session list updates: a new message immediately updates the row title and run count; Repeat increments the run count without moving the row; while search is active, updates preserve the query, results, scroll position, and highlight of the current session.

The current scope is Windows. Android follow-up items from historical comments belong to task 140; do not rewrite those comments. Compare appearance and button names with the current version (after task 135 the key screen becomes provider-neutral). Previous cost estimates are not guaranteed: show the person the current price before live calls, and reuse existing images.

1. Agent: build the app with `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-windows10.0.19041.0` and give the person the path `ImageGen/ImageGen.Maui/bin/Debug/net10.0-windows10.0.19041.0/win-x64/ImageGen.Maui.exe`.
2. The person follows the scenario and records the result of each item in “Comments”:
   1. First run: the “OpenRouter key” page opens. An invalid key shows “OpenRouter rejected this key.”; a valid key returns to the empty sessions list.
   2. “New session” opens chat with the “Models: 1 × 1” button.
   3. Send “draw a red fox in a snowy forest” → a run block with the prompt (`Prompt: ...`, shown once, not duplicated in the assistant message — task 057) and a card appear; the card transitions from “Queued”/“Generating...” to “Done” with image, model, seed, and price.
   4. Send “make it night” → a new run with an updated prompt (the prompt mentions night and preserves the rest).
   5. Send a message without a clear request (for example, “hi”) → the assistant replies with text and no run.
   6. On a card: Repeat creates a new card with the same seed; New seed creates a new card with a different seed; Edit opens a window with the exact prompt, then creates a card with the same seed and edited prompt.
   7. Open the model picker, select 2 models and 2 runs, then Save → “Models: 2 × 2”; the next message creates 4 cards, with the same pair of seeds for each model.
   8. A model without seed support (shown as “no seed” on the model screen) displays “seed not supported” on its card.
   9. Record the provider and commit. Close the app during generation and reopen it: a job with a persisted resumable handle (fal) resumes without another submit; a job without a handle (synchronous OpenRouter before 102/129) becomes Interrupted. A missing provider after 106 should produce ModelUnavailable. Do not expect Error for every provider; compare with current S3.8.
   10. Return to the list → the session is named after its first message; Delete → confirm → the session disappears and `%LOCALAPPDATA%\User Name\com.companyname.imagegen.maui\Data\images\<id>` is removed.
   11. Session search (task 101): query “watte” finds a session containing “water”; query “lighthu” finds one containing “lighthouse”. An exact word prefix (“wate”) should rank above a typo match.
   12. Accessibility (task 103): on an empty sessions list, the screen reader reads the “New session” button label.
   13. Session models screen (tasks 090, 092, and 204): the narrow page and wide pane have “Image models”, “Assistant model”, and “Video models” tabs, with the first open by default; switching tabs does not reset selections; one Save persists selected models and assistant. Models are grouped by vendor and the selected group is expanded; clicking the header collapses or expands it. Search keeps only matching expanded groups; clearing search restores their prior states, and Refresh preserves the current state. Tab focuses tabs and Enter switches them; the screen reader announces the tab name and group header with expanded/collapsed state. Verify only one assistant model can be selected.
   14. Keyboard/safe area (task 095, Windows only): pages show no visible changes and add no extra spacing during normal startup.
   15. Run summary (task 096): send a prompt to 2 models × 2; check that the counter and total price update as cards complete and that the summary matches after restart.
   16. MAUI transcript (task 097): open a long session and verify the last row is visible; send a message and wait for a card, which must not be hidden by the input; while the card updates, keep the bottom edge visible as long as the user follows the end; scroll up and verify updates do not jerk the transcript; scroll down and verify following resumes.
   17. Reopen and close chat after sending; there should be no duplicate scrolling or errors from a disappeared page.
   18. MAUI MediaElement (task 205): a completed video appears on its card as a poster/icon with Play (219); Play opens a separate page and starts the video; Escape/Back closes it and stops audio; an audio card remains an embedded player with controls. Check the Windows card, context menu, and keyboard navigation.

3. Agent: use the comments to fill in “Report”, create tasks for discrepancies, and update `docs/knowledge/` and `docs/status.md`.

After task 131, check the MAUI Windows viewer: open a completed card; verify that the image fits without cropping and navigation stays within one run; close with the button and Escape; verify focus returns to the card; clicking the bitmap does not close the window, while clicking the empty background does. Check portrait and landscape images, letterboxing, padding, and toolbar; a missing file shows a localized message. After task 146, check failed previous/next navigation: the displayed image, position, and accessible name are preserved; retrying uses the same adjacent file; rapid navigation does not apply stale loads.

Files: `tasks/v0.1/016-skeleton-manual-check.md`, `docs/knowledge/*.md`, `docs/status.md` (only based on check results)

### Verification
```
scripts/check.sh
```

### Saving an image (task 080)
1. Windows: “Save image as…” on a completed card opens the “Сохранить как” dialog; suggested filename is `imagegen-<8 characters>.png`; the file opens.
2. Android items moved to task 140; check iOS only if a separate, approved environment is available.
3. After saving, the card shows “Saved”.

Task 156 (MAUI Windows): in “Assistant model”, OpenRouter models and configured chat providers appear with provider labels; same-named models are distinguishable. Select a model from another provider, save, and reopen the screen; the saved provider id and model id remain selected. Image-only Together does not appear in the list.

### Image comparison (task 023, MAUI Windows)
Open Compare from a successful card's menu and select another successful image from the same session; check model/seed/cost labels, side-by-side and Overlay modes, and the slider with the mouse and ←/→ keys. Verify that images preserve their aspect ratio, Close/Escape returns to chat, and a missing file shows a message.

## Report

## Comments

### 2026-10-30, codex-gpt6
Task 133: on MAUI Windows, verify the actual width/height and shortened ratio under an image, no line on a card without an image, preservation of the line when the same card updates, reset on a new generation, and availability of dimensions through Narrator.

### 2026-10-28, claude-07d6 (task 020, parameters)
Add to the MAUI Windows manual check: the session “Параметры” screen (button beside “Модели” in chat) opens; pickers show options for selected models and “По умолчанию модели”; saving returns to chat; a replaced card shows “Соотношение сторон: … → …”.

### 2026-10-28, claude-07d6 (task 045, logging)
Add to the MAUI Windows manual check: after the scenario, open the app's `%LOCALAPPDATA%` folder (`FileSystem.AppDataDirectory`) → `logs/imagegen-<date>.log`. The first line records startup, version, `MAUI` shell, and log path; no key is present. On Android, check the same file in the app folder on the device.

### 2026-10-28, claude-07d6 (task 018, attachments)
Add to the MAUI Windows manual check: “Attach image” opens the file picker, F10.6 consent appears before the first image, “N of 4” updates, and an attachment can be removed; after sending, the image is sent to the assistant if the model accepts it.

### 2026-10-28, claude-07d6 (task 019, image description)
Add to the MAUI Windows manual check: “Describe” and “Prompt” buttons appear on a completed card; the dialog shows provider, model, and price or “cost unknown”; the input is unchanged until confirmation.

### 2026-10-28, owner (first manual check attempt, recorded by claude)
Built from `main` d4cae55 and run on Windows. The key page works: the key was checked (`GET /key` → 200) and saved. The owner paused the scenario after item 3: the assistant returned “Assistant error (RateLimited): Provider returned error” because the free `google/gemma-4-31b-it:free` model was overloaded by its provider (429, `upstream_provider_shared_pool`, according to the log). Owner feedback became tasks: 049 (paid default assistant model and model selection), 050 (send with Enter), 051 (mockups and design system), and 052 (log contains the key mask from the `/key` response). Resume the scenario after 049.

### 2026-10-28, owner (second manual check attempt, recorded by claude)
Built from branch 053 (be25d0b) and run on Windows. The “blue square moon” session was switched to `anthropic/claude-haiku-5.5`: the assistant called `generate_images`; generation `Succeeded` in 6 s, cost $0.014, and the file was saved (232 KB JPEG). Findings: image is not visible on the card — 054 (buttons are not clipped and remain visible while scrolling); Enter does not send — 056 (Windows part of 050); text copying is needed — 055; the prompt appears twice (assistant message and run block) — 057.

### 2026-10-28, claude (task 062, MAUI theme: Windows checks)
Manual item for wave 2 (062). Not checked in the cloud session: the MAUI window and theme switching.
1. Create `appearance.json` in the data folder with `{"style":"gallery","theme":"dark"}` and launch: dark theme, teal accent (`#5EC2B5`), and Onest font for text.
2. Change it to `{"style":"quiet","theme":"system"}` and switch the Windows theme; the app should change theme without restarting.
3. Verify platform values for `IgFontSizeBody` and `IgControlHeight` (WinUI 14 or 15, height 32) on screens where they are used (after tasks 063–072).

### 2026-10-28, claude (task 055, copy text: MAUI, Windows and Android)
1. Windows: the assistant response and run prompt row have a “Copy” button; clicking copies the text (paste it into Notepad), and “Copied” appears beside it for two seconds.
2. Android: the same “Copy” button on the assistant response and prompt copies text. Long-press copying is unavailable: MAUI labels cannot be selected (decision recorded in `docs/knowledge/maui.md`).

### 2026-10-28, claude (task 066, sessions list and key: MAUI, Windows and Android)
1. Windows: the list is divided into “Today” and “Earlier”; at window widths of 840 or more, the right side shows the empty state “What do you want to draw?”; hovering over a row shows a trash icon.
2. Android: delete a session by swiping left and tapping the red Delete button (confirmation is shown).
3. Key screen: the card is centered; an invalid key shows red error text (the MAUI field itself is not highlighted; see task 066 report).

### 2026-10-28, claude (task 070, models and parameters: MAUI, Windows and Android)
1. Models: “Image models · N selected” changes when a checkbox is toggled; choose the assistant with a single-selection radio button, and verify row tags “free” and “takes images”.
2. Save and Cancel buttons are at the bottom; on Windows, Save is on the left. On Android, system Back acts as Cancel, and the Cancel button also works.

### 2026-10-28, claude (task 072, Avalonia dialogs: desktop checks)
Confirmation and input dialogs: larger title, full-width fields, red delete button. Button order: primary action first on Windows and Linux. Escape closes the window as “Cancel”; Enter confirms single-line input. Not checked in the cloud session (desktop required); see 046.

### 2026-10-28, claude (task 065, Settings screen: MAUI and Avalonia)
1. The gear at the bottom of the sessions list opens “Settings”; the key is shown by its last four characters; “Change…” opens the key screen.
2. Style (Quiet, Gallery, Notebook) and theme (System, Light, Dark) change immediately and persist after restart (`appearance.json`).
3. On Windows, “Open folder” opens the logs folder; the button is absent on Android.

### 2026-10-29, codex-review
Android manual scenarios were moved to task 140. Historical results are preserved; current recovery is checked by provider/S3.8, not by a blanket Interrupted expectation for fal.

### 2026-10-30, codex-gpt6 (task 135)
After tasks 135/136, check provider-neutral keys in MAUI Windows:
1. Settings lists configured providers and shows either the last four key characters or Not connected; Change opens the key screen for the selected provider.
2. Add or replace OpenRouter, fal, and Together keys in turn (if Together is registered by 136); each is stored under its provider, and invalid validation does not replace the old key.
3. Remove for an optional provider deletes only its key and leaves Settings open. Remove for OpenRouter shows a warning that the assistant key is required; after returning to Sessions, OpenRouter key entry is requested again.
4. Without a key for a given provider, a chat/generation request shows the usual InvalidKey error and the app remains open. Open the regular log and verify that it contains no keys or Authorization headers; do not record real secrets in comments.
5. In the model list, same-named models from different providers are distinguishable by provider display name and id.

### 2026-11-01, claude-audit (tasks 088, 139, 148: card menu and viewer, MAUI Windows)
1. “…” on a completed card: mouse click and Tab + Enter open the same menu as right-click; Escape closes it; it can be reopened. The button is disabled for Running/Queued cards and works for a card from history after restart. Copy prompt copies the prompt (088).
2. The viewer opens full-screen (without title bar or taskbar) on the same monitor as its window. Close, Escape, and image-load errors restore the previous window state: a normal window returns to the same position and size; a maximized window remains maximized. Open and close five times in a row, test a second monitor if available, and close the main window while the viewer is open (139).
3. In the viewer opened from a middle image, Left/Right navigate only within the same run and stop at the first/last image (no wrapping), one press moves one step; Escape closes it; after reopening, focus is on Close (148).

### 2026-11-10, owner (in the chat, recorded by claude-bdd-plan)
Plan approved ("approve all tasks with plans").
