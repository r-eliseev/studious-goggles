# Catalog of factory mechanisms

Universal catalog (principle `document-everything` in `docs/factory/README.md`): every mechanism found in the sources, whether or not it applies to this repository's harness. Source codes: **`tsf-lesson`** TSF lesson 2, **`warp-talk`** Warp talk, **`owner-dialog`** owner's dialog (T.. = thread in the walkthrough), **`always-on-agents`** Anthropic always-on agents; kits from `research/factory-kits-code-reading.md`: **SSSF** (disler/super-simple-software-factory, ts-factory is its TypeScript port), **FS** (factory-setup), **PM** (PaddyMc/software-factory), **MF** (minifactory), **GE** (geut/factory-skills). Descriptions are condensed from the notes; web-sourced facts keep the labels used there.
Applicability to this repository's planner/executor harness: **exists** (already in place), **candidate** (worth proposing), **partial**, **n/a here** (does not apply to this harness, kept for universality), **unknown** (not assessed). The assessment is a hypothesis until task 291/288 reviews it.

## 1. Lifecycle and workflow

| ID | Mechanism | What it is | Source | Here |
|---|---|---|---|---|
| `lifecycle-loop` | Lifecycle loop | idea, triage, spec (if complex), implementation, review (agent first, then human), verification, release, monitoring, results back to intake | `warp-talk`, `lifecycle-loop` | partial (no triage, monitoring, feedback to intake) |
| `triage-agent` | Triage agent | easy and unambiguous issues go straight to implementation, hard ones get a spec | `warp-talk`, `triage-agent` | unknown |
| `product-and-tech-spec` | Product spec plus tech spec | invariants of the product separate from architecture and shape of code | `warp-talk`, `product-and-tech-spec` | partial |
| `validated-task-contract` | Machine-validated task contract | fixed keys, id regex, dependency cycle check, verification-command linter, placeholder ban, status stored outside the plan | PM | partial |
| `stacked-tasks` | Stacked tasks | a task depending on an open PR builds on its candidate; restack and conflict state when dependencies merge | PM | n/a here (batches share a branch) |
| `reshapeable-flow` | Reshapeable flow by config | grammar `[plan]? build+ (review\|gates)* [verify]?`, gates step always present | PM | n/a here |
| `declarative-workflows` | Declarative workflows as validated data | YAML steps, `when`, `on_fail`, `blocking`, templating, no loops by design, unknown keys are errors, forward references rejected | MF | partial |
| `operator-skill` | Operator skill | a thin skill that launches workflows and observes, never implements, does not volunteer state | SSSF | partial |
| `phase-defaults-to-fail` | Phase defaults to fail | a phase succeeds only on clean exit; acceptance of the run separate from phase success | SSSF | candidate |
| `plan-from-files` | Plan accepted from files, not from the reply | agent output text is not the source of truth | GE | exists |
| `versioned-stage-templates` | Versioned stage output templates | each stage returns a template (e.g. `factory.review.v5`) validated by the controller | GE | partial |
| `test-first-step` | Test-first step before the build | planned in PM, not built | PM | unknown |
| `pipeline-as-graph` | Pipeline spec as directed graph with LLM-evaluated transitions | phases (parse_spec, generate_code, run_scenarios, evaluate) | `owner-dialog`, `autonomy-levels` (Infralovers, secondary) | unknown |

## 2. Runtime primitives (always-on)

| ID | Mechanism | What it is | Source | Here |
|---|---|---|---|---|
| `event-inbox` | Event inbox | durable list of events written by handlers, read by one worker; status per event; dedupe by source id; batching; inbox is the source of truth | `always-on-agents` | partial (task files and claim scripts) |
| `timers` | Timers | follow-ups (set by the agent) and schedules (recurring) in their own durable store; a scheduler fires due timers into the inbox | `always-on-agents` | n/a here today (no timers) |
| `memory-store` | Memory store | durable, separate from the worker; holds only what cannot be looked up; pointers instead of copies | `always-on-agents` | partial (docs, status, reports) |
| `dreaming` | Dreaming | reviewing past sessions to curate memories, replace outdated entries, find cross-run patterns | `always-on-agents` | n/a here (closest: `scripts/audit.sh`) |
| `outbox-and-dispatcher` | Outbox and dispatcher | outgoing messages table, pacing, error recovery; tune the number of messages with evals; start quiet and earn attention | `always-on-agents` | n/a here |
| `deferred-tool-call` | Deferred tool call | call suspended, run ends, session saved mid-call, human answers as an inbox event that resumes the session; worker free meanwhile | `always-on-agents`; GE typed pause | partial (status `blocked` plus Comments) |
| `one-active-worker` | One active worker | a lease or claim so two runs never duplicate work | `always-on-agents`; GE server lease; this harness claim scripts | exists |
| `reducer-plus-transactional-outbox` | Pure reducer plus transactional outbox | state changes by events with idempotency keys; side effects through an outbox | GE | n/a here |
| `run-and-session-model` | Run and session model | a run inside a session; replies resume the same session; model change starts a new one | `always-on-agents`; SSSF agent map; GE | unknown |

