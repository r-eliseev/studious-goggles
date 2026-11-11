# 2026-11-11: task status audit

## Done
- Compared task statuses on `main` with the active task branch, open PRs, remote task branches, and `scripts/audit.sh` output.
- Recorded task 235 as `blocked` and cleared its stale claim: its task branch already documented a plan mismatch, while `main` still showed `approved`.
- Refreshed the task queue in `docs/status.md` for current blocked and active tasks.
- Confirmed PR #101 is the only open PR; tasks 255 and 256 are represented on its branch.

## Decisions and reasons
- Task 235 stays blocked until its plan resolves how catalog refresh reports failures and where Web startup invokes it. No implementation should start under the current plan.
- Task 255 stays blocked because SES-07 needs configurable fake chat models outside its approved file list. Task 276 remains `todo` and depends on 255.

## Not finished
- Task 256 is actively in progress in PR #101; its CHAT scenarios and bindings are uncommitted work in the shared worktree.
- PR #101 checks were still running during this audit. No failed check was reported at that point.
- The task 235 status change is in PR #101 and is not reflected on `main` until it merges.

## Points of attention
1. Owner decision is still needed to amend task 255 so SES-07 can configure a fake model catalog; until then 255 remains blocked.
2. Task 235 needs a plan amendment covering the catalog-service result/logging contracts and the Web `StartupInitializer`.
3. Recheck PR #101 checks after they finish; `docker-smoke` and `harness-check` were green while build and Apple/E2E checks were running.
