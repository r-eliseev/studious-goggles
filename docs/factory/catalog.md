# Catalog of factory mechanisms

Universal catalog (principle P1 in `docs/factory/README.md`): every mechanism found in the sources, whether or not it applies to this repository's harness. Source codes: **S01** TSF lesson 2, **S02** Warp talk, **S03** owner's dialog (T.. = thread in the walkthrough), **S04** Anthropic always-on agents; kits from `research/02-factory-kits-code-reading.md`: **SSSF** (disler/super-simple-software-factory, ts-factory is its TypeScript port), **FS** (factory-setup), **PM** (PaddyMc/software-factory), **MF** (minifactory), **GE** (geut/factory-skills). Descriptions are condensed from the notes; web-sourced facts keep the labels used there.
Applicability to this repository's planner/executor harness: **exists** (already in place), **candidate** (worth proposing), **partial**, **n/a here** (does not apply to this harness, kept for universality), **unknown** (not assessed). The assessment is a hypothesis until task 291/288 reviews it.

## 1. Lifecycle and workflow

| ID | Mechanism | What it is | Source | Here |
|---|---|---|---|---|
| C01 | Lifecycle loop | idea, triage, spec (if complex), implementation, review (agent first, then human), verification, release, monitoring, results back to intake | S02 W3 | partial (no triage, monitoring, feedback to intake) |
| C02 | Triage agent | easy and unambiguous issues go straight to implementation, hard ones get a spec | S02 W4 | unknown |
| C03 | Product spec plus tech spec | invariants of the product separate from architecture and shape of code | S02 W5 | partial |
| C04 | Machine-validated task contract | fixed keys, id regex, dependency cycle check, verification-command linter, placeholder ban, status stored outside the plan | PM | partial |
| C05 | Stacked tasks | a task depending on an open PR builds on its candidate; restack and conflict state when dependencies merge | PM | n/a here (batches share a branch) |
| C06 | Reshapeable flow by config | grammar `[plan]? build+ (review\|gates)* [verify]?`, gates step always present | PM | n/a here |
| C07 | Declarative workflows as validated data | YAML steps, `when`, `on_fail`, `blocking`, templating, no loops by design, unknown keys are errors, forward references rejected | MF | partial |
| C08 | Operator skill | a thin skill that launches workflows and observes, never implements, does not volunteer state | SSSF | partial |
| C09 | Phase defaults to fail | a phase succeeds only on clean exit; acceptance of the run separate from phase success | SSSF | candidate |
| C10 | Plan accepted from files, not from the reply | agent output text is not the source of truth | GE | exists |
| C11 | Versioned stage output templates | each stage returns a template (e.g. `factory.review.v5`) validated by the controller | GE | partial |
| C12 | Test-first step before the build | planned in PM, not built | PM | unknown |
| C13 | Pipeline spec as directed graph with LLM-evaluated transitions | phases (parse_spec, generate_code, run_scenarios, evaluate) | S03 T3 (Infralovers, secondary) | unknown |

## 2. Runtime primitives (always-on)

| ID | Mechanism | What it is | Source | Here |
|---|---|---|---|---|
| C20 | Event inbox | durable list of events written by handlers, read by one worker; status per event; dedupe by source id; batching; inbox is the source of truth | S04 | partial (task files and claim scripts) |
| C21 | Timers | follow-ups (set by the agent) and schedules (recurring) in their own durable store; a scheduler fires due timers into the inbox | S04 | n/a here today (no timers) |
| C22 | Memory store | durable, separate from the worker; holds only what cannot be looked up; pointers instead of copies | S04 | partial (docs, status, reports) |
| C23 | Dreaming | reviewing past sessions to curate memories, replace outdated entries, find cross-run patterns | S04 | n/a here (closest: `scripts/audit.sh`) |
| C24 | Outbox and dispatcher | outgoing messages table, pacing, error recovery; tune the number of messages with evals; start quiet and earn attention | S04 | n/a here |
| C25 | Deferred tool call | call suspended, run ends, session saved mid-call, human answers as an inbox event that resumes the session; worker free meanwhile | S04; GE typed pause | partial (status `blocked` plus Comments) |
| C26 | One active worker | a lease or claim so two runs never duplicate work | S04; GE server lease; this harness claim scripts | exists |
| C27 | Pure reducer plus transactional outbox | state changes by events with idempotency keys; side effects through an outbox | GE | n/a here |
| C28 | Run and session model | a run inside a session; replies resume the same session; model change starts a new one | S04; SSSF agent map; GE | unknown |

