---
id: 249
status: done
assignee:
claimed:
created: 2026-11-07
---

# macOS: runner GUI preflight, launch two .app files and, if possible, Mac2 UI tests

## Description
Stage P5 of the Apple addendum (ADR 0030, section 7, requirement 7 in `docs/specs/apple-ci-e2e.md`): on `macos-26`, determine whether a GUI session, meaningful screenshot, and Accessibility permission are available; launch Avalonia's `Omnilite.app` and MAUI Mac Catalyst (if task 245 built it), and save evidence. Add UI tests (Appium Mac2 4.6.1) only if preflight succeeds; otherwise close the task with a recorded blocker rather than green tests.

Depends on: 245 (artifacts `avalonia-macos`, `maui-maccatalyst`).
Environment: GitHub Actions `macos-26`.

## Acceptance criteria
- Script `ImageGen/ImageGen.E2E.Common/ci/apple-macos-preflight.sh` writes to `reports/macos-preflight/`:
  1. Session: `who`, `launchctl print gui/$(id -u) | head -20` (whether a GUI domain exists), `system_profiler SPDisplaysDataType` (resolution).
  2. Screenshot: `screencapture -x desktop.png`; check that it is “not uniform” using the same rule as 238 (the ratio of pixels more than 16 away from the mean is greater than 1%), with `python3` and no third-party packages: convert with `sips -s format bmp` and parse BMP manually, or use `python3 -I` with `struct`.
  3. Accessibility: run `osascript -e 'tell application "System Events" to get name of every process whose background only is false'`; record success or error text (error `-1743` / “not allowed assistive access” means permission is missing).
  4. `summary.json`: guiSession, resolution, nonUniformRatio, accessibilityOk.
- Launch each available `.app` artifact: unpack it, run `open -n <app>` (Avalonia) and likewise for Catalyst; after 15 seconds, check the process is alive (`pgrep -f`), capture `screencapture -x app-<shell>.png`, collect crash reports from `~/Library/Logs/DiagnosticReports`, then close it (`osascript -e 'quit app "Omnilite"'`, then `pkill`). If Catalyst is “blocked” in 245, skip it and record that.
- Job `smoke-macos` (`continue-on-error: true`, `workflow_dispatch` + `schedule`) uploads artifact `macos-e2e-smoke` for any outcome.
- If `accessibilityOk` and `nonUniformRatio` > 1%, do not create a UI test project in this task; record a proposal for a new “macOS UI tests with Mac2 4.6.1” task in `docs/status.md` with evidence (the owner decides whether to create it). Otherwise, record a blocker in `docs/knowledge/apple-ci.md` (“hosted macOS: no Accessibility permission / blank frame”) and do not create macOS UI tests.
- Record results from three runs in `docs/knowledge/apple-ci.md`.

## Analysis
Reference: the owner's addendum is `docs/specs/ui-e2e-original/MAUI_Avalonia_Apple_CI_E2E_Addendum.md`; ADR 0030 and `docs/specs/apple-ci-e2e.md` take precedence if they differ.
ADR 0030, task 238 (the “not uniform” rule and Windows script as an example), `docs/knowledge/apple-ci.md`.

## Plan
1. Add the preflight script; check BMP parsing locally with generated uniform and noisy BMP files in `tmp/`.
2. Launch `.app` files and add the job.
3. Run three times and record the conclusion.

Files: `ImageGen/ImageGen.E2E.Common/ci/apple-macos-preflight.sh` (new), `ImageGen/ImageGen.E2E.Common/ci/image-nonuniform.py` (new), `.github/workflows/apple-ci.yml`, `docs/knowledge/apple-ci.md`, `docs/status.md`.

### Verification
```
bash -n ImageGen/ImageGen.E2E.Common/ci/apple-macos-preflight.sh
python3 -I ImageGen/ImageGen.E2E.Common/ci/image-nonuniform.py tmp/solid.bmp
python3 -I ImageGen/ImageGen.E2E.Common/ci/image-nonuniform.py tmp/noisy.bmp
bash scripts/check.sh
# GitHub Actions: smoke-macos and artifact
```

### Execution order (for Luna)
- First read only the files in the “Files” line and the named tests; compare signatures with current `main`.
- Follow the steps in order (ub-mode): failing test → run it and confirm it fails for the described reason → minimal fix → green test.
- Do not change files outside the “Files” line, rename existing members beyond the plan, or fix neighboring findings; those belong to separate tasks.
- If the code differs from the plan (missing method, different signature, test already exists), do not improvise; stop and write a question in “Comments”.
- Run `dotnet` commands one at a time, not in parallel. After each changed project, run `dotnet format <project> --verify-no-changes`.
- In the “Report”, record what changed, commands and results, and differences from the plan.

## Report

### 2026-11-10, codex-gpt6
- Three consecutive scheduled `smoke-macos` runs succeeded: [42465262394](https://github.com/r-eliseev/studious-goggles/actions/runs/42465262394), [42596262790](https://github.com/r-eliseev/studious-goggles/actions/runs/42596262790), [42794177774](https://github.com/r-eliseev/studious-goggles/actions/runs/42794177774). In each run the GUI session was available, `accessibilityOk=true`, `nonUniformRatio` was about 0.999, and there were no crash reports or app failures.
- Mac2 conditions were met: a proposal for a separate Mac2 4.6.1 UI testing task was added to `docs/status.md`; the decision remains with the owner.

### 2026-11-07, codex-gpt6 (в работе)
- The first automatic smoke on `main`, run [42381579325](https://github.com/r-eliseev/studious-goggles/actions/runs/42381579325), stopped before checking GUI apps: `sips -s format bmp` produced a 32-bit BMP with `BI_BITFIELDS` (`compression=3`), while the parser accepted only `compression=0`.
- Added support for uncompressed 32-bit BMP with BGRA RGB masks and checked it against a saved runner screenshot: `nonUniformRatio=0.9991188`; the standard 24-bit `solid.bmp` and `noisy.bmp` fixtures also pass.
- Added ad-hoc signing and strict `.app` verification before `open` in `smoke_app`, because neighboring iOS smoke runs found the same unsigned simulator bundle signature issue. The next CI run should check the macOS GUI and both launched apps.
- Run [42386890196](https://github.com/r-eliseev/studious-goggles/actions/runs/42386890196): the screenshot was meaningful (`nonUniformRatio=0.9991404`), both apps remained running and produced screenshots, and there were no crash reports. The job failed with `app_failures[*]: unbound variable` because `set -u` was enabled and the array was empty; fixed array access by using an empty default. Hosted confirmation is pending.

## Comments

### 2026-11-07, claude-audit
Created from the Apple addendum: stage P5. Accessibility permissions on hosted macOS were unknown, so Mac2 UI tests were conditional on the preflight and a separate owner decision. Plan for Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
The owner's “MAUI + Avalonia — Apple CI/E2E” addendum (original: `docs/specs/ui-e2e-original/`). The plan was approved under the earlier instruction “approve all tasks with plans”. The owner explicitly requested the new `apple-ci.yml` workflow as part of this addendum (ADR 0030).

### 2026-11-07, codex-gpt6
Run 42381579325 failed on converted `desktop.bmp`: the BMP header uses compression `BI_BITFIELDS` and 32-bit BGRA masks, a valid `sips` output format. The parser now accepts it. The next run will check that GUI preflight continues and launches the app smokes.
