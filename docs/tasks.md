# Working with tasks

Rules for agents.

Tasks live in `tasks/<release|backlog>/NNN-name.md`. One task — one file: description, acceptance criteria, analysis, plan, report, review, comments. Several agents may work on the repository at the same time.

## Roles (ADR 0032)
Every agent works in one of two roles (ADR 0032, as clarified by ADR 0034). A clear user request to implement, continue, or finish approved tasks selects the **executor** role even without that exact role name. For planning, status review, approvals, blocked-task decisions, reviews and audits, use the **planner** role. Ambiguous requests default to planner; do not change `ImageGen/` unless execution was clearly requested.

- **Planner** — carries the weight: talks with the owner, writes specifications, ADRs and the scenario catalog, creates tasks, writes "Analysis" and "Plan" (with POCs in `poc/` when an unknown must be removed), records the owner's approvals, answers the executor's questions and amends plans, reviews finished tasks, archives, audits, keeps `docs/status.md`. The planner never writes product code in `ImageGen/`, never claims a task and never sets `in-progress` or `review`.
- **Executor** — carries out one approved task exactly as planned. Its complete rules are in `docs/executor.md`; the rest of this file is for the planner. The executor never writes or changes "Description", "Acceptance criteria", "Analysis" or "Plan", never sets `planned`, `approved` or `done`, and never decides anything that the plan does not say: it stops, writes the finding with a proposed plan change and sets `blocked`.

The planner's way of working — the session sync, questions to the owner, verifying facts, executor-proof plans, reviews, harness changes — is the `planner` skill, `.claude/skills/planner/SKILL.md`; every planner session uses it. The executor's session continuity and task execution rules are `.claude/skills/executor/SKILL.md` and `docs/executor.md`; every executor session uses them.

Plans are written for the executor: concrete steps, the exact files, the expected failing reason of each test, the verification commands. If an executor could ask "which way?", the plan is not finished.

## Statuses and who sets them
| Status | Meaning | Set by |
|---|---|---|
| `todo` | a task exists, no plan yet | planner, or executor for a finding (description + proposed plan in comments) |
| `planned` | analysis and plan written, waiting for the owner | planner |
| `approved` | the owner approved the plan (a "Plan approved" record in comments is required — `check-tasks.sh` checks it) | planner (recording the owner) or the owner |
| `in-progress` | claimed and being executed | executor (`scripts/claim-task.sh`, only from `approved`) |
| `blocked` | the executor stopped with a question and a proposed change; the claim is released | executor; the planner answers |
| `review` | the executor finished, the "Report" is filled, the claim is released | executor (`scripts/submit-task.sh`) |
| `done` | the planner checked the work against the plan and the criteria and filled "Review" | planner (`scripts/review-task.sh NNN done`) |
| `cancelled` | not needed any more | planner with the owner |

`scripts/check-tasks.sh` checks, against the base branch: the four planned sections stay unchanged while a task is executed (`approved`/`in-progress` → `in-progress`/`review`/`blocked`); `review` has a "Report" and no claim; a newly `done` task has a filled "Review"; a newly `approved` task has a new approval record.

## Review (planner)
1. Read the task's "Report" and the pull request diff. Check each acceptance criterion and each plan step against the diff; check that only the files of the "Files:" line changed and that the "Verification" commands were run (CI is green).
2. Write the "Review" section: `### YYYY-MM-DD, <planner-name>`, what was checked, the verdict, the follow-up tasks.
3. Accepted: `scripts/review-task.sh NNN done` (on the task branch before the merge, or in a follow-up change). Not accepted: write what to fix in "Comments" and `scripts/review-task.sh NNN approved`; the executor takes it again.
4. Unchecked manual items go to the manual check tasks (016, 046, 140, 198); they do not keep a task open.

## Answering a blocked task (planner)
Read the executor's finding and proposed change, check it against the code, decide with the owner, then amend the "Plan" (or the criteria, with the owner), write a comment with the decision and a "Plan amendment approved" record, and set `approved`. If the work must be re-analysed, set `todo`.

Section names. Task files are being translated to English (task 269). Until then the template `tasks/_template.md`, the scripts and the old task files keep the original section names; in the same order they mean: Description, Acceptance criteria, Analysis, Plan, Report, Comments (the "Review" section is English already); inside the Description the lines "Depends on" and "Environment", inside the Plan the lines "Files:" and "Verification". Use the names of the file you edit; task 269 switches the template, the scripts and all files at once.

## Agent name
Your name is the `AGENT_NAME` environment variable or the name the user gave. If there is none, invent a unique one, for example `claude-4f2a`. Names of different agents must not coincide.

