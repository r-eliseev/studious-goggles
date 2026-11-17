# Research 01: public "software factory" skills and kits

Date: 2026-11-17. Answers Q8 from `docs/factory/notes/01-tsf-lesson-2.md` (the TSF skill itself was not found; these are public analogues of the pattern "one skill / one command installs a factory into a repository").
Method: GitHub repository pages were read through a page-to-text tool; only what the page/README states is reported (no source code was read, no installs were run). "Page does not show" means exactly that, not that the thing is absent. Star counts and activity are as of the fetch.

## Comparison

| Repo | Form | Runtime | Who orchestrates | Human gate | License |
|---|---|---|---|---|---|
| [disler/super-simple-software-factory](https://github.com/disler/super-simple-software-factory) (SSSF) | one Claude Code skill `sssf`, stamped into a repo | Python scripts (`adws/adw_*.py`), agent runner `pi` | deterministic code ("Agent proposes, code disposes") | the operator runs workflows; no merge step | MIT |
| [riquenandez/ts-factory](https://github.com/riquenandez/ts-factory) | the same skill, TypeScript/Bun port of SSSF (per search snippet) | Bun, no npm deps in stamped code | deterministic TS scripts | operator runs workflows; no sandbox or merge step | MIT |
| [dwightfowler-tema/factory-setup](https://github.com/dwightfowler-tema/factory-setup) | skill that installs, repairs, upgrades a factory | factory lives in the target repo (`factory/bin/*`); skill only plants it | skill + `factory_setup.py` | n/a (installer); factory has a builder gate hook | GPL-3.0 |
| [PaddyMc/software-factory](https://github.com/PaddyMc/software-factory) | Python CLI `factory` (+ skills, init) | CLI, SQLite, sandboxed git worktrees (bubblewrap, Linux) | CLI with 4 agents | owner merges the PR | not shown |
| [juan-campuzano/minifactory](https://github.com/juan-campuzano/minifactory) | Python package: engine + CLI + YAML workflows + plugins | `minifactory run`, per-run state files | declarative workflow engine | PRs for existing repos; asks before running | MIT |
| [geut/factory-skills](https://github.com/geut/factory-skills) | Pi package: TypeScript controller + 4 skills | `/factory` command, HTTP server, SQLite | TypeScript controller ("skills describe specialist behavior only") | human merges, then `/factory complete` | not shown |

None is confirmed to be the TSF skill from source 01. TSF was not mentioned on any of these pages.

## Per repository

### disler/super-simple-software-factory (SSSF)
- **Link:** https://github.com/disler/super-simple-software-factory (1.1k stars, 282 forks; owner `disler`, page signs off as IndyDevDan and links the Tactical Agentic Coding course and his YouTube channel).
- **What it is:** "a software factory as a single Claude Code skill". Central claim: "Deterministic Python owns the graph. Coding agents are bounded nodes inside it."
- **Components:**
  - skill `.claude/skills/sssf/` (`SKILL.md`, `cookbooks/`, `references/`, `scripts/`, `apps/visualizer/`, `templates/`);
  - config `adws/adw_sssf_config/sssf.config.yaml`: the agent roster (model, thinking level, prompts, tools, `writes` paths); five starter agents: planner, builder, scout, reviewer, documenter;
  - prompts `adws/adw_data/prompt_engineering/{agent}/system.md` and `user.md`;
  - 12 starter workflows `adws/adw_*.py` (`adw_plan`, `adw_build_test`, `adw_simple_sdlc`, ...), logic in `adws/adw_modules/` (`quality.py`, `gates.py`);
  - trace: SQLite WAL `adws/adw_data/sssf.db`; read-only Vue/Vite visualizer served by Bun.
- **How it works:** a workflow creates/joins a session (`adw_id`); each phase is an `agent` phase (agent returns a JSON envelope checked by gates) or a `code` phase (tests, commit). Failed parse or gate triggers a correction in the same session. Writes outside an agent's `writes` list are rolled back and the phase fails. Events go to SQLite, the visualizer polls it. Install: copy the skill, `/sssf install` (or `uv run .../install.py`), set `.env`, `just demo`; re-running skips existing files, `--force` overwrites.
- **Limits stated:** `quality.py` commands are placeholders that exit 0 (tests are not meaningful until wired); `claude_code` coding agent is stubbed (v1 runs Pi only); runs use the current branch, no sandbox, no per-run branches, no merge step. The `example` branch shows a stamped factory.

### riquenandez/ts-factory
- **Link:** https://github.com/riquenandez/ts-factory
- **What it is:** "A reusable software factory for any repo": deterministic TypeScript "ADWs" control sequencing, retries and acceptance; agents work inside named phases. Bun runtime. Search snippet describes it as a TypeScript/Bun port of SSSF.
- **Components:** skill `.claude/skills/sssf/` (`SKILL.md`, `scripts/install.ts`, `scripts/makeAdw.ts`, `references/{handoff,config,observability,exec-protocol}.md`); stamped files `adws/adw_*.ts`, `adws/adw_modules/{quality,gates}.ts`, `adws/adw_sssf_config/sssf.config.yaml`, prompts per agent, `adws/adw_data/sssf.db` (7 tables), `justfile`, `.env.sample`; trace UI at `localhost:4601`.
- **How it works:** phases default to failure and must earn success; typed JSON envelopes (a parse failure re-prompts the same session); gates (`artifactsExist`, `diffMatchesClaims`) verify claims against the repo; per-agent `writes:` rules and `protected_files` enforced by diffing the repo around each call, unauthorized changes rolled back; `/sssf` operator skill launches workflows and reports, does not edit app code. Install: copy skill, `bun .claude/skills/sssf/scripts/install.ts`, set `.env`, `just demo`.
- **Limits stated:** current branch, no sandbox, no merge step; a missing API key fails only when that agent runs.

### dwightfowler-tema/factory-setup
- **Link:** https://github.com/dwightfowler-tema/factory-setup
- **What it is:** a Claude Code skill that "installs, repairs and upgrades the software factory in any git repo you start `claude` in". README says `payload/` is derived from ZazenCodes' `software-factory-claude-code-v2` (that repo was not found, see "Unresolved").
- **Components:** `SKILL.md` (mode selection, rules); `INSTALL.md` (judgment-heavy procedure per mode); `scripts/factory_setup.py` (stdlib-only deterministic work: preflight, copy, manifest, gitignore, AGENTS.md section, labels, `config get|set`, check detection, sabotage verification, upgrade classification); `payload/` (generic factory files); `templates/` (per-stack `checks.sh`, AGENTS.md section); `tests/smoke.sh`; `VERSION`.
- **How it works:** `/factory-setup` runs init if nothing is installed, else upgrade; modes `init`, `checks` (re-derive `factory/checks.sh` and verify it can actually fail), `upgrade`. The payload is copied verbatim; `VERSION` and a manifest of file hashes are stamped into the target so `upgrade` can tell untouched files from tailored ones. Runtime stays in the target repo (`factory/bin/*`, a builder hook at `$CLAUDE_PROJECT_DIR/factory/bin/gate`). `labels` and `upgrade-apply` are dry runs until `--yes`. "This skill only plants and maintains it."
- **Notable for us:** the closest to the "init / configure" idea in source 01 (separate installer, manifest-based upgrade, check that the check can fail).

### PaddyMc/software-factory
- **Link:** https://github.com/PaddyMc/software-factory (50 commits, 14 open PRs at fetch time)
- **What it is:** a Python CLI: `factory run <plan>` turns each task contract in a plan into a pull request to review and merge. Four agents (planner, builder, reviewer, verifier) work per task in a sandboxed git worktree; a PR opens only after deterministic checks pass; agents do not push, merge or see secrets, so merging is the approval step.
- **Components:** `factory/` package, `.factory/` config, `.claude/skills/` and `.agents/skills/` (contents not shown), `docs/architecture.md`, `docs/reference.md`, `AGENTS.md`; plans are fenced YAML task contracts (default `docs/plans/`); `factory validate-plan`, `factory sync`, `factory status --refresh`, `factory top` (terminal dashboard), `factory web` (board on 127.0.0.1:8765); state in `$FACTORY_HOME` (`factory.db`, `worktrees/`, `logs/`, `transcripts/`, `cache/`); `/plan-workstream` command helps write plans.
- **How it works:** plan -> validate -> sync to DB -> per task a worktree on branch `factory/<task>` -> agents -> deterministic gates -> PR; a task whose dependency is not merged builds on the dependency's open PR (stacking). `factory init` writes `.factory/config.yaml`, skills, a starter plan and an `AGENTS.md` section; idempotent, `--force` overwrites. Linux-only prerequisites (`uv`, `git`, `gh`, bubblewrap); sandbox is offline.
- **Notable for us:** the closest structural match to this repository's own task flow (task contracts, branch per task, human merge as approval).

### juan-campuzano/minifactory
- **Link:** https://github.com/juan-campuzano/minifactory (alpha; engine, CLI and three example workflows tested per README)
- **What it is:** "A tiny, pluggable software factory": declarative YAML workflows of plugin steps; each AI agent step bounded by deterministic checks.
- **Components:** `src/minifactory/core/` (engine, validation, registry, events, expressions); `plugins/builtin/` (steps, providers, routing); ports `scm`, `llm`, `secrets`, `source`, `executor`, `router`, `intake`; built-ins for GitHub/local git, Bedrock/OpenAI, `jev` and `keyword` routers; workspace = `minifactory.yaml` + `workflows/`; skills copied to `.minifactory/skills/`.
- **How it works:** `minifactory init my-factory` -> `workflows copy` (templates such as `flutter-app-factory`) -> `validate` -> `run` (each job writes `state.json`, `result.json`, `events.jsonl` under `.minifactory/runs/`). The format supports sequencing, `when`, `on_fail`, `blocking: false`, `${...}` templating, deliberately no loops or arithmetic. "The core never imports a plugin; plugins register themselves"; unknown YAML keys are errors. `minifactory ask "..."` routes a plain-language request to a workflow and asks before running. `skills add <path|URL>` shows each `SKILL.md` and asks before copying; only text is used, nothing executed. Existing repos receive PRs, never writes to the default branch.
- **Notable for us:** closest to "liquid": the factory is data (YAML) plus plugins, changeable without touching the core.

### geut/factory-skills
- **Link:** https://github.com/geut/factory-skills
- **What it is:** a [Pi](https://pi.dev) package that "runs one ticket from planning through work, review, and wrap-up to a human handoff". "Coordination lives in a TypeScript controller. Skills describe specialist behavior only." "Budget-aware" refers (per the page) to bounded retries and rounds in `FACTORY.json`.
- **Components:** skills `factory-plan`, `factory-qa` (optional, end-to-end tests per acceptance criterion, screenshots), `factory-review` (read-only reviewer: approve / changes_requested / blocked), `factory-wrapup` (handoff, optional PR) and a work agent; `src/extension/index.ts` (`/factory`), `src/server/` (HTTP, owner lease), `src/controller/` (commands, reducer, SQLite), `src/actions/` (dispatcher, worktrees), `src/validation/`, `openapi.json`; `.factory/FACTORY.json`; Herdr (one pane per agent); GitHub (`gh`) and Linear adapters.
- **How it works:** `/factory start <ticket>` freezes the source and creates a worktree -> planner writes `plan.md`, task files, `CONTEXT.md` -> work agent implements, controller reruns `validation.work.commands` and resumes the worker with exact failures -> optional QA -> review verdict checked against the worktree -> wrap-up validated -> human merges and runs `/factory complete`; PR comments can re-enter via `/factory feedback`. Skills return only a versioned template and "do not write the controller database".
- **Budget controls (from the page):** `limits.reviewRounds` 3, `reviewCorrections` 3, `qaCorrections` 2, `qaRounds` 3, `feedbackRounds` 3, `infrastructureRetries`; launch failures never spend a correction or review round; models split by role (`models.work`, `.review`, `.plan`).
- **Notable for us:** the clearest example of budgets (rounds/corrections per stage) and of "skills = behavior, controller = state".

## Patterns common to all six

1. **Code owns the graph, agents own a bounded step.** Sequencing, retries and acceptance are deterministic (SSSF, ts-factory, minifactory, geut); agents are not trusted with them.
2. **Claims are verified, not believed.** Gates compare agent claims to the repository (ts-factory `diffMatchesClaims`), checks that must be able to fail (factory-setup), controller re-runs validation (geut).
3. **Per-agent write permissions** enforced by diffing and rollback (SSSF family).
4. **State in SQLite plus a trace UI** (SSSF, PaddyMc, geut): everything observable.
5. **Human gate = merge** in the PR-based kits (PaddyMc, minifactory, geut); the SSSF family has none yet.
6. **init is idempotent**, `--force` overwrites, manifest/version enables upgrade (SSSF, factory-setup, PaddyMc).

## Unresolved

- ZazenCodes' `software-factory-claude-code-v2`: **resolved by the code reading** (`02-factory-kits-code-reading.md`, section 2): per factory-setup's `NOTICE` it is the folder `src/software-factory-claude-code-v2` in `https://github.com/zazencodes/zazencodes-season-3`, commit `5fc7c983f44d623057ef65361f25cf23cab2abef`, GPLv3. A SkillsMP page lists a ZazenCodes `/factory` skill (`/factory <feature>`, `next`, `approve <job-id>`, `rework <job-id> <note>`); that host was not reachable.
- Relation of "Tiny Software Factories" (source 01) to SSSF: both are "factory as a skill + template", both link to IndyDevDan's material, but the SSSF page does not mention TSF. Still a hypothesis.
- Source code is now being read: see `02-factory-kits-code-reading.md`.
