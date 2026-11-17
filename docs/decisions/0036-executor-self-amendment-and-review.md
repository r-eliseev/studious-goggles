# 0036. The executor may amend and close its own tasks under standing instructions

Status: accepted
Allowed statuses: proposed, accepted, superseded by NNNN, rejected
Date: 2026-11-17

Refines ADR [0032](0032-planner-and-executor-roles.md) and ADR [0034](0034-executor-session-continuity.md).

## Context
ADR 0032 split the roles: the planner records approvals, amends plans and reviews; the executor only executes. On 2026-11-15–17 executor sessions, citing the owner's instruction to keep working in one branch and not to wait for merges, changed `AGENTS.md`, `docs/tasks.md`, `docs/executor.md`, `.claude/skills/executor/SKILL.md` and `scripts/check-tasks.sh` (commits `da0bbc0`, `7e0abb0`). After that change the executor may edit the protected sections of its current task when its Comments add a `Plan amendment approved` record. The executor then approved its own amendments (274, 282) and wrote the Review and set `done` for 281 and 282. No ADR recorded the change.

## Decision
The owner confirmed it on 2026-11-17 (in the chat, answering the planner: "Yes, keep it"):
1. Under the standing instructions of 2026-11-07 ("approve all tasks that will come") and 2026-11-11 (ADR 0034), the executor may record a plan amendment approval for its current task and change that task's protected sections, as `scripts/check-tasks.sh` now allows. It must cite the standing instruction in the record. Other tasks' sections stay protected.
2. The executor may write the Review and set `done` for its own task.
3. Batches of tasks may share one branch and PR. After a merge, the branch is synced to `origin/main`.
4. The planner still answers questions that need the owner, plans new work, archives, audits and keeps `docs/status.md`. It reviews finished work at the owner's request or in its state reviews. It may reopen a self-closed task with a finding.

## Alternatives
- Restore the planner-only approvals and reviews of ADR 0032 — not chosen by the owner.
- Keep batches but leave reviews to the planner — not chosen.

## Consequences
- Faster flow: an executor is not blocked waiting for a planner on plan mismatches it can resolve.
- Less independent checking: self-reviewed work (281, 282) and self-approved amendments are reviewed only afterwards. The planner's state review should look at merged PRs with red checks and at self-closed tasks.
- `scripts/check-tasks.sh` accepts an amendment record written in the same branch; the checker no longer separates roles for the current task.
