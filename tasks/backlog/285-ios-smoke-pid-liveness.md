---
id: 285
status: in-progress
assignee: codex-20261117
claimed: 2026-11-17T18:39:03Z
created: 2026-11-17
---

# iOS simulator smoke: decide liveness by the launched PID, not by launchctl list

## Description
Task 274's 20-repeat run [44040174321](https://github.com/r-eliseev/studious-goggles/actions/runs/44040174321) gave 9/20 Avalonia and 11/20 MAUI smoke failures with zero app crash reports. In the failed runs the app had started (`launch.log` printed `com.eliseev.imagegen.avalonia: <pid>`, the app log has its start line), but every `xcrun simctl spawn <udid> launchctl list` probe timed out (4 s) during the 30-second window, so the smoke counted the app as not running (Avalonia run 05; MAUI run 09: the process first appeared at 46.7 s after earlier timeouts). The smoke observer, not the app, is the main suspect. Liveness decision: `ImageGen/ImageGen.E2E.Common/ci/apple-sim-smoke.sh` line ~264 (`running = probe_succeeded and bundle_id in launchctl_entry`).

Depends on: 274 (done).
Environment: Linux for the edit; GitHub Actions `macos-26` (`apple-ci.yml`, `workflow_dispatch`) for the proof.

## Acceptance criteria
- The script reads the PID that `simctl launch` prints into `launch.log` (`<bundle id>: <pid>`, line ~194) and decides liveness at each sample by that PID, using a fast host-side check of the simulator process: `ps -p <pid>` on the macOS host, because simulator processes are host processes. If the PID is not printed within 30 s, the sample is "not launched".
- `launchctl list` stays as diagnostic evidence only (its column in `process-timeline.txt` and its status), not part of the decision.
- `summary.json` keeps its keys; `aliveAfter30s` is true when the PID is alive at the 30-second sample; add `pid` and `livenessSource: "pid"`.
- Two `workflow_dispatch` runs with `ios_smoke_repeats=20` for both apps. The Report has the failure counts. For any failure, it quotes the timeline and the app log of that run, and says whether the PID died (an app exit) or never appeared (a launch failure).
- `docs/knowledge/apple-ci.md`: how liveness is decided now, and the result.
- If both runs show 0 failures per app, the Report says so, and 247 can be re-planned by the planner (three consecutive green runs per app). If real exits remain, `review` with the evidence and a proposed app-side investigation; do not change app code here.

## Plan
1. Parse the PID from `launch.log` while it is written (the launch runs in the background); the sample loop uses `ps -p` for the decision.
2. Keep the `launchctl` probe as evidence; write `pid` and `livenessSource` to `summary.json`.
3. `bash -n`; dispatch two 20-repeat runs; read the artifacts; write the Report and the knowledge entry.

Files: `ImageGen/ImageGen.E2E.Common/ci/apple-sim-smoke.sh`, `docs/knowledge/apple-ci.md`.

### Verification
```
bash -n ImageGen/ImageGen.E2E.Common/ci/apple-sim-smoke.sh
bash scripts/check.sh
# GitHub Actions: apple-ci.yml workflow_dispatch, ios_smoke_repeats=20, twice; counts and evidence in the Report
```

## Report

## Review

## Comments

### 2026-11-17, claude-planner
Created from the review of task 274 (its proposed follow-up).
Plan approved (standing instruction of 2026-11-07: "approve all tasks that will come").

### 2026-11-17, codex-20261117 — dispatch 44150856492
Dispatched `ios_smoke_repeats=20`, `ios_toolchain=26.4.1` on this task branch. MAUI iOS, MAUI Catalyst and Avalonia macOS builds succeeded. `build-avalonia-ios` was cancelled after 15m03s queued and `apple-preflight` after 17m24s queued; both jobs have no runner name and no steps ran. `smoke-ios-avalonia` was subsequently cancelled before its smoke step because its build produced no artifact. The MAUI 20-repeat smoke step started at 19:01:20Z and is still running. This dispatch does not count as a complete two-app run; retain its MAUI artifact/results, then dispatch another full run after it finishes.

### 2026-11-17, codex-20261117 — MAUI artifact from dispatch 44150856492
The MAUI smoke completed 20 repetitions and uploaded artifacts, but all 20 were invalid for the PID decision: each `summary.json` had `pid=null`, `launched=false`, `aliveAfter30s=false`; `launch.log` contained the `<bundle id>: <pid>` line only after the timeline sampling ended and cleanup killed `simctl launch --console-pty`. For example, run 01's app log records startup at 19:04:58Z and its launchctl timeline sees PID 4395 at the 5-second sample, while all PID columns stay empty; the final launch log then contains `com.eliseev.imagegen: 4395`. No app crash reports were collected. Changed the launch observer to omit `--console-pty`, so `simctl launch` returns its PID promptly while the launched app continues running. This dispatch does not count as a valid run; rerun the two-app workflow after the next check.
