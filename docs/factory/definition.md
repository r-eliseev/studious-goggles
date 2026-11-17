# Definition of a software factory (living document)

Status: **v1.2**, 2026-11-17. The definition is a means: the goal is an instruction on how to build a universal factory (task 292, `docs/factory/README.md`). Updated as the owner's questions are answered (`docs/factory/qa/owner-session-answers.md`, method `definition-and-method`). Each version keeps its reason. The concept is universal (principle `document-everything`): it is meant to describe humans, agents and mixed teams, with this repository's harness as the first example.

## Version history

| Version | Text | Reason for the change |
|---|---|---|
| v0 | "A repeatable delivery system where AI agents perform coordinated development tasks, and humans set intent, constraints, acceptance criteria and approval rules." (reply to `how-factories-work`, from secondary blogs: Faros, Sonar, DZone) | first pass; owner (`vs-classical-delegation`, `definition-and-method`): unclear, how does it differ from the classical "top sets intent, simple executors carry out", and it is the general question of delegation and verification |
| v1 | "A software factory is a repeatable arrangement of work in which humans state intent and the rules of acceptance, and AI agents carry out everything between intent and a verified result. Three conditions: (1) verification independent of whoever produced the result; (2) boundaries enforced by the system, not by instructions; (3) what happens after release returns to the input. A human is called only at defined decision points." | adds: the verification condition, the feedback loop, the decision-point rule, the contrast with the classical delegation scheme and with an agent or a CI pipeline |
| v1.1 | executor-agnostic statement below ("principals" and "executors" instead of "humans" and "AI agents"; independence is measured; regular reviews added) | owner `five-open-definition-points`: feedback layer mandatory (1); the point is a definition with **no difference between humans and agents** and a clear reason why (4); human needed at regular reviews (3); the definition is not the goal (4) |
| v1.2 | independence wording decided (provisional); the factory's own rules are always reviewed by a human together with an agent; human reviews are triggered by events and evidence, not by a calendar; the living example (this repository) is part of the definition | owner: `independence-wording` (chosen, to be revisited), `review-factory-rules`, `review-regularity`, `living-example` |

## v1.2 (current; the statement is unchanged from v1.1 except condition 4)

> A **software factory** is a repeatable arrangement of work in which **principals** state **intent** and the **rules of acceptance**, and **executors** (people, AI agents, or both) carry out everything between intent and a verified result (planning, building, reviewing, verifying, releasing). Four conditions make it a factory: (1) **verification of a result is independent of whoever produced it, and the independence is measured, not assumed**; (2) **boundaries of action are enforced by the system, not by instructions to the executor**; (3) **what happens after release returns to the input** (mandatory, owner `feedback-layer-mandatory`); (4) principals take part only at defined **decision points** and at **regular reviews** of intent, samples of results, the factory's own rules and indicators (owner `review-design`; what and how often: `concepts/review-cadence.md`, open).

In one line: *the answer to "how do we know the system does what it should, and that what it should do is right" is built into the arrangement as mechanisms, so that the work can continue without a principal present.* The same sentence holds for an organization of only people, of only agents, or a mix.

### Why there is no difference between people and agents (and where it seems to be one)

The factory is defined over an **executor profile**, not over "who the executor is". The parameters (typical values are descriptive, not exact):

| Parameter | Typical person | Typical agent | Design consequence |
|---|---|---|---|
| Capability (what tasks it can do) | broad, slow to train | broad, narrow at the edges, changes with model | assign work by profile, check by outcome |
| Reliability and **error correlation with other executors** | errors correlated more than expected (Knight-Leveson 1986) | correlated more strongly within a model (2026 studies) | verification independence must be measured (`concepts/knight-leveson.md`, `error-diversity.md`) |
| Cost and speed | high cost, hours to days | low cost, minutes; scalable in parallel | the bottleneck moves to review; review must be risk-based and partly mechanical |
| Availability | office hours | always | unattended operation needs inbox, timers, outbox (`always-on-agents`) |
| Memory between tasks | personal, tacit | only what is written (`always-on-agents`) | write rules, reasons, progress outward (ADRs, memory files) |
| Ability to ask | yes, cheap | in unattended runs only through a deferred call | specs more complete, or decision points |
| Incentives and accountability | personal stakes; legally responsible | none of its own | a human principal stays accountable; mechanisms replace relationships |
| Gaming of measures | yes (Goodhart, Campbell) | yes, faster (optimization pressure) | hidden and rotating checks, several measures (`concepts/goodhart-campbell.md`) |
| Observability of its work | partial | full transcript | measure, audit, sample |
| Trust granted | earned over years, per area | must be earned per area by the checks | mode per action and area (`mode-per-action`, `mode-per-action`, `commanders-intent-trust`) |

**Claim (to be challenged):** these are parameters of one model, so the principles of a factory (independent verification, enforced boundaries, feedback, decision points, regular review) are the same for people and agents; replacing an executor changes parameter values, not the factory. **Where a real difference in kind might exist:** legal and moral accountability (stays with a human principal; the factory does not change it) and the nature of "stakes"; everything else in the table is a difference of degree, which becomes a difference in design only when it crosses a threshold (for example when review cost exceeds production cost).

