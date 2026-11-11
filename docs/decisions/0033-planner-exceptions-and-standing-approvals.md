# 0033. Planner: red main, CI experiments, standing approvals, artifact reviews, cloud sessions

Status: accepted
Allowed statuses: proposed, accepted, superseded by NNNN, rejected
Date: 2026-11-11

## Context
ADR 0032 split the roles and the `planner` skill (`.claude/skills/planner/SKILL.md`) describes how the planner works. The owner asked to compare the skill with a long planner session of 2026-11-07 — 2026-11-08 (UI E2E and Apple CI, tasks 235–251, PRs #84–#90). The session needed five things the skill does not cover:
1. `main` was red and the owner asked the planner to fix it ("MAUI is fixed, Avalonia is left"). The planner changed product code (`ImageGen/ImageGen.Avalonia.iOS/Views/MainView.axaml.cs`, the font links in the Avalonia iOS and Android project files) and files listed in the "Files:" lines of tasks 246 and 247, which were in progress for an executor. Under ADR 0032 the planner does not change `ImageGen/`.
2. The cause was found by experiments: temporary edits of `apple-ci.yml` on the planner's branch, each run with `workflow_dispatch` (runs 42402076497, 42403798934, 42404807310, 42405774295). This is a proof of concept, but it cannot live in `poc/`.
3. The owner gave a standing instruction ("approve all tasks that will come") and every new task was recorded as approved by referring to it. The skill and `docs/tasks.md` describe only an approval per task.
4. The planner checked UI tests by their CI artifacts: videos cut into frames (`docs/knowledge/ui-e2e.md`, "How to watch the videos and screenshots of runs"). That found tests that pass without showing what they claim (task 251).
5. Cloud sessions refuse force pushes; a merged pull request was followed by new work on the same designated branch, and PRs were watched through a subscription with a delayed check-in.

The owner's decision of 2026-11-11: add all five to the skill.

## Decision
1. **Red `main`.** The default is a diagnosis task for the executor (as task 274), then a fix task planned from its findings. The planner changes product code only when the owner explicitly asks for it in the chat. Then the planner:
   - first checks the "Files:" lines of in-progress tasks; when the fix touches them, writes a comment in that task before the change and names the task in the pull request;
   - keeps the change to what the failure needs;
   - records the owner's request and the evidence (runs, logs) in the affected task's comments and in the pull request.
2. **CI experiments.** The planner may run `workflow_dispatch` on its own branch with temporary workflow edits to test a hypothesis. Each experiment is recorded in the task: the run link, what changed, the result. A temporary edit is reverted or becomes a planned change before the pull request.
3. **Standing approvals.** A standing instruction of the owner is recorded once: a line in `docs/status.md`, "Open questions" or the order of work, or an ADR for a wide one, with its date and words. Every task approved by it still gets its own record, `Plan approved (standing instruction of YYYY-MM-DD: "<words>")`, so `scripts/check-tasks.sh` keeps working. The owner can withdraw it at any time.
4. **Artifact reviews.** A review of UI, E2E or CI work looks at the CI artifacts: screenshots, videos cut into frames, logs. The goal is to confirm that each test shows what it claims. The method is in `docs/knowledge/ui-e2e.md`.
5. **Cloud sessions.**
   - Force pushes are refused. After the branch's pull request is merged, the planner restarts the branch from `origin/main` and pushes it only when the old head is an ancestor (a fast-forward). Otherwise it merges `origin/main` into the branch.
   - A finished pull request is watched with the PR subscription and one delayed check-in. The check-in is cancelled when the PR is green and mergeable, or merged.

## Alternatives
- **Keep the planner out of `ImageGen/` with no exception.** Cleaner, but a red `main` that the owner wants fixed now would wait for a full plan–execute–review round.
- **One approval record for many tasks.** Shorter, but `check-tasks.sh` would need a new rule, and an executor reading one task would not see that it is approved.
- **CI experiments only through a POC task.** Correct but slow for a hypothesis that one run answers.

## Consequences
- The planner's code changes stay rare and visible: they need the owner's words, a comment in the affected task and a note in the pull request.
- Standing approvals are traceable from each task back to one dated instruction.
- Reviews of UI work cost more time (downloading artifacts, frames) and catch tests that pass for the wrong reason.