## 3. Verification

| ID | Mechanism | What it is | Source | Here |
|---|---|---|---|---|
| `holdout-scenarios` | Holdout scenarios | end-to-end user stories kept outside the repository and invisible to the agent, validated by an LLM judge | `owner-dialog`, `holdout-oracle-independence`; StrongDM | candidate |
| `satisfaction-metric` | Satisfaction metric | fraction of trajectories through scenarios that likely satisfy the user; hard invariants stay binary and blocking | `owner-dialog`, `holdout-oracle-independence`; StrongDM | unknown |
| `digital-twins` | Digital twins | agent-built behavioral clones (fakes) of external services, checked against the live dependency until no differences are found | `owner-dialog`, `mocks-twins-fakes`; StrongDM | n/a here |
| `two-test-sets` | Two test sets | visible set for debugging, hidden set for acceptance, graded feedback, rotation, attempt limits | `owner-dialog`, `holdout-oracle-independence` (synthesis) | unknown |
| `independent-check-source` | Independent check source | different model, role or context; different truth (replay, historical bugs, reference implementation, external contracts); different method (property, fuzz, mutation, N-version) | `owner-dialog`, `holdout-oracle-independence` | partial |
| `human-arbiter` | Human arbiter | decides when implementation and scenario disagree; decisions become precedents that calibrate the LLM judge | `owner-dialog`, `holdout-oracle-independence` | partial (planner review) |
| `check-quality-measurement` | Check quality measurement | mutation testing on changed code, sampling reviews with the rule of three, seeded known bugs to calibrate the reviewer | `owner-dialog`, `autonomy-levels`, `proving-checks-and-pilots` | unknown |
| `checks-that-can-fail` | Checks that can fail | run checks twice in clean worktrees, then once with an injected violation that must fail; fail on skipped or deselected tests | FS | candidate |
| `inconclusive-is-failure` | Inconclusive is failure | zero tests selected counts as failure; stuck detector for repeated identical failures | PM | candidate |
| `verdict-verification` | Mechanical verdict verification | reviewer must cite `path:line` that resolves in the worktree, cover the exact expected criterion ids, differ in model, use read-only tools, take rules from the base commit; diff-hash freshness | GE | partial |
| `deterministic-gates-decide` | Deterministic gates decide | the model reviewer cannot override the gates; PR opens only when gates pass | PM | partial |
| `gate-from-main-checkout` | Gate from the main checkout | the check script comes from the trusted checkout and protected paths cannot be edited by the change | FS; SSSF | candidate |
| `typed-envelopes` | Typed envelopes with self-consistency gates | JSON output validated by schema; corrections fed back in the same session, bounded | SSSF | n/a here (markdown outputs) |
| `claim-gate-vs-real-diff` | Claim gate against the real diff | compare claimed changed files with the git diff (a kit's version only checked that files exist: counter-example) | SSSF family | candidate |
| `rules-into-checks` | Rules into checks | turn "this must not change" into compile-time or linter checks with a passing and a failing fixture | `owner-dialog`, `rules-to-checks-cheap-failure` | candidate |
| `production-verification` | Production verification | synthetic user journeys, business-invariant alarms, shadow traffic, canary rollout with auto-rollback | `owner-dialog`, `rules-to-checks-cheap-failure`; AWS docs | n/a here (no product runtime) |
| `ui-verification-by-computer-use` | UI verification by computer use | the agent drives the product and records videos and screenshots | `warp-talk`, `verification-step` | partial (E2E on Apple CI) |

## 4. Permissions and safety

| ID | Mechanism | What it is | Source | Here |
|---|---|---|---|---|
| `write-boundary-by-tree-diff` | Write boundary by tree diff | snapshot before, diff after, roll back disallowed paths, per-role `writes` and protected files (known holes: committed changes, skipped on earlier failure, same-count edits) | SSSF family | candidate |
| `os-sandbox` | OS sandbox | bubblewrap, read-only root, cleared environment, task database invisible to the agent | PM | n/a here (cloud sandbox of the session) |
| `credential-proxy` | Credential proxy | credentials injected by a proxy outside the agent's environment; the agent never holds them | `always-on-agents` | n/a here (the session runtime does it) |
| `auto-mode` | Auto mode | classifiers review higher-risk actions; human review kept for the most important decisions | `always-on-agents` | n/a here (runtime feature) |
| `least-privilege` | Least privilege and enforcement by the system | allowlists over denylists, secrets out of reach, branch or worktree isolation; rules in prompts are not enforcement | `owner-dialog`, `specs-and-agent-boundaries`; Claude Code docs | partial |
| `per-role-models-and-tools` | Per-role models and tools | reviewer on a different model with read-only tools | GE | unknown |
| `trust-boundary-for-inbound-text` | Trust boundary for inbound text | issue text reaches agents only as quoted data; a label is the trust boundary | FS | unknown |
| `state-outside-the-agent` | State outside the agent | agents cannot change task state; a controller or single-writer script does | PM; GE; FS | partial |
| `blast-radius-of-state` | Blast radius of state | deletion protection, retained stateful resources, backups outside the workload's lifecycle and account, scoped tokens | `owner-dialog`, `rules-to-checks-cheap-failure`, `postmortems-and-incidents` | n/a here |

## 5. Human involvement

| ID | Mechanism | What it is | Source | Here |
|---|---|---|---|---|
| `mode-per-action` | Mode per action, task class or repository | the human mode is a property of the action or area, not of the person or the factory | `owner-dialog`, `autonomy-levels`; task 287 | candidate |
| `in-near-out-of-loop` | In, near and out of the loop | modes of human participation; near the loop = agents run unattended and stop only at decision points | `tsf-lesson`, `loop-position` | partial |
| `decision-points` | Decision points | the agent returns a result needing attention instead of asking continuously | `tsf-lesson`; `always-on-agents` | partial |
| `risk-based-review-routing` | Risk-based review routing | humans enter code review by risk; reviewers chosen by risk and surfaces | `warp-talk`, `review-risk-routing`; FS route | unknown |
| `understanding-debt` | Understanding-debt practices | predict before reading, agent quizzes the owner, architecture diff, bounded change size, manual incident analysis | `owner-dialog`, `control-understanding-review` | candidate |
| `pre-mortem-and-assumptions` | Pre-mortem and assumptions | "imagine it failed, why" before starting; the agent lists assumptions before acting | `owner-dialog`, `premortem-and-assumptions` (`remember-pre-mortem`, `remember-assumptions-list`) | candidate |
| `interviewer-agent` | Interviewer agent | extracts tacit knowledge by asking the owner concrete questions | `owner-dialog`, `tacit-knowledge-and-sources` (`remember-people-answer-questions`) | unknown |
| `commanders-intent-trust` | Commander's intent and trust earned per area | centralized intent, decentralized execution; autonomy granted according to trust that must be earned and sustained (mission command, NATO AJP-01) | `vs-classical-delegation`; human-org-parallels | candidate |
| `verification-versus-validation` | Verification versus validation | "building the product right" versus "building the right product": two separate questions with separate mechanisms | `same-question-for-human-orgs`; Boehm 1984 | candidate |
| `post-merge-watch-role` | Post-merge watch role | a rotating person watches the release branch after merge | monday.com (secondary) | n/a here |

## 6. Observability and metrics

| ID | Mechanism | What it is | Source | Here |
|---|---|---|---|---|
| `tool-call-summaries` | Tool-call summaries | a cheap model summarizes tool calls; configurable frequency and lines | `tsf-lesson` | n/a here (runtime feature) |
| `trace-store-and-visualizer` | Trace store and visualizer | sessions, phases, events, envelopes, gate results in SQLite, polled UI | SSSF family | partial (git history and task Reports) |
| `prompt-version-and-cost` | Prompt version, cost and transcript per run; replay bench | fixed cases replayed against hidden tests to see whether a prompt or model change helped | PM | unknown |
| `efficiency-metrics` | Efficiency metrics | output versus human time and token cost; owner minutes per merged task, returns, approval waiting time | `warp-talk`, `efficiency-metrics`; owner note `attention-metrics` | candidate |
| `notification-volume-eval` | Notification volume eval | measure and tune how many messages the agent sends | `always-on-agents` | candidate |
| `usage-without-cost-cap` | Usage recorded without a cost cap | token usage snapshots only (a gap in one kit) | GE | n/a here |

## 7. Memory and context

| ID | Mechanism | What it is | Source | Here |
|---|---|---|---|---|
| `context-hygiene` | Context hygiene | sub-agents with clean context returning summaries, just-in-time loading, notes outside the window, compaction | Anthropic docs (`owner-dialog`, `context-and-agent-split`); `always-on-agents` | n/a here (runtime) |
| `decision-log` | Decision log | append-only decisions with reasons (ADR), source pointers | `owner-dialog`, `context-and-agent-split`; Nygard | exists |
| `short-rules-with-reason` | Short rules with reason, date, owner | converted to checks where possible; reviewed against staleness | `owner-dialog`, `tacit-knowledge-and-sources` | partial |
| `pointer-instead-of-copy` | Pointer instead of copy | memory holds what cannot be looked up; otherwise a link | `always-on-agents` | exists (docs practice) |
| `progress-files-per-task` | Progress files per task | what is done and what is left, loaded by the next run | `always-on-agents` | exists (task Report and Comments) |
| `progressive-disclosure-of-skills` | Progressive disclosure of skills | name and description at start, body when relevant, files on demand | Anthropic skills article (`owner-dialog`, `context-and-agent-split`) | n/a here (runtime) |

## 8. Packaging, install, upgrade

| ID | Mechanism | What it is | Source | Here |
|---|---|---|---|---|
| `empty-factory-template` | Empty factory template | a generic template without purpose; init, configure, specialize | `tsf-lesson` | n/a here (gap) |
| `one-skill-installs-factory` | One skill installs the factory | idempotent copy that skips existing files, `--force` overwrites, marker block in AGENTS.md | SSSF; PM; FS | n/a here |
| `manifest-based-upgrade` | Manifest-based upgrade | hashes of shipped files; untouched files auto-update, tailored files are held and diffed, project-owned files never touched, `--mark-reviewed` | FS | n/a here |
| `per-stack-check-templates` | Per-stack check templates | starter `checks.sh` per language that fail on skips | FS | n/a here |
| `stack-and-gap-detection` | Stack and gap detection | detect tools, CI commands, linter and typecheck gaps, skip patterns | FS | n/a here |
| `ports-and-plugins` | Ports and plugin registry | the core never imports plugins; plugins register themselves; strict parameters | MF | n/a here |
| `skills-as-text` | Skills as text | `skills add` shows and asks before copying; nothing executed; size budget per step | MF | n/a here |
| `plain-language-routing` | Plain-language routing | a request is routed to a workflow with a confidence threshold, drafted and confirmed before running | MF | n/a here |
| `agentic-access-parity` | Agentic access parity | everything the human can do in the tool, the agent can do | `tsf-lesson` | partial |

## 9. Adaptation and self-improvement

| ID | Mechanism | What it is | Source | Here |
|---|---|---|---|---|
| `skill-loop` | Skill loop | observer agents watch how skills are applied (e.g. human corrections of review comments) and improve the skills | `warp-talk`, `skill-loop` | candidate |
| `liquid-configuration` | Liquid configuration | the factory is configured by data that an agent can rewrite on request | `tsf-lesson` | partial |
| `meta-optimization-cycle` | Meta-optimization cycle | measure, find where humans wait, automate with gates or move the human to a decision point, repeat | `tsf-lesson`, `meta-optimization`; `owner-dialog` (synthesis) | candidate |
| `pilot-per-task-class` | Pilot per task class | narrow claims, control group, a new pilot for each class, re-measure after model or tool changes | `owner-dialog`, `proving-checks-and-pilots` | n/a here |

## 10. Budgets and cost

| ID | Mechanism | What it is | Source | Here |
|---|---|---|---|---|
| `per-agent-budgets` | Per-agent budgets | model tier, tools, time and spend limits per sub-agent | `tsf-lesson` | n/a here |
| `per-stage-limits` | Per-stage limits | separate counters for infrastructure retries, corrections (per round or per session) and semantic rounds; malformed output never burns a review round; typed pause at the limit | GE | candidate |
| `max-cost-and-stuck-stop` | Max cost and stuck stop | a task aborts at a cost cap or after repeated identical gate failures | PM | n/a here |
| `token-spend-guideline` | Token spend guideline | $1000 per engineer per day as a practical target, with the objection that $20,000 a month changes the business model | `owner-dialog`, `autonomy-levels`; StrongDM; Willison | n/a here |

## 11. Architecture and orchestration

| ID | Mechanism | What it is | Source | Here |
|---|---|---|---|---|
| `control-execution-data-planes` | Control, execution and data plane | intake channels, a control plane distributing work, cloud sandboxes with harness and model choice, a data plane for memory and learning | `warp-talk`, `control-execution-data-planes` | partial |
| `build-versus-buy` | Build versus buy | a simple factory is easy, a scalable one is infrastructure; domain tuning stays yours | `warp-talk`, `build-vs-buy` | n/a here |
| `public-factory-board` | Public factory board | issues, states and agents visible to contributors | `warp-talk`, `public-factory` | n/a here |
| `worktree-per-task` | Worktree per task, PR as the human gate | branch per task from the base, human merges | PM; FS; GE | exists |
| `server-lease-and-outbox` | Server lease and action outbox | TTL and heartbeat lease; compare-and-set leased actions; orphan requeue | GE | n/a here |
| `agent-process-recovery` | Agent pane and process recovery | agents survive a restart and are adopted again | GE | n/a here |
| `isolation-for-parallel-agents` | Isolation for parallel agents | port offsets, tests that pass twice on a dirty database | `owner-dialog`, `autonomy-levels` (Understanding Data) | n/a here |

## 12. Cross-cutting concepts (added 2026-11-17)

| ID | Mechanism | What it is | Source | Here |
|---|---|---|---|---|
| `error-diversity-techniques` | Error-diversity techniques | change the model family, the input (independent interpretation of the requirement), the mechanism (executable oracles), the information the checker sees; prompt changes give little | `one-model-errs-differently`; Nogueira 2026; Ron 2026; Verga 2024 | candidate |
| `failure-independence-measure` | Failure-independence measurement | pairwise failure-correlation test and the share of the independence gain realized by an ensemble | Knight-Leveson 1986; Nogueira 2026 | candidate |
| `evidence-grading` | Evidence grading | every empirical claim carries sample, population, venue and a weight grade; weak data does not carry a decision alone | `weak-sample-48-percent`; `evidence-register.md` | n/a here (meta) |
| `review-cadence-switching` | Review cadence with switching | review share by class, starting high and lowered after clean samples, raised after an escaped defect; plus time-based and event-based reviews | `review-design`; acceptance sampling (MIL-STD-105, ANSI/ASQ Z1.4); DORA | candidate |
| `review-factory-rules` | Review of the factory's own rules | checks, prompts, skills, thresholds are reviewed like code because they define what counts as correct | `review-design`; Goodhart | candidate |
| `executor-profile` | Executor profile | capability, reliability and error correlation, cost and speed, availability, memory, ability to ask, incentives and accountability, trust, observability | `goal-and-no-difference` | candidate |
| `ask-before-assuming` | Ask before assuming | clarifying questions early, not guesses (owner rule `ask-dont-assume`); human side of "assumptions list" and deferred tool calls | `ask-dont-assume-rule`; `always-on-agents` | candidate |
| `reality-judges-criterion` | Measure the criterion, reality judges | the human defines what counts as value (the evaluation criterion), experiments and indicators judge; most ideas fail to move target metrics | `reality-as-judge`; Kohavi | candidate |
| `risk-classes` | Risk classes (universal rules only) | classify a change or action by reversibility, blast radius, what it touches (money, secrets, user data, third parties, the factory's own rules) and how visible a failure is; the highest class touched wins; each class has an approver, required checks, containment and observation | `owner-session-answers`; `case-study-imagegen` | candidate |
| `irreversible-actions-human-approval` | Irreversible external actions go through a human | emails to customers, payments, publications and releases need a human approval; a cancel window complements it but does not replace it | `owner-session-answers` (owner's decision) | candidate |
| `architecture-review-instead-of-code-review` | Architecture review instead of reading all code | an agent prepares a map of modules, contracts and failure policy; the owner reviews the map and finds architectural fixes for classes of bugs | `owner-session-answers`; owner-dialog (review the architecture diff, not the code) | candidate |
| `bug-pattern-mining` | Mining the history for bug patterns | group past fixes by cause to find recurring architectural themes before deciding on a refactor | `case-study-imagegen` | candidate |
| `scripted-agent-controller` | Script as controller of a ready-made agent | a script owns sequencing, retries, budgets, permissions and acceptance and runs the agent as a bounded node over ACP, `claude -p` or the Agent SDK; permission requests are answered by policy or routed to a human | owner's request; `research/scripted-harness-around-agents.md`; the factory kits | candidate |
| `agent-cannot-bypass-control` | The agent cannot bypass the controller | control is placed where the agent cannot reach: tools for reading and writing served by the host, OS-level isolation, credentials outside the agent's environment; pattern deny lists are not a boundary | owner's requirement; ACP and Agent SDK documentation; `research/scripted-harness-around-agents.md` | candidate |
| `label-driven-routing` | Label-driven stage routing | issue events and labels start the workflow of the next stage; triage returns exactly one readiness state and chooses the more cautious one when in doubt | `cloud-factory-demo` | candidate |
| `triage-split-read-and-write` | Split rights across jobs | the analysing job is read-only and returns a structured result; a separate job with the write right applies it | `cloud-factory-demo` | candidate |
| `specs-as-reviewed-pull-request` | Specs as a pull request reviewed by a human | the spec stage writes product and technical specs as files in a pull request that a human reviews before implementation starts | `cloud-factory-demo` | candidate |
| `behavioral-verification-by-computer-use` | Behavioral verification with evidence | cloud subagents reproduce a bug or verify a change by using the product, one worker per user story, with video and screenshot evidence folded into comments and reviews | `cloud-factory-demo` | candidate |
| `review-as-json-then-publish` | Review as validated JSON, published by another job | the review agent writes a validated structured file and never posts; PR text is untrusted evidence; a separate job publishes | `cloud-factory-demo` | candidate |
| `skill-improvement-outer-loop` | Skill-improvement outer loop | a daily job reads human reactions to the agent's review comments, classifies them (validated, corrected, refined, ambiguous) and opens a pull request that updates the review skill | `cloud-factory-demo`; `warp-talk` (`skill-loop`) | candidate |
| `workflow-as-controller` | Repository-host workflow as the controller | triggers, pinned action versions, secrets, per-job permissions and concurrency groups on the host control the agent runs | `cloud-factory-demo` | candidate |
| `control-plane-contents` | What the control plane holds | configuration (skills and MCP servers, model and harness, environment, permissions, memory), architecture (agent routing, rules?), administration (cost, governance, billing, evals, user management) | `warp-talk-owner-notes` (not in the talk transcript) | candidate |
| `data-plane-contents` | What the data plane holds | artifacts and storage, skills, memory, MCP, knowledge base, rules? | `warp-talk-owner-notes`; `warp-talk` (name and purpose) | candidate |
| `efficiency-formula` | Efficiency formula | efficiency = software shipped / (tokens + human time) | `warp-talk-owner-notes`; `warp-talk` (`efficiency-metrics`) | candidate |
| `two-level-loops` | Two levels of loops | the inner loop does the work, the outer loop improves the loop (skills, rules, routing) | `warp-talk-owner-notes`; `cloud-factory-demo` (`skill-improvement-outer-loop`) | candidate |
| `factory-as-code` | Factory defined as code | one versioned definition (`factory.yaml`: repositories, secrets, MCP servers, cloud providers, integrations, agent defaults) plus files for agents, automations, runners, skills, webhooks, scorers, benchmarks | `warp-dev-site` | candidate |
| `scorers-on-agent-runs` | Scorers on agent runs | graders that score completed agent conversations (for example code quality) | `warp-dev-site` | candidate |
| `model-benchmarks-on-own-work` | Model benchmarks on the team's own work | replay the team's agent conversations to compare models on correctness and cost | `warp-dev-site` | candidate (vendor claim) |
| `routing-agent-foreman` | Routing agent | one orchestration agent receives work and delegates to specialized agents (triage, implementation, review, prototyping) | `warp-dev-site` | candidate |
| `any-surface-steering` | Any surface | watch, steer and hand off runs from web, mobile, terminal or IDE | `warp-dev-site` | candidate |
| `rules-advisory-vs-enforced` | Rules: advisory vs enforced | standing guidance in the agent's context (advisory, data plane) versus policy backed by permissions or checks (enforced, control plane); a rule without enforcement is a wish | `warp-dev-site` (rules docs), this repo's `AGENTS.md` and `scripts/check.sh` | candidate |
