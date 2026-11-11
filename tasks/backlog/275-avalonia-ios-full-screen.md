---
id: 275
status: approved
assignee:
claimed:
created: 2026-11-11
---

# Avalonia iOS: run full screen instead of the letterboxed compatibility mode

## Description
On every Avalonia iOS simulator screenshot the page does not fill the screen: black bars on top and bottom and no navigation bar at the top (noted in task 247, run [42405774295](https://github.com/r-eliseev/studious-goggles/actions/runs/42405774295)). `ImageGen/ImageGen.Avalonia.iOS/Info.plist` has neither `UILaunchStoryboardName` nor `UILaunchScreen`. Without one of them iOS runs the app in the legacy compatibility mode with the old fixed screen size, letterboxed — which matches the screenshots. The MAUI head gets a launch screen from its splash resources and fills the screen.

The minimum iOS version of the head is 15.0, so the `UILaunchScreen` dictionary (iOS 14+) is enough: it needs no storyboard file and no `ibtool` step.

Depends on: —.
Environment: Linux (the edit, the Android/desktop builds stay untouched); GitHub Actions `macos-26` (the iOS build and the smoke screenshot).

## Acceptance criteria
- `Info.plist` of the Avalonia iOS head declares `UILaunchScreen` (an empty dictionary is enough; a background color key only if the build requires it). No storyboard or xib is added.
- The `smoke-ios-avalonia` screenshots (`screenshot.png`, `screenshot-30s.png`) show the page over the whole screen: no black bars above and below; the key screen reaches the safe area edges. Compare with the screenshot of run 42405774295 and attach both crops (the method: `docs/knowledge/ui-e2e.md`, "How to watch the videos and screenshots").
- If the page now fills the screen but content goes under the status bar or the home indicator, record it in "Comments" as a finding with a proposed fix (safe area insets in `Views/MainView`), set `blocked`; do not change the view in this task.
- The `build-avalonia-ios` and `smoke-ios-avalonia` jobs are green on a `workflow_dispatch` run.
- `docs/knowledge/apple-ci.md`: one line with the fact (no launch screen key → letterboxed compatibility mode) and the run number.

## Analysis
- `ImageGen/ImageGen.Avalonia.iOS/Info.plist` (no launch screen key), `ImageGen/ImageGen.Avalonia.iOS/Views/MainView.axaml` (the page host).
- The smoke script already saves screenshots at 15 s and 30 s (`ImageGen/ImageGen.E2E.Common/ci/apple-sim-smoke.sh`); no script change is needed.
- Overlap: task 274 changes the smoke script and `apple-ci.yml`, not `Info.plist`; either order works. If 274's diagnostics are merged first, use its run for the screenshots.

## Plan
1. Add the `UILaunchScreen` key to `Info.plist`.
2. Run `apple-ci.yml` through `workflow_dispatch` on the task branch; take the screenshots from the `ios-smoke-avalonia` artifact; compare with run 42405774295.
3. Knowledge line; "Report" with the run link and the crops.

Files: `ImageGen/ImageGen.Avalonia.iOS/Info.plist`, `docs/knowledge/apple-ci.md`.

### Verification
```
bash scripts/check.sh
# GitHub Actions: apple-ci.yml (workflow_dispatch) — build-avalonia-ios and smoke-ios-avalonia green, screenshots full screen
```

## Report

## Review

## Comments
### 2026-11-11, owner (in the chat, recorded by claude-planner)
Plan approved ("create tasks for the fixes with plans").
