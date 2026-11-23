# Spec-driven development: approaches, and how to organize specs as a tree

Owner's request (2026-11-23), a digression from the walkthrough: which approaches to spec-driven development exist and how to organize specs into a tree. Sources: `sources/spec-driven-development/`. Labels: **read** = primary text read; **synthesis** = my inference; the tools change quickly, this is the state of 2026-11-23.

## 1. Approaches (what was read)

**Depth of the spec's role** (Böckeler, 2025-10-15, read): *spec-first* (a spec is written before the code, then maybe discarded), *spec-anchored* (the spec is kept and maintained with the feature over time), *spec-as-source* (the spec is the main artifact and humans do not edit the code). "All SDD approaches I found are spec-first, but not all strive to be spec-anchored or spec-as-source; often it is left vague what the spec maintenance strategy over time is meant to be."

| Approach | Unit of spec | Files and tree (read) | Order of work | Notation |
|---|---|---|---|---|
| **Kiro** (AWS) | one feature (or bugfix) | `requirements.md`, `design.md`, `tasks.md` per spec; always-on **steering** files: `product.md`, `tech.md`, `structure.md`, plus custom ones, in the workspace or the home folder | two workflows: requirements-first (requirements, design, tasks) or design-first (design, requirements, tasks); "Quick Spec" runs all three without approval gates; analysis of requirements for inconsistencies and gaps before design | requirements in EARS: "WHEN [condition] THE SYSTEM SHALL [behavior]" |
| **spec-kit** (GitHub) | one feature, a numbered folder per branch | `specs/NNN-feature/` with `spec.md`, `plan.md`, `research.md`, `data-model.md`, `contracts/`, `quickstart.md`, `tasks.md`; project-wide `memory/constitution.md` | constitution once per project, then specify, plan, tasks, implement; gates: the plan is checked against the constitution; tests and contracts before implementation | user stories with acceptance criteria; templates constrain the model |
| **OpenSpec** (Fission AI) | the system's current behaviour by domain, plus one folder per change | `openspec/specs/<domain>/spec.md` (the source of truth) and `openspec/changes/<change>/` with `proposal.md`, `design.md`, `tasks.md` and **delta specs** (ADDED, MODIFIED, REMOVED requirements); on archive the deltas merge into `specs/` and the change moves to `changes/archive/` | proposal, specs, design, tasks; "dependencies are enablers, not gates"; custom schemas of artifacts | requirements with RFC 2119 words (SHALL, MUST) and Given/When/Then scenarios; "progressive rigor": lite spec by default, full spec for higher risk |
| **BMAD method** | an idea or a change; briefs, specifications, architecture | installed skills and agents with roles (product, architecture, UX, development, testing); right-sized process | clarify, plan, build and verify, learn and adjust | not fixed by the README |
| **Tessl** | one spec per code file (spec-as-source), per Böckeler; not read at the source | spec file next to the generated code, code marked as generated | spec edited, code regenerated | free text |
| **Nested rules files** (`AGENTS.md`, Warp and Kiro docs) | directory | `AGENTS.md` in the root and in subdirectories; most specific wins (Warp: subdirectory, then root, then global); Kiro adds inclusion modes by front matter (always, by file pattern, manual, by description) | n/a | prose |

Common core (read): a requirements-like document, a design-like document, a task list; an always-on layer of project rules; approval points between phases in most tools.

## 2. How to organize specs as a tree (synthesis, grounded in the above)

The tools mix **four different trees**; the confusion is the main reason spec folders rot:
1. **Intent tree** (why and for whom): product goals, users, scope. Slow-changing, human-owned (ImageGen: `docs/product.md`).
2. **Current-truth tree** (what the system does now): behaviour contracts by domain with scenarios; one home for each requirement; changed only by reviewed deltas (OpenSpec `specs/<domain>/`).
3. **Change tree** (what is being altered): one folder per change or feature with proposal, design, tasks, delta specs; temporary, archived afterwards (OpenSpec `changes/`, spec-kit `specs/NNN-*`, Kiro per-feature specs).
4. **Constraint layer** (always-on rules): constitution, steering, nested `AGENTS.md`, plus the decision log (ADRs, append-only).
Tools that keep only per-feature folders (spec-kit, Kiro) have trees 3 and 4 but **no tree 2**: the feature folders accumulate and nobody merges them into a current description, which is exactly the maintenance question Böckeler says is left vague. OpenSpec is the one that makes tree 2 explicit and merges deltas into it.