### Why "factory" and not "agent"

An agent is an executor inside one step. A factory is the **process around the agents**: stages, durable stores (inbox of work, memory, outbox of reports, timers), boundaries, measured output, and the rules for when a human is involved. A factory can run with no human at the keyboard (`always-on-agents`: runs triggered by events); a single chat agent cannot.

### Parts

| Part | Role | Held by | Sources |
|---|---|---|---|
| Intent layer | goals, specs, acceptance criteria, rules of approval ("when do I step in") | human (arbiter on ambiguity) | `tsf-lesson`, `loop-position`; `warp-talk`, `lifecycle-loop`, `human-taste`; `owner-dialog`, `specs-and-agent-boundaries`, `holdout-oracle-independence`; `how-to-write-specs`, `grow-spec-writing-skill`, `cannot-fit-claim-too-strong` |
| Production layer | planning, implementation, review, documentation | agents | `warp-talk`, `lifecycle-loop`; kits (`research/factory-kits-code-reading.md`) |
| Verification layer | independent checks, hidden scenarios, fakes of external services, a human arbiter for disputes | separate agents or processes, humans for decisions | `owner-dialog`, `holdout-oracle-independence`, `mocks-twins-fakes`, `proving-checks-and-pilots`; `what-is-holdout`, `graded-satisfaction-populism`, `other-ways-to-independence` |
| Boundary layer | permissions enforced outside the agent, isolation, protected state | system | `owner-dialog`, `specs-and-agent-boundaries`, `rules-to-checks-cheap-failure`; `always-on-agents` (permissions); kits |
| Runtime layer | inbox, timers, memory, outbox, runs and sessions | system | `always-on-agents` |
| Feedback layer | monitoring back to intake; observer loops that improve skills | agents with system support | `warp-talk`, `monitoring-feedback`, `skill-loop`; `owner-dialog`, `rules-to-checks-cheap-failure` |
| Decision points | where a human answers (spec review, code review, product review; deferred tool calls) | human, asynchronously | `warp-talk`, `lifecycle-loop`; `always-on-agents`, `human-points-in-the-loop` |

### What a software factory is not

- **Not a single agent or a chat:** no durable process, no independent verification (`tsf-lesson` argues one super-agent is the wrong shape).
- **Not a CI pipeline:** a pipeline runs deterministic steps; a factory delegates **judgment** (planning, review, interpretation) to agents and wraps it in checks.
- **Not the classical (Microsoft, 2004) factory:** that one generated code deterministically from a model in a DSL (Greenfield and Short, `old-vs-new-compare`/`old-new-same-axis-objection`); the AI factory accepts probabilistic execution and moves the effort into verification. A hybrid (deterministic generation for routine parts, agents for the rest) is possible.
- **Not "no humans":** the dark factory (Shapiro level 5) is one end of a scale; the owner's goal is level 4 with understanding kept (`keeping-control`).

### Difference from the classical delegation scheme (`vs-classical-delegation`)

Same shape (centralized intent, decentralized execution; "mission command"), different executor: cannot ask in unattended mode, no accumulated context, fast and cheap, quiet confident mistakes, no personal stakes, correlated errors among copies. Therefore trust that humans earn over years becomes **mechanisms**: independent verification, enforced boundaries, a trust level granted per action and area and raised as the checks earn it (NATO doctrine as read in `human-org-parallels.md`; owner note `mode-per-action`).

### Living example: this repository (ImageGen)

Rule `living-example`: the definition is shown on a running example. The example, with the facts and the flow from start to end, is `case-study-imagegen.md`. In short: the owner states goals and decisions (`docs/product.md`) and gives standing instructions; the planner agent writes specs, ADRs and tasks; the executor agent implements one approved task at a time; CI and acceptance scenarios verify; the planner agent reviews; a human checks the product at the screen and decides open questions; nobody reads the code (owner's words). Gaps against the definition (independence of verification not measured, no monitoring after release, no formal outbox) are listed there.

### Open points

1. ~~Feedback layer~~ **decided: mandatory** (`feedback-layer-mandatory`).
2. ~~Independence wording~~ **decided provisionally** (measured independence; different method or source of truth by default); **to be revisited** when the owner understands better (`independence-wording-revisit`).
3. **Review design** (`review-design`): decided that the factory's own rules are reviewed by a human with an agent, triggers are events and evidence, no numeric threshold. Open: what the owner's review hours should be spent on, and the ranked list of what is reviewed by sampling.
4. ~~Humans-only organizations~~ **resolved as goal:** no difference in kind between people and agents (`goal-and-no-difference`); the executor profile above states why.
5. **The name** (`name-of-the-thing`): left open.
6. **Living example questions** (`case-study-imagegen`): how the owner checks that ImageGen does what he wants, what "done" means for it, when he would want to see code.