## 3. Verification

| ID | Mechanism | What it is | Source | Here |
|---|---|---|---|---|
| C30 | Holdout scenarios | end-to-end user stories kept outside the repository and invisible to the agent, validated by an LLM judge | S03 T5; StrongDM | candidate |
| C31 | Satisfaction metric | fraction of trajectories through scenarios that likely satisfy the user; hard invariants stay binary and blocking | S03 T5; StrongDM | unknown |
| C32 | Digital twins | agent-built behavioral clones (fakes) of external services, checked against the live dependency until no differences are found | S03 T2; StrongDM | n/a here |
| C33 | Two test sets | visible set for debugging, hidden set for acceptance, graded feedback, rotation, attempt limits | S03 T5 (synthesis) | unknown |
| C34 | Independent check source | different model, role or context; different truth (replay, historical bugs, reference implementation, external contracts); different method (property, fuzz, mutation, N-version) | S03 T5 | partial |
| C35 | Human arbiter | decides when implementation and scenario disagree; decisions become precedents that calibrate the LLM judge | S03 T5 | partial (planner review) |
| C36 | Check quality measurement | mutation testing on changed code, sampling reviews with the rule of three, seeded known bugs to calibrate the reviewer | S03 T3, T13 | unknown |
| C37 | Checks that can fail | run checks twice in clean worktrees, then once with an injected violation that must fail; fail on skipped or deselected tests | FS | candidate |
| C38 | Inconclusive is failure | zero tests selected counts as failure; stuck detector for repeated identical failures | PM | candidate |
| C39 | Mechanical verdict verification | reviewer must cite `path:line` that resolves in the worktree, cover the exact expected criterion ids, differ in model, use read-only tools, take rules from the base commit; diff-hash freshness | GE | partial |
| C40 | Deterministic gates decide | the model reviewer cannot override the gates; PR opens only when gates pass | PM | partial |
| C41 | Gate from the main checkout | the check script comes from the trusted checkout and protected paths cannot be edited by the change | FS; SSSF | candidate |
| C42 | Typed envelopes with self-consistency gates | JSON output validated by schema; corrections fed back in the same session, bounded | SSSF | n/a here (markdown outputs) |
| C43 | Claim gate against the real diff | compare claimed changed files with the git diff (a kit's version only checked that files exist: counter-example) | SSSF family | candidate |
| C44 | Rules into checks | turn "this must not change" into compile-time or linter checks with a passing and a failing fixture | S03 T10 | candidate |
| C45 | Production verification | synthetic user journeys, business-invariant alarms, shadow traffic, canary rollout with auto-rollback | S03 T10; AWS docs | n/a here (no product runtime) |
| C46 | UI verification by computer use | the agent drives the product and records videos and screenshots | S02 W7 | partial (E2E on Apple CI) |

## 4. Permissions and safety

| ID | Mechanism | What it is | Source | Here |
|---|---|---|---|---|
| C50 | Write boundary by tree diff | snapshot before, diff after, roll back disallowed paths, per-role `writes` and protected files (known holes: committed changes, skipped on earlier failure, same-count edits) | SSSF family | candidate |
| C51 | OS sandbox | bubblewrap, read-only root, cleared environment, task database invisible to the agent | PM | n/a here (cloud sandbox of the session) |
| C52 | Credential proxy | credentials injected by a proxy outside the agent's environment; the agent never holds them | S04 | n/a here (the session runtime does it) |
| C53 | Auto mode | classifiers review higher-risk actions; human review kept for the most important decisions | S04 | n/a here (runtime feature) |
| C54 | Least privilege and enforcement by the system | allowlists over denylists, secrets out of reach, branch or worktree isolation; rules in prompts are not enforcement | S03 T8; Claude Code docs | partial |
| C55 | Per-role models and tools | reviewer on a different model with read-only tools | GE | unknown |
| C56 | Trust boundary for inbound text | issue text reaches agents only as quoted data; a label is the trust boundary | FS | unknown |
| C57 | State outside the agent | agents cannot change task state; a controller or single-writer script does | PM; GE; FS | partial |
| C58 | Blast radius of state | deletion protection, retained stateful resources, backups outside the workload's lifecycle and account, scoped tokens | S03 T10, T12 | n/a here |

## 5. Human involvement

| ID | Mechanism | What it is | Source | Here |
|---|---|---|---|---|
| C60 | Mode per action, task class or repository | the human mode is a property of the action or area, not of the person or the factory | S03 T3; task 287 | candidate |
| C61 | In, near and out of the loop | modes of human participation; near the loop = agents run unattended and stop only at decision points | S01 C2 | partial |
| C62 | Decision points | the agent returns a result needing attention instead of asking continuously | S01; S04 | partial |
| C63 | Risk-based review routing | humans enter code review by risk; reviewers chosen by risk and surfaces | S02 W6; FS route | unknown |
| C64 | Understanding-debt practices | predict before reading, agent quizzes the owner, architecture diff, bounded change size, manual incident analysis | S03 T9 | candidate |
| C65 | Pre-mortem and assumptions | "imagine it failed, why" before starting; the agent lists assumptions before acting | S03 T11 (D4, D5) | candidate |
| C66 | Interviewer agent | extracts tacit knowledge by asking the owner concrete questions | S03 T7 (D3) | unknown |
| C68 | Commander's intent and trust earned per area | centralized intent, decentralized execution; autonomy granted according to trust that must be earned and sustained (mission command, NATO AJP-01) | W01; human-org-parallels | candidate |
| C69 | Verification versus validation | "building the product right" versus "building the right product": two separate questions with separate mechanisms | W04; Boehm 1984 | candidate |
| C67 | Post-merge watch role | a rotating person watches the release branch after merge | monday.com (secondary) | n/a here |

## 6. Observability and metrics

| ID | Mechanism | What it is | Source | Here |
|---|---|---|---|---|
| C70 | Tool-call summaries | a cheap model summarizes tool calls; configurable frequency and lines | S01 | n/a here (runtime feature) |
| C71 | Trace store and visualizer | sessions, phases, events, envelopes, gate results in SQLite, polled UI | SSSF family | partial (git history and task Reports) |
| C72 | Prompt version, cost and transcript per run; replay bench | fixed cases replayed against hidden tests to see whether a prompt or model change helped | PM | unknown |
| C73 | Efficiency metrics | output versus human time and token cost; owner minutes per merged task, returns, approval waiting time | S02 W11; owner note N1 | candidate |
| C74 | Notification volume eval | measure and tune how many messages the agent sends | S04 | candidate |
| C75 | Usage recorded without a cost cap | token usage snapshots only (a gap in one kit) | GE | n/a here |

## 7. Memory and context

| ID | Mechanism | What it is | Source | Here |
|---|---|---|---|---|
| C80 | Context hygiene | sub-agents with clean context returning summaries, just-in-time loading, notes outside the window, compaction | Anthropic docs (S03 T6); S04 | n/a here (runtime) |
| C81 | Decision log | append-only decisions with reasons (ADR), source pointers | S03 T6; Nygard | exists |
| C82 | Short rules with reason, date, owner | converted to checks where possible; reviewed against staleness | S03 T7 | partial |
| C83 | Pointer instead of copy | memory holds what cannot be looked up; otherwise a link | S04 | exists (docs practice) |
| C84 | Progress files per task | what is done and what is left, loaded by the next run | S04 | exists (task Report and Comments) |
| C85 | Progressive disclosure of skills | name and description at start, body when relevant, files on demand | Anthropic skills article (S03 T6) | n/a here (runtime) |

## 8. Packaging, install, upgrade

| ID | Mechanism | What it is | Source | Here |
|---|---|---|---|---|
| C90 | Empty factory template | a generic template without purpose; init, configure, specialize | S01 | n/a here (gap) |
| C91 | One skill installs the factory | idempotent copy that skips existing files, `--force` overwrites, marker block in AGENTS.md | SSSF; PM; FS | n/a here |
| C92 | Manifest-based upgrade | hashes of shipped files; untouched files auto-update, tailored files are held and diffed, project-owned files never touched, `--mark-reviewed` | FS | n/a here |
| C93 | Per-stack check templates | starter `checks.sh` per language that fail on skips | FS | n/a here |
| C94 | Stack and gap detection | detect tools, CI commands, linter and typecheck gaps, skip patterns | FS | n/a here |
| C95 | Ports and plugin registry | the core never imports plugins; plugins register themselves; strict parameters | MF | n/a here |
| C96 | Skills as text | `skills add` shows and asks before copying; nothing executed; size budget per step | MF | n/a here |
| C97 | Plain-language routing | a request is routed to a workflow with a confidence threshold, drafted and confirmed before running | MF | n/a here |
| C98 | Agentic access parity | everything the human can do in the tool, the agent can do | S01 | partial |

## 9. Adaptation and self-improvement

| ID | Mechanism | What it is | Source | Here |
|---|---|---|---|---|
| C100 | Skill loop | observer agents watch how skills are applied (e.g. human corrections of review comments) and improve the skills | S02 W12 | candidate |
| C101 | Liquid configuration | the factory is configured by data that an agent can rewrite on request | S01 | partial |
| C102 | Meta-optimization cycle | measure, find where humans wait, automate with gates or move the human to a decision point, repeat | S01 C1; S03 (synthesis) | candidate |
| C103 | Pilot per task class | narrow claims, control group, a new pilot for each class, re-measure after model or tool changes | S03 T13 | n/a here |

## 10. Budgets and cost

| ID | Mechanism | What it is | Source | Here |
|---|---|---|---|---|
| C110 | Per-agent budgets | model tier, tools, time and spend limits per sub-agent | S01 | n/a here |
| C111 | Per-stage limits | separate counters for infrastructure retries, corrections (per round or per session) and semantic rounds; malformed output never burns a review round; typed pause at the limit | GE | candidate |
| C112 | Max cost and stuck stop | a task aborts at a cost cap or after repeated identical gate failures | PM | n/a here |
| C113 | Token spend guideline | $1000 per engineer per day as a practical target, with the objection that $20,000 a month changes the business model | S03 T3; StrongDM; Willison | n/a here |

## 11. Architecture and orchestration

| ID | Mechanism | What it is | Source | Here |
|---|---|---|---|---|
| C120 | Control, execution and data plane | intake channels, a control plane distributing work, cloud sandboxes with harness and model choice, a data plane for memory and learning | S02 W10 | partial |
| C121 | Build versus buy | a simple factory is easy, a scalable one is infrastructure; domain tuning stays yours | S02 W13 | n/a here |
| C122 | Public factory board | issues, states and agents visible to contributors | S02 W14 | n/a here |
| C123 | Worktree per task, PR as the human gate | branch per task from the base, human merges | PM; FS; GE | exists |
| C124 | Server lease and action outbox | TTL and heartbeat lease; compare-and-set leased actions; orphan requeue | GE | n/a here |
| C125 | Agent pane and process recovery | agents survive a restart and are adopted again | GE | n/a here |
| C126 | Isolation for parallel agents | port offsets, tests that pass twice on a dirty database | S03 T3 (Understanding Data) | n/a here |

## 12. Cross-cutting concepts (added 2026-11-17)

| ID | Mechanism | What it is | Source | Here |
|---|---|---|---|---|
| C130 | Error-diversity techniques | change the model family, the input (independent interpretation of the requirement), the mechanism (executable oracles), the information the checker sees; prompt changes give little | W08; Nogueira 2026; Ron 2026; Verga 2024 | candidate |
| C131 | Failure-independence measurement | pairwise failure-correlation test and the share of the independence gain realized by an ensemble | Knight-Leveson 1986; Nogueira 2026 | candidate |
| C132 | Evidence grading | every empirical claim carries sample, population, venue and a weight grade; weak data does not carry a decision alone | W06; `evidence-register.md` | n/a here (meta) |
| C133 | Review cadence with switching | review share by class, starting high and lowered after clean samples, raised after an escaped defect; plus time-based and event-based reviews | W14.3; acceptance sampling (MIL-STD-105, ANSI/ASQ Z1.4); DORA | candidate |
| C134 | Review of the factory's own rules | checks, prompts, skills, thresholds are reviewed like code because they define what counts as correct | W14.3; Goodhart | candidate |
| C135 | Executor profile | capability, reliability and error correlation, cost and speed, availability, memory, ability to ask, incentives and accountability, trust, observability | W14.4 | candidate |
| C136 | Ask before assuming | clarifying questions early, not guesses (owner rule P6); human side of "assumptions list" and deferred tool calls | W09; S04 | candidate |
| C137 | Measure the criterion, reality judges | the human defines what counts as value (the evaluation criterion), experiments and indicators judge; most ideas fail to move target metrics | W10; Kohavi | candidate |

