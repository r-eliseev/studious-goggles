# Working with tasks

Rules for agents.

Tasks live in `tasks/<release|backlog>/NNN-name.md`. One task — one file: description, acceptance criteria, analysis, plan, report, comments. Several agents may work on the repository at the same time.

Section names. Task files are being translated to English (task 269). Until then the template `tasks/_template.md`, the scripts and the old task files keep the original section names; in the same order they mean: Description, Acceptance criteria, Analysis, Plan, Report, Comments; inside the Description the lines "Depends on" and "Environment", inside the Plan the lines "Files:" and "Verification". Use the names of the file you edit; task 269 switches the template, the scripts and all files at once.

## Executor name
Your name is the `AGENT_NAME` environment variable or the name the user gave. If there is none, invent a unique one, for example `claude-4f2a`. Names of different agents must not coincide.

## Starting work
1. Read the whole task file, including "Comments".
2. Check the "Depends on" line in the "Description": every task named there must be `done` in `main` (`TASKS_BRANCH=main scripts/list-tasks.sh --main done`) or recorded in `tasks/DONE.md` (the log of finished tasks, `scripts/archive-tasks.sh`). If at least one is not ready, do not claim the task: tell the user. The queue order is in `docs/status.md`, section "Order of work"; taking a task out of order is allowed only if its dependencies are ready and the user named it.
3. Check the "Environment" line in the task's "Description" (`docs/knowledge/dev-environment.md`). If your environment does not fit (for example, the task is "Windows only" and you are on Linux or in a cloud session), do not claim the task: tell the user. If there is no such line, the task runs on Linux or Windows, except the "Windows only" cases from `docs/knowledge/dev-environment.md` (running MAUI on Windows, `Platforms/Windows`, DPAPI).
4. See what others are doing. The task scripts work with the current branch, so other agents' claims are visible only through branches on origin: `git fetch --prune && git branch -r --list 'origin/task/*'`. A `task/NNN-*` branch on origin means someone is already working on task NNN: if it is not your branch, stop and tell the user. What is already merged is visible like this: `TASKS_BRANCH=main scripts/list-tasks.sh --main in-progress`. If the "Files:" line of the "Plan" of another in-progress task lists files you are going to change too, do not start: write a comment in your task and tell the user.
5. Create the task branch from a fresh origin/main and publish it right away: `git fetch && git switch -c task/NNN-name origin/main && git push -u origin task/NNN-name`. If the `task/NNN-name` branch already exists on origin and it is yours, continue in it (`git switch task/NNN-name`). If other agents work on this machine, use a separate worktree: `git worktree add ../wt-NNN -b task/NNN-name origin/main`.
6. Claim the task from the task branch: `scripts/claim-task.sh <number> <your-name>`, then `git pull --ff-only` (the script pushes the claim commit to origin and does not touch the working copy). Exit code 2 means the task is already claimed: stop and tell the user. Do not take another task yourself.

## What to do depending on the status
- `todo`: read the description and criteria, study the code and `docs/`. Fill in "Analysis" and "Plan" (with a `Files: ...` line in the plan), set `planned` and stop. Do not write code in `ImageGen/` (proof-of-concept code only in `poc/`, see below).
- `planned`: waits for a human review. Do nothing. If the user asks to fix the plan according to comments, fix the "Plan" and answer with a comment.
- `approved`: may be implemented. `claim-task.sh` sets `in-progress` itself.
- `in-progress`: continue the implementation.

## What to write in the "Plan"
- Only concepts, not finished code: which types and members appear and what they are responsible for, pseudocode of algorithms, state machines, data flows, edge cases, test cases in words (condition → expected result), verification commands, the `Files:` line.
- Names of types, members and files may and should be written: they must match `docs/architecture.md`. Do not paste method bodies, XAML markup or whole file contents into the task.
- If an unknown must be removed before the plan (will a dependency build for Android, what does an API answer, how does a component render), write proof-of-concept code and put it in separate files in `poc/<release>/<NNN-name>/` (rules in `poc/README.md`). The task holds only a link to the folder and the conclusion. Do not write or edit proof-of-concept code for renames, namespace changes, language version and similar small edits: describe the edit in words.
- Proof-of-concept code is a hint, not the source of truth: when implementing, check against the current code and the plan.
- Do not fix the number of a new migration in the plan: write `NNNN_<meaning>.sql`. The number is the next free one in `ImageGen/ImageGen.Data/Migrations/` at implementation time: otherwise parallel plans claim the same number.
- If the plan changes the same files or tables as another unfinished task, name it in the "Analysis" and propose an order.
- If part of the work builds or is checked only on Windows (`Platforms/Windows`, WinUI, DPAPI), move it to a separate task with the line "Environment: Windows only", and keep the rest in a "Linux or Windows" task. Then the main task is closed on Linux instead of hanging unfinished. The Android target does not belong to the Windows part: it builds on Linux with the Android SDK.
- Write the plan in English (`docs/conventions.md`, "Language").

## Approval
Approval of a plan is the `approved` status set by a human, or an explicit "plan approved" in the chat or in the comments (then set `approved` yourself). Do not write code without approval.

## Finishing
1. Run the tests and the linter.
2. Fill in the "Report": what changed, which files, how it was verified, what differs from the plan.
3. Update `docs/` if the behavior or structure diverged from the description: the specification, an ADR, `docs/architecture.md`. If it was done as described, do not touch `docs/`. Do not write progress into `docs/` ("task NNN added …", "not written yet"): it is visible from task statuses and "Reports".
4. Set the status `done` and release the claim: `scripts/release-task.sh NNN`.
   If everything that is checked by the build and tests in your environment passed, and only checks your environment lacks remain (building the MAUI Windows target — CI does it, running on Windows or macOS, a manual screen check, a live request with a key), do **not** leave the task `in-progress`: set `done`, list in the "Report" what was not checked and why, and add the manual items to the manual check task (016 — MAUI on Windows, 046 — Avalonia on the Linux desktop and Windows). A task stays `in-progress` only if the code is not ready or a check available in the environment fails.
5. Run `scripts/check.sh`.
6. `git fetch && git rebase origin/main`, then a pull request. In commit messages name the task file, for example: `001-add-login: add the login form`.

## Comments
- Append to the end of the "Comments" section as `### YYYY-MM-DD, <your-name>` followed by the text.
- Do not edit or delete other people's comments. An answer to a comment is a new comment.

## If something is unclear
Do not rewrite the "Description" and the "Acceptance criteria". Write the questions in "Comments", set `blocked` and stop. Keep the claim: a human decides whether to release it. After the answer, the human returns the task to `approved` (or `todo` if a new analysis is needed).

## New tasks
If you notice a problem outside the task's scope, do not fix it; create a new task `scripts/new-task.sh --publish <release|backlog> <name>` and mention it in the "Report". With `--publish` the task is pushed to the current branch right away; its number is the next after the tasks of this branch. In parallel branches the numbers may coincide: after `git rebase origin/main`, `scripts/check.sh` reports a duplicate id; then renumber your not yet merged task (the `id` field and the file name) with the next free number.

## Shared files
Make only the needed edits in shared `docs/` files, without reformatting. On a conflict during `rebase` keep both versions.

## Audit

An audit of documentation and code is done at the user's request, usually once a week or before a release. An audit does not change code: unambiguous discrepancies are fixed in the documentation, those requiring code become tasks, unclear ones become questions in `docs/status.md`. Every finding records its evidence: documentation and code files.

1. Run `scripts/audit.sh`; go through check errors, tasks claimed for a long time, tasks waiting for a human, stale documents and ADRs without a decision.
2. Compare the architecture, conventions and specifications with the code and tests; check the layers and prohibitions, ADR statuses, whether `AGENTS.md` is current and the open questions in `docs/status.md`.
3. Fix obvious documentation discrepancies. Turn the rest into tasks through `scripts/new-task.sh --publish backlog <name>` or into questions in `docs/status.md`.
4. Write the result to `docs/log/YYYY-MM-DD-audit.md` from the `docs/log/_template.md` template: scope, fixes, new tasks and unclear questions.
5. Automate recurring findings: for example, with architecture tests or checks in `scripts/check-docs.sh`.

## Helper agents

If the tool supports delegation, helper agents are useful for exploration, independent review and non-overlapping subtasks. Do not delegate tightly coupled work, small tasks or decisions that need an understanding of the whole task.

An assignment must contain the goal, the needed files and documents, the constraints and the expected answer format. Responsibility for the task stays with the main agent: only it claims the task and updates its plan and report. By default a helper works read-only; when edits are allowed, it gets non-overlapping files or a separate worktree. Verify its conclusions against the named files. Do not build chains of nested helpers.
