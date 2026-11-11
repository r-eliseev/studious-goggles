# Executor: the complete rules

Use the executor role when the user clearly asks to implement, continue, or finish approved repository tasks, even if they do not use the word “executor” (ADR 0034). The executor carries out one approved task exactly as planned. Everything that needs judgment — scope, design, specifications, decisions with the owner — belongs to the planner. When in doubt, stop and ask; do not decide.

At the start of every executor session, load `.claude/skills/executor/SKILL.md`; read `AGENTS.md`, this file, `docs/tasks.md`, `docs/index.md`, `docs/status.md`, and the current task file (all of it, including "Comments"). Sync the current branch, `origin/main`, task branches and the associated PR before deciding what to do. Before writing C# read `docs/conventions.md` and the relevant knowledge files.

## Session continuity and standing authorization

- The task card, task branch, GitHub PR/checks and `docs/status.md` are the durable record. Do not depend on the previous chat transcript to recover state. At each meaningful step, add the evidence and next step to the task's Report or Comments.
- A current user request may authorize a defined pool, such as all approved tasks or approved tasks returned for changes. When that instruction is recorded in `docs/status.md`, it remains active across messages and new sessions until withdrawn. Select only from that pool, one task at a time, in dependency/queue order. Do not ask for routine reconfirmation.
- Continue the current task first. A task returned to `approved` with planner comments is eligible again: claim it and address those comments exactly. A task in `review` is waiting for the planner; do not change its status yourself.
- When a task is blocked by a dependency, another worker, environment, missing credential, or a genuine plan mismatch, stop that task and record the facts. Release its claim as required by the status procedure, then continue with another independent eligible task in the authorized pool. Stop the session only when no eligible task remains or a decision blocks the whole authorized pool.
- Treat later user messages as steering for the active objective, not as a replacement, unless the user clearly cancels or changes it. Progress updates are not a reason to pause the work. Ask only for a materially necessary decision or missing input.
- After each task, finish its checks, fill its Report, run `scripts/submit-task.sh`, and update the requested PR. If the user asked to keep work in one existing PR and not wait for merges, add each eligible task's finished commits to that PR and continue; preserve one task branch and claim per task. If that PR is merged, sync `origin/main` before starting the next task and follow the user's PR instruction for the next batch.

## 1. Taking a task
1. Work on the task the user named or, when a standing instruction is recorded, select the next eligible task from that exact authorized pool. Never expand the pool on your own.
2. The task's status must be `approved`. Any other status — stop and tell the user.
3. Every task in the "Depends on" line must be `done` in `main` or listed in `tasks/DONE.md`. Otherwise do not claim it: record the blocker, tell the user, and consider the next independent task in the authorized pool.
4. The "Environment" line must fit your environment (`docs/knowledge/dev-environment.md`). Otherwise stop and tell the user.
5. `git fetch --prune && git branch -r --list 'origin/task/*'`: if a `task/NNN-*` branch of your task exists and is not yours, stop. If another `in-progress` task lists the same files in its "Files:" line, stop and tell the user.
6. `git switch -c task/NNN-name origin/main && git push -u origin task/NNN-name` (or continue your own existing branch).
7. `scripts/claim-task.sh NNN <your-name>`, then `git pull --ff-only`. Your name is `AGENT_NAME` or the name the user gave. Exit code 2 — someone else has it: stop.

## 2. Doing the work
- Follow the "Plan" literally, step by step, in its order. Do not add features, refactorings, renames or "improvements" the plan does not name.
- Change only the files of the "Files:" line. A file you need that is not listed — stop (section 3).
- ub-mode for every code change: a failing test first when an honest behavioral test is practical; run it and check that it fails for the reason the plan describes; then the minimal code; then the test is green. Do not write a misleading test when it is impractical; record the reason and use the narrowest meaningful verification. If multiple fixes based on one premise fail the same gate, stop and test that premise before another fix. Small commits: `NNN-name: <what>` with the reason and how it was verified.
- Run `dotnet` commands one at a time. After each changed project: `dotnet format ImageGen/<Project>/<Project>.csproj --verify-no-changes`.
- Check names and signatures against the current code. Proof-of-concept code in `poc/` is a hint, not the source of truth.
- Never edit the "Description", "Acceptance criteria", "Analysis" or "Plan" sections: `scripts/check.sh` fails if you do. You write only "Report" and "Comments".
- Do not edit ADRs, specifications, `docs/architecture.md`, `AGENTS.md` or `scripts/` unless the "Files:" line names them.
- Do not add packages, migrations or settings the plan does not name.
- Everything you write is in English.

## 3. When something does not match
The plan does not match the code (a missing member, another signature, a test that already exists), a step cannot be done, a file outside "Files:" is needed, a criterion contradicts another, the specification and the code disagree — in all these cases:
1. Stop work on this task. Do not improvise and do not choose an option yourself.
2. Write a comment in the task: what you found (files and lines), what blocks you, and a **proposed plan change** (concrete steps and files). The planner decides with the owner.
3. Set `status: blocked`, release the claim (`scripts/release-task.sh NNN`), commit and push the branch, tell the user. If a standing task-pool authorization is active, then continue with the next independent eligible task; otherwise end the session for this task.

A problem outside the task (a bug you noticed, a flaky test, a missing check): do not fix it. Create a task with `scripts/new-task.sh --publish backlog <name>`; keep `status: todo`; fill only the "Description" with facts and evidence (files, lines, commands, output) and add a comment "Proposed plan" with the steps you would take. Mention the task number in your "Report". The planner writes the real analysis and plan.

## 4. Finishing
1. All tests and format checks named in the task's "Verification" pass; `bash scripts/check.sh` passes.
2. Fill in the "Report": what changed (files), the commands and their results, what was not checked and why (for example a Windows-only or manual check — add those items to the manual check task, 016 for MAUI on Windows, 046 for Avalonia desktop), any difference from the plan (there should be none; if there is, it must have been approved in "Comments").
3. `scripts/submit-task.sh NNN`: it sets `review` and releases the claim. Do not set `done` yourself: the planner reviews and closes the task.
4. `git fetch && git rebase origin/main`, `bash scripts/check.sh`, push, and update or open the pull request according to the user's instruction. Watch all checks; investigate failures from logs/artifacts before fixing. When the user authorized a pool, continue to the next eligible task after this task is submitted and the PR is updated.
5. If the planner sends the task back (`approved` with comments), claim it again and fix exactly what the comments say.

## 5. Comments
Append to the end of "Comments" as `### YYYY-MM-DD, <your-name>` and the text. Never edit or delete other comments.
