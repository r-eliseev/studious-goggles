---
id: 274
status: review
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

Implemented the diagnostics in the three planned files. The script now measures elapsed time from launch with a monotonic clock, records each query's actual time and status, bounds `launchctl list` probes, and keeps screenshot capture from blocking the timeline. The workflow supports repeated fresh simulator runs and preserves evidence on cancellation.

Verification:
- `bash -n ImageGen/ImageGen.E2E.Common/ci/apple-sim-smoke.sh` — passed.
- Embedded Python syntax parse and `bash scripts/check.sh` — passed.
- `shellcheck` is not installed in the local environment.
- PR #114 checks: harness, UI E2E, Android, Windows build, core, Avalonia, Docker and other non-Apple checks passed. `windows-tests` failed in the acceptance step (task 282); the two iOS build jobs failed with a hosted .NET workload manifest conflict in run [44041619896](https://github.com/r-eliseev/studious-goggles/actions/runs/44041619896).
- After isolating the .NET SDK/workload installation under `$RUNNER_TEMP/dotnet` for the Apple jobs that install workloads, the Apple workflow passed in [44088295597](https://github.com/r-eliseev/studious-goggles/actions/runs/44088295597). Its log confirms `DOTNET_INSTALL_DIR=/Users/runner/work/_temp/dotnet`, SDK `10.0.302`, and only the `10.0.100` SDK manifest directory before installing workload `10.0.302.0`.
- The full PR #114 check set on commit `06c0ecb6ad0e2fca08aebec6af1b44e5c39a388b` passed on 2026-11-17: harness, Apple builds/preflight, Linux core/Avalonia/Avalonia Android/Docker, Windows tests and build, MAUI Android/Windows, Android and Windows UI E2E, and Allure report. Apple smoke jobs were skipped as configured for this PR event; the separate 20-repeat diagnostic evidence remains the completed run [44040174321](https://github.com/r-eliseev/studious-goggles/actions/runs/44040174321) documented above.
- The required 20-repeat manual run completed for both apps in [44040174321](https://github.com/r-eliseev/studious-goggles/actions/runs/44040174321), using the compatible Xcode 26.4.1 / .NET 10.0.203 option. It reported 11/20 MAUI and 9/20 Avalonia smoke failures, with zero app crash reports. Treat these as failed smoke observations, not confirmed app exits: the failed runs frequently timed out in `launchctl list`.

Representative evidence from Avalonia run 05:
```
process timeline: 10.505s no / launchctl timed out; 19.118s no / timed out; 24.192s no / timed out; 29.207s no / timed out
launch.log: com.eliseev.imagegen.avalonia: 22808
app log: 2026-11-17 08:08:10.940 ... starting in the Avalonia iOS shell
simulator.log: RunningBoard lookupHandleForPredicate activity; no app termination record
```
The process and app log show that the app started, while the observer did not successfully query it during the 30-second window. In MAUI run 09, the app log started at 08:32:01Z and `launch.log` reported PID 42190, but `launchctl` first returned the process at elapsed 46.681s after timing out on earlier samples. Other runs had no app log or process during the 60-second window, so slow or missing launch remains possible. No collected report identifies an app crash or confirms a post-launch exit. The strongest hypothesis is Simulator launch/query contention on the hosted runner, compounded by `launchctl list` timing out; this does not establish the cause of the original app exit.

The scheduled `main` Apple run [44067539154](https://github.com/r-eliseev/studious-goggles/actions/runs/44067539154) passed both iOS builds and both single-run iOS smoke jobs. The earlier manifest conflict did not recur, but the 20-run results above still fail the task's smoke criterion.

Proposed follow-up fix (not implemented here): add an independent per-process liveness check using the PID emitted by `simctl launch`, keep `launchctl list` as diagnostic evidence, then repeat the 20×2 run. If the app process is genuinely absent or starts after 30 seconds, create a separate app/runner investigation based on that evidence. No app code was changed in task 274.

## Review

## Comments
### 2026-11-17, codex-20261117
Returned for follow-up because PR #114's Apple workflow still fails on the current branch. Runs [44086334311](https://github.com/r-eliseev/studious-goggles/actions/runs/44086334311) and [44087284039](https://github.com/r-eliseev/studious-goggles/actions/runs/44087284039) fail during iOS/Mac Catalyst workload installation. The detailed log from run [44040174321](https://github.com/r-eliseev/studious-goggles/actions/runs/44040174321) shows the same conflict: `Microsoft.NET.Runtime.Emscripten.Node.net10` from `/Users/runner/.dotnet/sdk-manifests/11.0.100` conflicts with the .NET 10.0.110 manifest, although the job requests SDK 10.0.302 and workload set 10.0.302.0. Scheduled main run [44067539154](https://github.com/r-eliseev/studious-goggles/actions/runs/44067539154) passed, so this is an intermittent hosted-runner manifest contamination.

Follow-up within the approved `apple-ci.yml` scope: set `DOTNET_INSTALL_DIR` to a per-job isolated path under `${{ runner.temp }}` before `actions/setup-dotnet` in the Apple jobs that install workloads, then verify the SDK/manifests path and rerun the PR Apple workflow. Keep the supported toolchain set and smoke criteria unchanged. This uses the setup-dotnet v4 documented install-directory environment variable; the owner’s standing approval for approved tasks covers this in-scope correction.
Plan approved (standing instruction of 2026-11-07: "approve all tasks that will come").

### 2026-11-13, codex-20261113
Finding from the first dispatch run [43440159572](https://github.com/r-eliseev/studious-goggles/actions/runs/43440159572): both simulator jobs completed, but their pass/fail checkpoint is not trustworthy. In Avalonia run 15, `process-timeline.txt` reports the 30-second sample at `15:49:10Z`, while `app-logs/imagegen-20261113.log` records app startup at `15:48:57.991Z`; the first timeline sample is at `15:48:12Z`. Samples nominally 5 seconds apart can be separated by 25–87 wall-clock seconds (30→35 is 87 s). The script increments `elapsedSeconds` by the loop index, so `firstSeenAtSeconds=30` and `aliveAfter30s=true` can actually describe a process first observed about 58 seconds after launch. This invalidates both 20-repeat series as verification of the owner’s 30-second rule; those results must not be reported as 20/20 valid passes. The second dispatch [43464158467](https://github.com/r-eliseev/studious-goggles/actions/runs/43464158467) also had `build-avalonia-ios` cancelled and no app artifact; it is cancelled while its MAUI smoke was still running.

Proposed plan amendment: measure elapsed time from the actual launch timestamp using a monotonic clock, record real elapsed seconds and each probe’s timestamp/duration in `process-timeline.txt`, and set `firstSeenAtSeconds`/`exitedAtSeconds` from observed elapsed time rather than the loop counter. Preserve the rule that the app must be observed alive by 30 real seconds; a late first observation is a smoke failure. Add a regression check for delayed probes (a probe that returns after 30 seconds cannot make `aliveAfter30s=true`). Then rerun the full 20 repetitions for both apps twice and report the accurate result and logs. Keep the same files and do not edit app code. Needs planner review/approval before resuming.

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


### 2026-11-16, codex-20261116
Owner instruction: take over task 274 and continue in the existing shared branch without waiting for a merge. The old remote task branch has no open PR and was last updated on 2026-11-13; it is blocked by an elapsed-time mismatch documented in its comments. The approved plan amendment in the current card/main resolves that mismatch: 240-minute timeout for repeated runs, distinct first-seen/exited timestamps, app-only crash counting, progress lines, and always-uploaded evidence. Continuing that work here.

### 2026-11-17, codex-20261116 — owner-approved implementation clarification
The 20-repeat dispatch [43825048969](https://github.com/r-eliseev/studious-goggles/actions/runs/43825048969) completed with one MAUI failure and three Avalonia failures. Its timeline labels are still loop counters, not elapsed time: for example, MAUI run 05 has the sample labelled 30 s at `08:09:03Z`, but the app log starts at `08:09:25Z`; Avalonia run 13 has the sample labelled 30 s at `09:06:53Z`, while the app log starts at `09:07:28Z`. The intervals between samples also exceed a minute in places. This is a self-block in the diagnostic and cannot verify the actual 30-second rule. The owner directed continuation of self-blocked work in the same branch. This clarification implements the existing 30-second criterion using monotonic time, bounds each probe, and prevents screenshot capture from blocking sampling. No app code is changed.
Owner-approved continuation in the same branch (owner instruction in chat); standing task-pool approval recorded 2026-11-07.

### 2026-11-17, codex-20261116
The corrected dispatch [44039721757](https://github.com/r-eliseev/studious-goggles/actions/runs/44039721757) did not reach either smoke: both iOS build jobs failed while installing the MAUI workload because the .NET 11.0.100 Emscripten manifest conflicts with the .NET 10.0.110 manifest. Avalonia/macOS, Catalyst and Apple preflight passed. Retried the required 20 runs via the existing diagnostic toolchain option `ios_toolchain=26.4.1` / .NET 10.0.203 in [44040174321](https://github.com/r-eliseev/studious-goggles/actions/runs/44040174321); both iOS builds passed and both smoke jobs are running. The failed first dispatch does not count as a reproduction.
