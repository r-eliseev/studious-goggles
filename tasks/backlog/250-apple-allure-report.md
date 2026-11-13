---
id: 250
status: approved
assignee:
claimed:
created: 2026-11-07
---

# Apple CI: Allure report for Apple platforms

## Description
Apple addendum (ADR 0030, section 8, `docs/specs/apple-ci-e2e.md`): the `apple-report` job in `apple-ci.yml` collects `allure-results` from iOS tests (and macOS tests if added) into one HTML report, keeps platform and app labels separate, and includes a run summary. The approach and script come from task 242.

Depends on: 242 (`allure-summary.sh`, generation), 248.
Environment: GitHub Actions; locally, run `scripts/check.sh` and verify the script with samples.

## Acceptance criteria
- Job `apple-report` (`ubuntu-latest`, `needs` all Apple test/smoke jobs, `if: always()`): download available artifacts (`continue-on-error` for each download), merge `allure-results` into `reports/apple-allure-results`, generate the report with `allure-commandline@2.46.1` (Temurin 21), and upload the `apple-allure-html` artifact (14 days).
- Labels: each result has `parentSuite` = `ios-maui` / `ios-avalonia` / `macos-<shell>` (set by task 248 fixtures through `AllureApi`); verify that groups are separate in the generated report.
- `$GITHUB_STEP_SUMMARY`: a table grouped by `parentSuite` with passed / failed / skipped counts (`allure-summary.sh` from 242, extended with backward-compatible grouping by `parentSuite`), `blocked` rows for iOS/Catalyst builds based on task 245 results (job outputs `outputs.blocked`), and a link to the run.
- If there are no results, show a “no results” summary and exit 0.
- In `docs/specs/apple-ci-e2e.md`, set the status to “implemented” if tasks 244–250 are complete and the criteria are met; otherwise list the outstanding items in the “Report”.

## Analysis
Reference: the owner's addendum is `docs/specs/ui-e2e-original/MAUI_Avalonia_Apple_CI_E2E_Addendum.md`; ADR 0030 and `docs/specs/apple-ci-e2e.md` take precedence if they differ.
Task 242 (`allure-summary.sh`, `report` job), `.github/workflows/apple-ci.yml` (after 245–249).

## Plan
1. Add grouping by `parentSuite` to `allure-summary.sh` with TDD using samples in `tmp/` (two `*-result.json` files with different `parentSuite` values).
2. Add `blocked` outputs to the build jobs; add the `apple-report` job; run it.
3. Update the documentation.

Files: `ImageGen/ImageGen.E2E.Common/ci/allure-summary.sh`, `.github/workflows/apple-ci.yml`, `ImageGen/ImageGen.E2E.iOS/IosTestBase.cs` (label, if task 248 did not add it), `docs/specs/apple-ci-e2e.md`, `docs/knowledge/apple-ci.md`.

### Verification
```
bash ImageGen/ImageGen.E2E.Common/ci/allure-summary.sh tmp/allure-apple-sample
bash scripts/check.sh
# GitHub Actions: apple-report, apple-allure-html artifact, summary
```

### Execution order (for Luna)
- First read only the files in the “Files” line and the named tests; compare signatures with current `main`.
- Follow the steps in order (ub-mode): failing test → run it and confirm it fails for the described reason → minimal fix → green test.
- Do not change files outside the “Files” line, rename existing members beyond the plan, or fix neighboring findings; those belong to separate tasks.
- If the code differs from the plan (missing method, different signature, test already exists), do not improvise; stop and write a question in “Comments”.
- Run `dotnet` commands one at a time, not in parallel. After each changed project, run `dotnet format <project> --verify-no-changes`.
- In the “Report”, record what changed, commands and results, and differences from the plan.

## Report

## Comments

### 2026-11-07, claude-audit
Created from the Apple addendum (A8). Plan for Luna.

### 2026-11-07, owner (in chat, recorded by claude-audit)
The owner's “MAUI + Avalonia — Apple CI/E2E” addendum (original: `docs/specs/ui-e2e-original/`). The plan was approved under the earlier instruction “approve all tasks with plans”. The owner explicitly requested the new `apple-ci.yml` workflow as part of this addendum (ADR 0030).