Design rules for the tree (synthesis):
- **Split by zoom level**: intent, then domain or capability specs (behaviour), then interface contracts (between modules or services), then design (per change), then tasks. Higher levels change slowly and are decided by humans; lower levels are drafted by agents and reviewed.
- **Parent and child**: a child may refine but not contradict its parent; every requirement has one home and a stable name; links go up (what it serves) and down (the scenario and the test that verifies it). A script can check that every spec file is linked and every requirement has a scenario: a rule turned into a check.
- **Split criteria**: an ownership or bounded-context boundary, a size that an agent can read in one pass, different change rates, different verification modes (pure logic versus UI versus platform behaviour).
- **Load by path**: an agent working in one module should load the root rules plus that module's spec, not everything (nested rules files, Kiro inclusion by file pattern; "just-in-time context", `research/context-management-practices.md`). This is the module map of task 298 seen from the spec side.
- **Mutable versus append-only**: constitution and ADRs change by amendment with a date; domain specs change only through a change folder; change folders are archived, not edited.
- **Rigor by risk**: a short spec by default, a full one for contracts, migrations, security (OpenSpec progressive rigor); the risk classes of `definition.md` decide.
- **Specs also feed the checks**: scenarios in the spec are the source of acceptance tests; keep some hidden from the implementer for independence (`imagegen-acceptance-not-independent`).
- **Many services** (the owner's main work: 10+ services in different languages): one spec tree per service, a system-level contracts layer above them owned by the people who own the boundary (contract tests on both sides), one organization-wide constitution, and a change folder that can span several services (OpenSpec marks cross-repo changes as full-spec).

## 3. ImageGen as the living example (facts from the repository, 2026-11-23)
- Intent: `docs/product.md`. Constraints: `AGENTS.md`, `docs/conventions.md`, ADRs (37 files in `docs/decisions/` including the template). Architecture: `docs/architecture.md`. Changes: tasks (`tasks/backlog`, `tasks/v0.1`, archived in `tasks/DONE.md` with the text in git history) with Description, Acceptance criteria, Analysis, Plan, Report: this is already a **change folder in one file**.
- Current truth: `docs/specs/*.md`, flat, one file per feature with a template (goal, requirements, acceptance criteria, constraints, tests). `image-client.md` is 392 lines and holds ten requirement areas, nine UI screens and the data entities in one file; `chat-sessions.md` 97 lines; `acceptance-scenarios.md` 187 lines (Gherkin catalog).
- Observations (synthesis, not checked against all files): (a) tree 2 exists but as a few large files, so an agent that needs one area still reads the whole; (b) the merge from a finished task into the spec is a manual rule in `AGENTS.md` ("changed behaviour, update the spec") with no delta format and no check, which fits the drift Böckeler describes; (c) the requirement areas of `image-client.md` (provider connections, model catalog, runs and jobs, spending, errors and moderation, settings) are natural domain splits; (d) the touch and tap regressions had no spec home at all (platform behaviour), see `fix-regressions-across-tasks`.
- A tree proposal for ImageGen is **not made yet**: it needs the owner's choices (open questions below).

## 4. Open questions for the owner
1. Is the goal a tree for ImageGen only, or the universal guide's recommendation for any project (my default: the guide, with ImageGen as the example)?
2. Should current-truth specs be merged from finished tasks by delta (OpenSpec style) or stay manually edited?
3. Notation: EARS, Given/When/Then, or free text with scenarios? (EARS: `research/unambiguous-complete-specs.md`.)
4. Where should rules and the constitution live relative to the spec tree (`rules-placement`)?