## Starting work (executor; the short version is `docs/executor.md`)
1. Resume from durable state: sync the current branch and `origin/main`, inspect task/PR state and the task Report/Comments. Read the whole task file. If the user authorized a task pool, read the standing instruction in `docs/status.md`; continue the active task first, then choose only the next eligible task in that pool.
2. Check the "Depends on" line in the "Description": every task named there must be `done` in `main` (`TASKS_BRANCH=main scripts/list-tasks.sh --main done`) or recorded in `tasks/DONE.md` (the log of finished tasks, `scripts/archive-tasks.sh`). If a task is not ready, do not claim it; record/report the blocker and, when authorized, continue with the next independent eligible task. The queue order is in `docs/status.md`, section "Order of work"; taking a task out of order is allowed only if its dependencies are ready and it is within the user's authorization.
3. Check the "Environment" line in the task's "Description" (`docs/knowledge/dev-environment.md`). If your environment does not fit (for example, the task is "Windows only" and you are on Linux or in a cloud session), do not claim the task: tell the user. If there is no such line, the task runs on Linux or Windows, except the "Windows only" cases from `docs/knowledge/dev-environment.md` (running MAUI on Windows, `Platforms/Windows`, DPAPI).
4. See what others are doing. The task scripts work with the current branch, so other agents' claims are visible only through branches on origin: `git fetch --prune && git branch -r --list 'origin/task/*'`. A `task/NNN-*` branch on origin means someone is already working on task NNN: if it is not your branch, stop and tell the user. What is already merged is visible like this: `TASKS_BRANCH=main scripts/list-tasks.sh --main in-progress`. If the "Files:" line of the "Plan" of another in-progress task lists files you are going to change too, do not start: write a comment in your task and tell the user.
5. Create the task branch from a fresh origin/main and publish it right away: `git fetch && git switch -c task/NNN-name origin/main && git push -u origin task/NNN-name`. If the `task/NNN-name` branch already exists on origin and it is yours, continue in it (`git switch task/NNN-name`). If other agents work on this machine, use a separate worktree: `git worktree add ../wt-NNN -b task/NNN-name origin/main`.
6. Claim the task from the task branch: `scripts/claim-task.sh <number> <your-name>`, then `git pull --ff-only` (the script pushes the claim commit to origin and does not touch the working copy). Exit code 2 means the task is already claimed: stop and tell the user. Do not take another task yourself.

## What to do depending on the status
- `todo` (planner): read the description and criteria, study the code and `docs/`. Fill in "Analysis" and "Plan" (with a `Files: ...` line in the plan), set `planned` and stop. Do not write code in `ImageGen/` (proof-of-concept code only in `poc/`, see below). A `todo` created by the executor has a proposed plan in "Comments": use it as input, not as the plan.
- `planned` (planner): waits for the owner. If the owner asks to fix the plan according to comments, fix the "Plan" and answer with a comment.
- `approved` (executor): may be implemented. `claim-task.sh` sets `in-progress` itself.
- `in-progress` (executor): continue the implementation.
- `blocked` (planner): see "Answering a blocked task".
- `review` (planner): see "Review".

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
Approval of a plan is the `approved` status set by the owner, or an explicit "plan approved" by the owner in the chat or in the comments; then the planner records it as a comment `### YYYY-MM-DD, owner (in the chat, recorded by <planner-name>)` with the words "Plan approved" and sets `approved`. A standing instruction of the owner ("approve all tasks that will come") is recorded once in `docs/status.md` with its date and words; each task approved by it gets its own record `Plan approved (standing instruction of YYYY-MM-DD: "<words>")` (ADR 0033). Nobody writes code without approval.

## Finishing (executor)
1. Run the tests and the linter.
2. Fill in the "Report": what changed, which files, how it was verified, what differs from the plan.
3. Update `docs/` if the behavior or structure diverged from the description: the specification, an ADR, `docs/architecture.md`. If it was done as described, do not touch `docs/`. Do not write progress into `docs/` ("task NNN added …", "not written yet"): it is visible from task statuses and "Reports".
4. Submit for review: `scripts/submit-task.sh NNN` (sets `review` and releases the claim). The planner sets `done` after the review.
   If everything that is checked by the build and tests in your environment passed, and only checks your environment lacks remain (building the MAUI Windows target — CI does it, running on Windows or macOS, a manual screen check, a live request with a key), do **not** leave the task `in-progress`: submit it, list in the "Report" what was not checked and why, and add the manual items to the manual check task (016 — MAUI on Windows, 046 — Avalonia on the Linux desktop and Windows). A task stays `in-progress` only if the code is not ready or a check available in the environment fails.
5. Run `scripts/check.sh`.
6. `git fetch && git rebase origin/main`, then a pull request. In commit messages name the task file, for example: `001-add-login: add the login form`.

## Comments
- Append to the end of the "Comments" section as `### YYYY-MM-DD, <your-name>` followed by the text.
- Do not edit or delete other people's comments. An answer to a comment is a new comment.

## If something is unclear
Executor: do not rewrite the planned sections. Write the finding and a proposed plan change in "Comments", set `blocked`, release the claim (`scripts/release-task.sh NNN`) and stop (`docs/executor.md`, section 3). The planner answers, amends the plan with the owner and returns the task to `approved` (or `todo` if a new analysis is needed).
Planner: write the question in `docs/status.md` ("Open questions") or ask the owner; do not guess.

## New tasks
If you notice a problem outside the task's scope, do not fix it; create a new task `scripts/new-task.sh --publish <release|backlog> <name>` and mention it in the "Report". An executor fills only the "Description" (facts and evidence) and a "Proposed plan" comment and leaves `todo`; the planner writes the analysis and plan. With `--publish` the task is pushed to the current branch right away; its number is the next after the tasks of this branch. In parallel branches the numbers may coincide: after `git rebase origin/main`, `scripts/check.sh` reports a duplicate id; then renumber your not yet merged task (the `id` field and the file name) with the next free number.

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
