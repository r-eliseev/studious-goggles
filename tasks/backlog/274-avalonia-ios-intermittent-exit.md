---
id: 274
status: approved
assignee:
claimed:
created: 2026-11-11
---

# Avalonia iOS: find why the app sometimes exits right after launch in the simulator

## Description
Task 247 is blocked: the Avalonia iOS smoke passed two of the last three nightly runs, and in run [42794177774](https://github.com/r-eliseev/studious-goggles/actions/runs/42794177774) the app launched and was gone by 30 s (`launched=true`, `aliveAfter30s=false`, `crashReports=0`). `launch.log` holds only `getpwuid_r did not find a match for uid 501` (a known simulator warning, not a cause), `simulator.log` is empty. MAUI passed all three runs. The owner's decision of 2026-11-10: the smoke must always pass; continue the investigation, do not relax the criterion.

The current evidence cannot explain an exit, because three sources are missing:
1. **The app's own log is never collected.** The iOS head writes `imagegen-*.log` through `AddImageGenFileLogging` and attaches `CrashLog` and `Dispatcher.UIThread.UnhandledException` (`ImageGen/ImageGen.Avalonia.iOS/MobileApp.axaml.cs`), into `LocalApplicationData/ImageGen/logs` inside the app's data container (`Platform/IosAppPaths.cs`). An unhandled managed exception (no crash report, see `docs/knowledge/apple-ci.md`) would be there.
2. **The simulator log predicate probably matches nothing.** `apple-sim-smoke.sh` filters `process CONTAINS "ImageGen" OR process CONTAINS "Omnilite"`; the process name is the bundle's `CFBundleExecutable`, which the script already reads but does not use in the predicate. The system processes that record why an app was terminated (`runningboardd`, `SpringBoard`/`FrontBoard`, watchdog) are not included at all.
3. **No exit timeline.** The script checks the process only at 15 s and 30 s; the exit time and the last exit status from `launchctl list` are not recorded.

This task adds the diagnostics, reproduces the exit and records the cause. It does not fix the app: the fix is planned by the planner from the findings (a follow-up task).

Depends on: —.
Environment: GitHub Actions `macos-26` (the `apple-ci.yml` workflow); script edits on Linux.

## Acceptance criteria
- `apple-sim-smoke.sh` additionally saves, on success and on failure:
  - `app-logs/`: every `imagegen-*.log` found under the data container (`xcrun simctl get_app_container "$udid" "$bundle_id" data`, found with `find`, not a hard-coded path), copied before the simulator is deleted;
  - `simulator.log` with the predicate `process == "<CFBundleExecutable>" OR process == "runningboardd" OR process == "SpringBoard" OR process == "FrontBoard"` (the executable name from `Info.plist`), `--last 10m`, `--style compact`; if it is still empty, `simulator.log` says so explicitly together with the predicate used;
  - `process-timeline.txt`: every 5 s from launch to 60 s, a timestamp, running yes/no and the app's `launchctl list` line (PID and last exit status);
  - `summary.json` keeps its keys and adds `exitedAtSeconds` (the first sample where the process is gone, or `null`).
- The decision rule of the smoke does not change: alive at 30 s and no crash report. Keep the 60 s timeline as extra evidence only; it must not make the job slower than `timeout-minutes`.
- `apple-ci.yml` gets a `workflow_dispatch` input `ios_smoke_repeats` (default `1`, maximum `20`): the Avalonia (and MAUI) smoke job runs the script that many times in a row on fresh installs (`reports/ios-smoke/<app>/run-NN/`), and fails if any run fails. Scheduled and push runs keep one run.
- Reproduction: one manual run with `ios_smoke_repeats=20` for both apps (links in the "Report", with the failure count per app). If Avalonia fails at least once, the "Report" quotes the relevant lines of `app-logs/`, `simulator.log` and `process-timeline.txt` of a failed run and states the cause or the strongest hypothesis with evidence. If it never fails in 20 runs, run once more with 20 and record both results.
- `docs/knowledge/apple-ci.md`: the new evidence files, how to read them, and the result (failure rate, cause).
- Finish as `review` with the findings and a **proposed fix** in "Comments" (files and steps). Do not change `ImageGen/ImageGen.Avalonia.iOS/` or other app code in this task.

## Analysis
- Script: `ImageGen/ImageGen.E2E.Common/ci/apple-sim-smoke.sh` — launch at line ~138 (`simctl launch --console-pty`), `is_running` at ~141, log collection in the cleanup at ~62–72, `copy_crash_reports` at ~29. The data container must be read before `simctl delete` in the cleanup trap.
- Workflow: `.github/workflows/apple-ci.yml`, jobs `smoke-ios-maui` (~214) and `smoke-ios-avalonia` (~248): they run on `schedule`, `workflow_dispatch` and push, not on pull requests (`if: ... github.event_name != 'pull_request'`); `continue-on-error: true` stays as it is (247 decides when to remove it).
- Known facts from 247: `TrimMode=copy` broke the `AppDelegate` mapping (removed); `InitializeComponent()` in `MainView`; font links; a 15 s check was unreliable. Candidates for this exit (to confirm or rule out with evidence, not to fix): an unhandled exception on a background thread during startup (the composition, the key store in Keychain, the first DB migration), a watchdog termination of a slow launch, a simulator resource kill (`runningboardd` jetsam).
- Overlap: task 275 changes `ImageGen.Avalonia.iOS/Info.plist` (no overlap with this task's files); 247 waits for this task.

## Plan
1. `process-timeline.txt` and `exitedAtSeconds` (the loop replaces the two sleeps; the 15 s and 30 s screenshots stay).
2. The predicate with `CFBundleExecutable` and the system processes; the explicit empty-log message.
3. Copying the app logs from the data container in the cleanup, before delete.
4. The `ios_smoke_repeats` input and the loop in both smoke jobs (`run-NN` folders, overall failure if any run failed, the artifact uploads the whole folder as now).
5. A manual run with 20 repeats; read the evidence of a failed run; write the findings, the knowledge entry and the proposed fix.

Each script step: check it with `bash -n` and `shellcheck` if available; verify on a `workflow_dispatch` run before the next step only when the step changes the CI behavior (steps 3–4).

Files: `ImageGen/ImageGen.E2E.Common/ci/apple-sim-smoke.sh`, `.github/workflows/apple-ci.yml`, `docs/knowledge/apple-ci.md`.

### Verification
```
bash -n ImageGen/ImageGen.E2E.Common/ci/apple-sim-smoke.sh
bash scripts/check.sh
# GitHub Actions: apple-ci.yml via workflow_dispatch with ios_smoke_repeats=20 (links and failure counts in the Report)
```

## Report

## Review

## Comments
### 2026-11-11, owner (in the chat, recorded by claude-planner)
The smoke tests must always pass; continue the investigation instead of relaxing the criterion of 247. Plan approved ("create tasks for the fixes with plans"). The change of `.github/workflows/apple-ci.yml` is allowed for this task.

### 2026-11-12, codex-gpt6
The required `workflow_dispatch` run with 20 repetitions is still running. The Avalonia simulator build has passed, but its smoke job is queued on GitHub-hosted macOS capacity, so there are no repetition results to inspect yet. This task is blocked on runner availability; resume it when the smoke job runs and produces its artifacts. The plan and acceptance criterion remain unchanged.

### 2026-11-12, codex-gpt6
Runner capacity became available and both iOS smoke jobs started in run [43296911876](https://github.com/r-eliseev/studious-goggles/actions/runs/43296911876). The task is eligible again; inspect its artifacts when the run completes.

### 2026-11-13, codex-gpt6
The cancelled 20-repeat run [43296911876](https://github.com/r-eliseev/studious-goggles/actions/runs/43296911876) did upload partial artifacts for Avalonia runs 01–14 (14/20; the task report must not treat this as a complete failure rate). Evidence:
- Runs 09 and 11 have `crashReports=1`, but the only copied reports are `UsageTrackingAgent-*.ips`; their `app_name` is `UsageTrackingAgent`, so these are host macOS crashes rather than Avalonia crashes.
- Run 14 has `aliveAfter30s=false`, `crashReports=0`, and `exitedAtSeconds=5`. Its timeline says the process is absent through 45 seconds and present at 50 seconds. The app log says it starts at `2026-11-12 21:48:43.500`; simulator.log records the process receiving SIGTERM at `21:49:46.771`, at cleanup, with no app crash report. This suggests the smoke observed a delayed startup rather than a spontaneous app exit; the cause of the delay remains unknown.

The approved plan has a reproducible timeout mismatch: the previous 20-repeat job was cancelled after 14 runs and more than 90 minutes, while both smoke jobs have `timeout-minutes: 90` in `.github/workflows/apple-ci.yml`. At the observed pace, 20 runs cannot complete within that limit. The current retry, [43377652976](https://github.com/r-eliseev/studious-goggles/actions/runs/43377652976), is running the same 20-repeat plan and currently has both smoke jobs in progress.

Proposed plan amendment: allow increasing the smoke-job timeout to cover a 20-repeat run and emit per-repeat progress/partial evidence; change the diagnostic so a process not yet seen is distinct from one that exited, and count/report app-specific crash reports separately from unrelated macOS runner reports. Then complete the 20-repeat run and document whether the launch-delay pattern recurs. Until the planner resolves this plan mismatch, this task is blocked and its claim is released.

The follow-up run [43377652976](https://github.com/r-eliseev/studious-goggles/actions/runs/43377652976) was cancelled after the timeout mismatch was confirmed; it had not yet produced final artifacts. Its build jobs passed, but it does not count as a 20-repeat result.

### 2026-11-13, claude-planner — plan amendment
Answers the mismatch of 2026-11-13. Verified from the partial artifacts of run 43296911876: 14 runs took more than 90 minutes, the two `crashReports=1` runs were `UsageTrackingAgent` (a macOS host process), and run 14 is a launch delay (process first seen at 50 s), not an exit. Amended plan, within the same `Files:` line:
1. `apple-ci.yml`: the two smoke jobs get `timeout-minutes` from the input — 90 for one run, 240 when `ios_smoke_repeats` is more than 1 (an expression on `inputs.ios_smoke_repeats`). If one app's 20 runs still do not fit, run each app in a separate dispatch and record it.
2. `apple-sim-smoke.sh`: add `firstSeenAtSeconds` (the first sample with the process, or `null`) to `summary.json`; `exitedAtSeconds` is set only after the process has been seen. The process timeline keeps sampling to 60 s.
3. Crash reports: `crashReports` counts only reports whose process or `app_name` is the app's `CFBundleExecutable`; other reports are counted as `otherCrashReports` and copied to `crash-reports/other/`. The decision rule stays (alive at 30 s and no app crash report).
4. After each repeat, print one progress line (`run NN: alive30=…, firstSeen=…, exited=…`) and upload the artifact folder even when the job is cancelled or times out (the upload step needs `if: always()`; check the workflow and add it where missing).
5. Then the 20-repeat run for both apps and the findings as planned. A launch delay beyond 30 s counts as a failure of the smoke (the owner's criterion of 2026-11-11 stays); the Report states its frequency and, from `simulator.log`, what the system did before the first sample with the process.
Plan amendment approved (standing instruction of 2026-11-07: "approve all tasks that will come").
