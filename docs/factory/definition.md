# Definition of a software factory (living document)

Status: **v1.1**, 2026-11-17. The definition is a means: the goal is an instruction on how to build a universal factory (task 292, `docs/factory/README.md`). Updated as the owner's questions are answered (`docs/factory/qa/05-workshop-answers.md`, method W05). Each version keeps its reason. The concept is universal (principle P1): it is meant to describe humans, agents and mixed teams, with this repository's harness as the first example.

## Version history

| Version | Text | Reason for the change |
|---|---|---|
| v0 | "A repeatable delivery system where AI agents perform coordinated development tasks, and humans set intent, constraints, acceptance criteria and approval rules." (reply to M01, from secondary blogs: Faros, Sonar, DZone) | first pass; owner (W01, W05): unclear, how does it differ from the classical "top sets intent, simple executors carry out", and it is the general question of delegation and verification |
| v1 | "A software factory is a repeatable arrangement of work in which humans state intent and the rules of acceptance, and AI agents carry out everything between intent and a verified result. Three conditions: (1) verification independent of whoever produced the result; (2) boundaries enforced by the system, not by instructions; (3) what happens after release returns to the input. A human is called only at defined decision points." | adds: the verification condition, the feedback loop, the decision-point rule, the contrast with the classical delegation scheme and with an agent or a CI pipeline |
| v1.1 | executor-agnostic statement below ("principals" and "executors" instead of "humans" and "AI agents"; independence is measured; regular reviews added) | owner W14: feedback layer mandatory (1); the point is a definition with **no difference between humans and agents** and a clear reason why (4); human needed at regular reviews (3); the definition is not the goal (4) |

## v1.1 (current)

> A **software factory** is a repeatable arrangement of work in which **principals** state **intent** and the **rules of acceptance**, and **executors** (people, AI agents, or both) carry out everything between intent and a verified result (planning, building, reviewing, verifying, releasing). Four conditions make it a factory: (1) **verification of a result is independent of whoever produced it, and the independence is measured, not assumed**; (2) **boundaries of action are enforced by the system, not by instructions to the executor**; (3) **what happens after release returns to the input** (mandatory, owner W14.1); (4) principals take part only at defined **decision points** and at **regular reviews** of intent, samples of results, the factory's own rules and indicators (owner W14.3; what and how often: `concepts/review-cadence.md`, open).

In one line: *the answer to "how do we know the system does what it should, and that what it should do is right" is built into the arrangement as mechanisms, so that the work can continue without a principal present.* The same sentence holds for an organization of only people, of only agents, or a mix.

### Why there is no difference between people and agents (and where it seems to be one)

The factory is defined over an **executor profile**, not over "who the executor is". The parameters (typical values are descriptive, not exact):

| Parameter | Typical person | Typical agent | Design consequence |
|---|---|---|---|
| Capability (what tasks it can do) | broad, slow to train | broad, narrow at the edges, changes with model | assign work by profile, check by outcome |
| Reliability and **error correlation with other executors** | errors correlated more than expected (Knight-Leveson 1986) | correlated more strongly within a model (2026 studies) | verification independence must be measured (`concepts/knight-leveson.md`, `error-diversity.md`) |
| Cost and speed | high cost, hours to days | low cost, minutes; scalable in parallel | the bottleneck moves to review; review must be risk-based and partly mechanical |
| Availability | office hours | always | unattended operation needs inbox, timers, outbox (source 04) |
| Memory between tasks | personal, tacit | only what is written (source 04) | write rules, reasons, progress outward (ADRs, memory files) |
| Ability to ask | yes, cheap | in unattended runs only through a deferred call | specs more complete, or decision points |
| Incentives and accountability | personal stakes; legally responsible | none of its own | a human principal stays accountable; mechanisms replace relationships |
| Gaming of measures | yes (Goodhart, Campbell) | yes, faster (optimization pressure) | hidden and rotating checks, several measures (`concepts/goodhart-campbell.md`) |
| Observability of its work | partial | full transcript | measure, audit, sample |
| Trust granted | earned over years, per area | must be earned per area by the checks | mode per action and area (N2, C60, C68) |

**Claim (to be challenged):** these are parameters of one model, so the principles of a factory (independent verification, enforced boundaries, feedback, decision points, regular review) are the same for people and agents; replacing an executor changes parameter values, not the factory. **Where a real difference in kind might exist:** legal and moral accountability (stays with a human principal; the factory does not change it) and the nature of "stakes"; everything else in the table is a difference of degree, which becomes a difference in design only when it crosses a threshold (for example when review cost exceeds production cost).

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

### Open points

1. ~~Must a factory include the feedback layer?~~ **Resolved: yes, mandatory** (W14.1).
2. **Independence as a condition: minimum wording.** Open, needs the owner's choice between A (different executor), B (different method or source of truth) and C (independence measured; at least one of four axes differs), recommendation C with B as default (`qa/05-workshop-answers.md` W14.2; `open-threads.md` OT04).
3. **Regular human reviews.** The condition is in v1.1; what and how often is open (`concepts/review-cadence.md`, `open-threads.md` OT05, task 293).
4. ~~Humans-only organizations~~ **Resolved as goal:** the definition and the guide treat people and agents without a difference in kind (W14.4); the claim and its limits are in the executor-profile section above.
5. **The name.** Left open by the owner (OT06).
