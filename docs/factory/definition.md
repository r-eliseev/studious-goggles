# Definition of a software factory (living document)

Status: **v1**, 2026-11-17. Updated as the owner's questions are answered (`docs/factory/qa/05-workshop-answers.md`, method W05). Each version keeps its reason. The concept is universal (principle P1): it is meant to describe humans, agents and mixed teams, with this repository's harness as the first example.

## Version history

| Version | Text | Reason for the change |
|---|---|---|
| v0 | "A repeatable delivery system where AI agents perform coordinated development tasks, and humans set intent, constraints, acceptance criteria and approval rules." (reply to M01, from secondary blogs: Faros, Sonar, DZone) | first pass; owner (W01, W05): unclear, how does it differ from the classical "top sets intent, simple executors carry out", and it is the general question of delegation and verification |
| v1 | see below | adds: the verification condition, the feedback loop, the decision-point rule, the contrast with the classical delegation scheme and with an agent or a CI pipeline |

## v1

> A **software factory** is a repeatable arrangement of work in which humans state **intent** and the **rules of acceptance**, and AI agents carry out everything between intent and a verified result (planning, building, reviewing, verifying, releasing). Three conditions make it a factory: (1) **verification of a result is independent of whoever produced it**; (2) **boundaries of action are enforced by the system, not by instructions**; (3) **what happens after release returns to the input**. A human is called only at defined **decision points**.

In one line: *the answer to "how do we know the system does what it should, and that what it should do is right" is built into the arrangement as mechanisms, so that the work can continue without a human present.*

### Why "factory" and not "agent"

An agent is an executor inside one step. A factory is the **process around the agents**: stages, durable stores (inbox of work, memory, outbox of reports, timers), boundaries, measured output, and the rules for when a human is involved. A factory can run with no human at the keyboard (source 04: runs triggered by events); a single chat agent cannot.

### Parts

| Part | Role | Held by | Sources |
|---|---|---|---|
| Intent layer | goals, specs, acceptance criteria, rules of approval ("when do I step in") | human (arbiter on ambiguity) | S01 C2; S02 W3, W15; S03 T8, T5; M21, M24, M36 |
| Production layer | planning, implementation, review, documentation | agents | S02 W3; kits (`research/02-factory-kits-code-reading.md`) |
| Verification layer | independent checks, hidden scenarios, fakes of external services, a human arbiter for disputes | separate agents or processes, humans for decisions | S03 T5, T2, T13; M14, M31, M32 |
| Boundary layer | permissions enforced outside the agent, isolation, protected state | system | S03 T8, T10; S04 (permissions); kits |
| Runtime layer | inbox, timers, memory, outbox, runs and sessions | system | S04 |
| Feedback layer | monitoring back to intake; observer loops that improve skills | agents with system support | S02 W8, W12; S03 T10 |
| Decision points | where a human answers (spec review, code review, product review; deferred tool calls) | human, asynchronously | S02 W03; S04; W02 |

### What a software factory is not

- **Not a single agent or a chat:** no durable process, no independent verification (source 01 argues one super-agent is the wrong shape).
- **Not a CI pipeline:** a pipeline runs deterministic steps; a factory delegates **judgment** (planning, review, interpretation) to agents and wraps it in checks.
- **Not the classical (Microsoft, 2004) factory:** that one generated code deterministically from a model in a DSL (Greenfield and Short, M02/M08); the AI factory accepts probabilistic execution and moves the effort into verification. A hybrid (deterministic generation for routine parts, agents for the rest) is possible.
- **Not "no humans":** the dark factory (Shapiro level 5) is one end of a scale; the owner's goal is level 4 with understanding kept (M25).

### Difference from the classical delegation scheme (W01)

Same shape (centralized intent, decentralized execution; "mission command"), different executor: cannot ask in unattended mode, no accumulated context, fast and cheap, quiet confident mistakes, no personal stakes, correlated errors among copies. Therefore trust that humans earn over years becomes **mechanisms**: independent verification, enforced boundaries, a trust level granted per action and area and raised as the checks earn it (NATO doctrine as read in `human-org-parallels.md`; owner note N2).

### Example: this repository as a small factory

| Part | Here (hypothesis, task 291 will check) |
|---|---|
| Intent | owner's goals (`docs/product.md`), standing instructions (`docs/status.md`) |
| Production | planner agent writes specs, ADRs, plans; executor agent implements one approved task |
| Verification | tests, CI, `scripts/check.sh`, planner review; partial (no hidden scenarios, no independent reviewer model) |
| Boundaries | prohibitions in `AGENTS.md` (instruction-level); `check-tasks.sh` guards task sections |
| Runtime | task files and claim scripts (inbox, single worker); task Report and Comments (progress); no timers; no formal outbox |
| Feedback | ADRs after incidents (manual); no monitoring input |
| Decision points | plan approval (a standing instruction already given), `blocked` status with a question |

### Open points (owner's word needed)

1. **Boundary of the definition:** must a factory include the *feedback layer* (monitoring to intake), or can a one-directional pipeline from intent to verified result already be called a factory? (v1 says it must; source 02 says so; source 03 never required it.)
2. **Independent verification as a condition:** v1 makes it mandatory. Is "independent" enough as "a different process with a different source of truth", or must it be a different model or a human?
3. **Decision points as the only human involvement:** is that the owner's meaning of "near the loop" (source 01), or should there also be scheduled human attention (reviews, audits) regardless of events?
4. **Humans-only organizations:** the owner noted that the same questions apply there (W04). Do we want the definition to cover them explicitly (a factory with zero agents), or only note the equivalence?
5. **Name:** keep "software factory" or choose a term that does not suggest the classical deterministic factory?
