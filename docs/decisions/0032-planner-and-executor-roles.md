# 0032. Planner and executor roles

Status: accepted (2026-11-10)
Allowed statuses: proposed, accepted, superseded by NNNN, rejected
Date: 2026-11-10

## Context
Two kinds of agents already work on the repository in practice: one plans with the owner (analysis, plans, reviews, audits — Claude sessions), another carries out approved plans (codex-gpt6 / GPT Luna). The rules in `docs/tasks.md` did not separate them: any agent could write a plan, set `done`, or improvise when the plan did not match the code. Plans already carried a repeated "Execution order (for Luna)" block. Recent examples: an executor stopped with a good proposal on task 255; another found a catalog error (task 272) — both had to be decided by the owner, not by the executor.

The owner's decisions of 2026-11-10: introduce two roles, planner and executor; the role is given by the user's words; a `review` status between execution and closing; the executor only proposes plans — "make the executor as simple as possible and move all the weight to the planner"; try enforcing the rules with scripts ("I do not believe in scripts, but let's try").

## Decision
1. **Roles.** The user names the role at the start of a session. Without a named role the agent is the planner and does not change `ImageGen/`.
   - The planner owns everything that needs judgment: specifications, ADRs, the scenario catalog, tasks, "Analysis" and "Plan", recording approvals, answering blocked tasks and amending plans with the owner, reviews, archiving, audits, `docs/status.md`. The planner does not write product code and does not claim tasks.
   - The executor carries out one approved task literally. Its complete rules are one self-contained file, `docs/executor.md`. It writes only "Report" and "Comments". On any mismatch it stops, writes the finding with a proposed plan change and sets `blocked` (releasing the claim). A problem outside the task becomes a `todo` task with facts and a "Proposed plan" comment; the planner writes the real plan.
2. **Lifecycle.** `todo → planned → approved → in-progress → review → done`, plus `blocked` and `cancelled`. Who sets which status is a table in `docs/tasks.md`. The template gets a "Review" section filled by the planner.
3. **Scripts.**
   - `scripts/claim-task.sh` claims only `approved` tasks.
   - `scripts/submit-task.sh NNN` (executor): `in-progress` → `review`, requires a filled "Report", releases the claim.
   - `scripts/review-task.sh NNN done|approved` (planner): closes a `review`; `done` requires a filled "Review".
   - `scripts/check-tasks.sh` compares every task file with the base branch: the "Description", "Acceptance criteria", "Analysis" and "Plan" sections do not change while a task is executed (`approved`/`in-progress` → `in-progress`/`review`/`blocked`); `review` has a "Report" and no claim; a newly `done` task has a "Review"; a newly `approved` task has a new "Plan approved" record (except sending a reviewed task back). Tasks archived in the base are skipped. Regression: `scripts/tests/task-roles.sh`, run by `scripts/check.sh`.

## Alternatives
- **Role from an environment variable (`AGENT_ROLE`) or from the agent's name.** Scripts could then refuse actions by role. Rejected by the owner: the role is given in words; the scripts check the effects (sections, statuses, records) instead of the actor.
- **No review step.** Simpler, but the executor would close its own work; mistakes would be found only at an audit.
- **The executor writes full plans for its findings.** Faster for small fixes, but splits the judgment between two roles; the owner chose to keep it with the planner.
- **Documentation only.** Cheaper, but the existing rules were already broken without anyone noticing; the scripts make the most important rules visible in CI.

## Consequences
- The planner has more work per task (a reviewable plan, a review); the executor needs less context and fewer decisions.
- A finished task is not `done` until the planner reviews it; the release notes (`scripts/release-notes.sh`) list a task only after the review.
- The scripts cannot know who acted: an executor that edits the "Review" section or a planner that sets `review` is not detected. The checks catch the common mistakes (an edited plan, a missing report, a missing review, an unrecorded approval), not a determined violation.
- The repeated "Execution order (for Luna)" blocks in plans become optional: `docs/executor.md` holds the general rules, a plan holds only task-specific notes.
