# 0034. Executor session continuity and standing task-pool authorization

Status: accepted
Allowed statuses: proposed, accepted, superseded by NNNN, rejected
Date: 2026-11-11

## Context

The owner asked the executor to continue approved and returned tasks across messages and sessions, work one task at a time, update the existing pull request after each task, and not wait for merges or pause after routine progress updates. The executor rules were written for a single explicitly named task and a single session. A fresh session could therefore default to the planner, lose the scope of the standing authorization, or stop after reporting a blocker even when independent work remained.

The recent task 243 session also showed that durable state must be reconstructed from `origin/main`, task branches and reports, PR merge state/checks, and test artifacts. Conversation summaries alone did not show that PR #96 had merged, nor did a green check alone explain the Android locator failure; the screenshot and UI tree did.

## Decision

1. **Select the role from intent.** A clear request to implement, continue, or finish approved repository tasks selects the executor even when the user does not say “executor”. Planning, task-state inspection, approvals, reviews, and audits select the planner. Ambiguity defaults to planner.
2. **Persist task-pool authorization.** When the owner authorizes a pool (for example, all approved tasks, including tasks returned to `approved`), record its scope and date in `docs/status.md`. It remains active across messages and sessions until withdrawn or changed. The executor does not ask for routine reconfirmation and never expands the pool itself.
3. **Process one task at a time.** Resume an active owned task first. After it is submitted for review and its PR is updated, choose the next eligible task from the authorized pool in dependency/queue order. Respect task dependencies, environment, claims, remote task branches, and file overlap. A blocker stops that task; record/release it according to the lifecycle, then continue an independent eligible task if authorized. Stop only when no eligible task remains or a decision blocks the whole pool.
4. **Recover state from the repository.** At session start, sync and inspect the current branch, `origin/main`, task Reports/Comments, remote task branches, PR merge state/checks, and relevant CI logs/artifacts. If a PR merged, start subsequent work from fresh `origin/main`. Record progress, evidence, run links and the next action in the task so another session can resume without chat history.
5. **Honor PR batching instructions.** When the owner asks to keep work in one existing PR and not wait for merges, update that PR after each task while preserving one task branch/claim per task. If the PR merges, sync `origin/main` before the next task and follow the owner's instruction for the next batch.
6. **Keep ub-mode evidence-led.** Use a meaningful failing behavior test first when practical; do not write a misleading test when it is not. After repeated failures based on the same premise, test that premise before trying another fix. For CI/UI failures, inspect logs and artifacts before changing code.

## Alternatives

- Require the exact role name and a new authorization in every session. Rejected: it contradicts the owner's repeated instruction to keep the task work moving and makes continuity depend on chat history.
- Stop the entire session whenever one task is blocked. Rejected: unrelated eligible tasks can still make progress without weakening task dependencies or ownership checks.
- Store all progress in a separate handoff note. Rejected: task Reports/Comments, branches and PR checks are already the authoritative work records and stay attached to the code.

## Consequences

- A new executor session can resume work from repository state and a recorded authorization rather than requiring the full previous conversation.
- The executor may move between authorized tasks, but may not bypass blockers or take tasks outside the recorded scope.
- Work remains reviewable per task, even when several task branches are collected in one owner-requested PR.
- Harness checks require both planner and executor skills to remain present.
