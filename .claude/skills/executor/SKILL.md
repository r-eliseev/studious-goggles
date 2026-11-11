---
name: executor
description: Execute, continue, or finish approved repository tasks in the Omnilite harness. Load for clear implementation requests, including when the user authorizes a standing pool of tasks.
---

# Executor

Use this skill with `AGENTS.md`, `docs/executor.md`, `docs/tasks.md`, and the selected task. The harness is authoritative when it differs from the general workflow below.

## Resume before acting

At the start of every executor session, sync the repository and reconstruct state from durable sources, not chat memory:

1. Check the current branch, uncommitted changes, `origin/main`, task branches, the task card and its latest Report/Comments, and the related pull request's merge state and checks.
2. If the current task's PR was merged, start the next task from fresh `origin/main`. If it remains open, continue its branch when that task is still authorized. Never discard or switch away from uncommitted work.
3. Read the task pool authorization in `docs/status.md` when the user says to keep processing tasks. Follow its scope until the user withdraws or changes it.

## Keep working across turns

- Treat a new user message as steering for the active objective unless they clearly cancel or replace it.
- Do not stop after a progress message, a completed task, or a routine CI wait. Send concise progress updates while continuing the work.
- Finish the current task before selecting another. After submission, update the designated PR as requested, then continue with the next eligible task in the authorized pool without waiting for a merge when the user said not to wait.
- Use one task at a time. Respect its dependencies, environment, ownership and exact plan. A task branch held by someone else or an overlapping active task is not eligible.
- If a task is blocked, record the evidence and proposed plan change, set `blocked`, release its claim, and report the blocker. Stop work on that task; continue with another independent eligible task if the standing authorization allows it. If none remain, report the concrete blockers and stop.
- Ask only for a decision, approval, credential, or missing fact that materially blocks the next action. Do not ask again for standing authorization already recorded.

## Implement and verify

- Follow the approved plan and ub-mode: write a behavior test first when an honest test is practical, confirm it fails for the expected reason, then make the smallest fix. Do not add a misleading test just to satisfy TDD; record why a behavioral test is impractical.
- Work in small verifiable units. If multiple fixes based on the same premise fail the same gate, state and test that premise before trying another fix.
- For a failing CI check, inspect its logs and relevant artifacts before changing code. For UI/E2E tests, inspect screenshots, recordings and UI trees to confirm the test passed for the claimed reason.
- Record progress, CI run links, results, blockers and the next actionable step in the task Report/Comments so a fresh session can resume without this conversation.
- After every task: run its verification and `scripts/check.sh`, submit it for planner review, and update the requested PR. Do not mark it `done` yourself.

See [docs/executor.md](../../../docs/executor.md) for the full task procedure and [the upstream ub-mode skill](https://github.com/unclebob/ub-mode/blob/master/SKILL.md) for the reduced principles.
