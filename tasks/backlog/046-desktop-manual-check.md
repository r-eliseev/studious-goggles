---
id: 046
status: approved
assignee:
claimed:
created: 2026-10-28
---

# Manual Avalonia check on Linux desktop and Windows

## Description
Manually walk through the Avalonia shell's skeleton scenario with a real OpenRouter key: on Linux desktop (gnome-keyring or KWallet) and on Windows. This task collects manual checks that remained unverified after tasks 038–042 (their "Reports"): actual screens, dialogs, a key persisted across restarts, a real OpenRouter response, and an image on the card. A person performs the check (provides the key and clicks); the agent prepares the build, reads logs, and records discrepancies as tasks. No code changes are made here.

Depends on: — (can run in parallel with the queue; task 045 makes log analysis easier).
Environment: Linux desktop and Windows (a person at the screen). Do not check macOS (owner decision).

## Acceptance criteria
- Record each scenario in "Comments" for Linux and Windows as passed or failed, with a description of any discrepancy.
- Create a task for each discrepancy with `scripts/new-task.sh --publish backlog <name>` and reproduction steps; list the task numbers in the "Report".
- Record non-obvious facts in `docs/knowledge/avalonia.md`.

## Analysis
- The skeleton scenario is task 016, "Plan", step 2 (items 1–10); it is the same for both shells. The only difference is the data folder: Avalonia uses `~/.local/share/ImageGen` on Linux and `%LOCALAPPDATA%\ImageGen` on Windows.
- Keys (ADR 0008): Linux uses Secret Service, collection `default` (login); Windows uses DPAPI, file `secrets/<name>.bin`. Without Secret Service the key remains in memory, and the key page indicates this.
- Generations cost money: check the selected model's current price and agree with the person before calling it. The historical estimate of <$0.40 is not a guarantee; use existing images for UI scenarios.

## Plan
After "Refresh", OpenRouter models show their price in the row ("$0.014 / MP" for FLUX.2 Klein 4B), and fal models show "price unknown"; reopening the screen reads prices from the cache without delay.

After task 223, select several audio models from the Music & sound and Speech groups in Models, search by name, save the selection, and check the audio chip in the chat header. In session settings, the voice is visible for a speech model and remains selected after reopening. A completed audio card shows the model, status and price/price unknown; image and Play buttons are hidden, and Save file saves the audio. Use the built desktop shell to check audio when the player is supported.

In Avalonia, narrow the window to about 400 dp: header chips wrap and a long title is truncated with an ellipsis; at 1100 dp, a short title and all chips fit on one line.

Also check session list updates: a new message immediately updates the row title and run count; Repeat increases the run count without moving the row; while search is active, an update preserves the query, results, scroll position and highlight of the current session.

After task 131, check the image viewer: open a completed card and confirm it is modal and fits without cropping; use Left/Right and buttons to navigate only within one run; check Escape/Close and focus restoration to the card; clicking the image does not close the viewer, clicking the background does; a missing file shows a localized message without crashing. After task 146, check a failed previous/next operation: the previous image, position and accessible name remain; retrying navigation tries the same adjacent item; rapid navigation does not apply a stale response. After task 137, check OS fullscreen only for the Avalonia viewer: the owner window retains its previous size/state, and Escape/Close restore the shell and decorations.

For Haiku/Luna: do not change code. This is a manual check with a person; an agent without a desktop must not mark scenarios as passed. For each result, record the commit/version, OS and provider. If task 016 or this task has already been archived, use the scenarios in `tasks/DONE.md` and `git show` history; do not create an empty file at the old path. After task 135, the key screen is provider-neutral; adapt the old task 016 names to the current version. Recovery by provider: fal with a handle resumes; OpenRouter without a handle before tasks 102/129 becomes Interrupted; after 102/129, an unknown local handle also becomes Interrupted without submission.

1. Agent: build `dotnet build ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj -c Release` on each machine and give the person the executable path. On Linux, check that Secret Service responds (`busctl --user status org.freedesktop.secrets` or check the service without reading secret values; do not use `secret-tool search/lookup` or publish secret file contents).
2. The person completes items 1–10 from task 016 in Avalonia, plus:
   11. Save the key, close the app, and reopen it → the key page does not appear (the key is in secure storage).
   12. Linux without Secret Service (or with the service stopped) → the key page warns that the key will not be saved; after restart the app asks for the key again.
   13. Windows: the key file exists in `%LOCALAPPDATA%\ImageGen\secrets\`, and the key is not plaintext in that file (open it in Notepad).
   14. Dialogs: delete confirmation and Edit window open above the main window; Esc closes them and focus returns.
   15. A long feed (10+ runs) scrolls, and new messages are visible at the bottom; Ctrl+Enter sends and Enter inserts a newline.
   16. With the OS dark theme, the app remains readable.
   17. Logs (task 045): after items 1–10, open `<data folder>/logs/imagegen-<date>.log`. The first line shows startup, version and log path; no key appears (search for the key's first characters); navigation metadata, HTTP status and generation statuses are present. Exact text depends on the current logger and level; response body/exception detail above Debug is prohibited after task 127, so do not require it for this scenario.
   18. Run summary (task 096): send a prompt to 2 models × 2; check that the counter and price total update as cards complete and that the summary matches after restart.
   18. Generation parameters (task 020): on the session parameters screen, select aspect ratio 3:2 and webp format and save; start a run in chat; the card shows a replacement or "не отправлено" if the model rejects the value. Use one paid request to check the `POST images` request body for `aspect_ratio`, `resolution`, `output_format` and `quality` (assumption, ADR 0011).
   19. Attachments (task 018): click "Attach image" in chat, choose a PNG, and send with and without text; the assistant response shows why a model without reference support was skipped; the card shows "Reference images: N". Use one paid request to check the `input_references` shape (assumption, ADR 0012).
   20. Image description (task 019): on a completed card, click "Describe" and "Prompt"; the dialog shows the text, provider, model and price; without confirmation the input does not change; "Insert into input" fills an empty field and, if it is not empty, asks "заменить или дописать". Use one paid request to check a model response with an image (assumption, ADR 0013).
   21. Session model screen (tasks 090 and 092): the page and model panel have two tabs, "Image models" and "Assistant model", with the first open by default; switching tabs preserves both selections; one Save stores the selected models and assistant. Models are grouped by provider, with the selected group expanded; group headers work with mouse and keyboard (Enter/Space); search shows only matching expanded groups and clearing search restores the prior expanded states; Refresh preserves the state. The screen reader announces expanded/collapsed for group headers and the selected tab. Only one model can be selected in the assistant group.
   22. Avalonia feed (task 097): open a long session and see the last row; send a message and keep the new card visible; card growth keeps the end in view when follow is enabled; after scrolling up, card growth and window resizing do not move the viewport; adding a new row scrolls down.
   23. Reopen chat and leave immediately after a card update; there are no duplicate scrolls or calls to a disappeared view.
   24. The Omnilite icon appears in the window title and launcher/taskbar on Linux and Windows.
   25. After task 228, select another embedding model in Settings, check the estimate and confirmation, and wait for reindexing to complete; run a semantic search and confirm results use the new model. Decline confirmation and verify the previous model remains selected.
3. Agent: fill in the "Report" based on the comments, create tasks for discrepancies, and update `docs/knowledge/avalonia.md` and `docs/status.md`.

Files: `tasks/backlog/046-desktop-manual-check.md`, `docs/knowledge/avalonia.md`, `docs/status.md` (only based on check results)

### Verification
```
scripts/check.sh
```

Task 156 (Avalonia): in "Assistant model", OpenRouter and configured chat provider models have provider labels; models with the same name are distinguishable. Select a model from another provider, save, and reopen the screen — the saved provider id and model id remain selected. Image-only Together is not listed.

### Image comparison (task 023)
Open the menu on a successful card and choose Compare; the list contains only other successful images from this session, with model, seed and price (an unknown price is not zero). Select a second image: side by side, preserve aspect ratios, zoom both with the wheel and pan both by dragging; Overlay shows the second image over the first, and the slider and ←/→ change the boundary. Close with Escape and the button; verify a missing file/session shows a message and does not leave a window behind. Check portrait/landscape and return to the original window.

## Report

## Comments

### 2026-10-30, codex-gpt6
Task 133: on Linux and Windows Avalonia, check actual dimensions and the shortened ratio below the image in all three styles, hiding the row for a card without an image, updating/resetting when the result changes, and dimensions accessibility through a screen reader.

### 2026-10-28, claude (task 055, text copy — Avalonia)
On Linux desktop and Windows: text in messages and the prompt row can be selected with the mouse; Ctrl+C (Cmd+C on macOS) copies the selection; assistant responses and run prompts have a "Copy" button, which shows "Copied" for two seconds after clicking.

### 2026-10-29, codex-review
The manual task remains planned: a person is needed on Linux desktop/Windows. Current recovery expectations are clarified, no price is promised in advance, and checking Secret Service does not reveal stored secrets.

### 2026-10-30, codex-gpt6 (task 135)
After tasks 135/136, check provider-neutral keys in Avalonia on Linux and Windows:
1. Settings lists providers in configuration order and shows only the last four characters masked or Not connected; Change opens a screen with the selected provider's display name.
2. If safe test keys are available, check different provider ids (OpenRouter, fal, Together after 136): a valid key is saved for the selected provider; an invalid key does not replace the previous one.
3. Remove on an optional key deletes only that key and does not close Settings. Remove on OpenRouter asks for confirmation with a warning; after leaving for Sessions, the OpenRouter form appears again.
4. Calling a provider without its key shows InvalidKey and does not terminate the app. Check the regular log and debug trace: the key and Authorization are absent at every level; do not attach secrets/logs to the report.
5. In model screens, cards with the same name from different providers have distinguishable provider display names/ids and accessible labels.

- [ ] Clear history: confirm all sessions and media are deleted; spending and keys remain; create and open a new session. (229)
- [ ] Avalonia desktop player (207): open a video and an audio card, verify playback starts, seek and volume controls work, Space pauses/resumes, Escape and the window close button stop playback; on Linux without `libvlc5`/`vlc-plugin-base`, Play is hidden.

### 2026-11-10, owner (in the chat, recorded by claude-bdd-plan)
Plan approved ("approve all tasks with plans").
