# Notes on source 01 (TSF lesson 2)

Source: `docs/factory/sources/01-tsf-lesson-2.md`. Each claim below is stated by the source; the "Maps to" column is our interpretation, to be checked.

## Claims

| # | Claim in the source | Maps to in this repo (hypothesis) |
|---|---|---|
| C1 | The leverage is a meta-optimization of how you work, not the primitives (model, tools, harness). | The harness (ADR 0032-0036) is already such a meta layer. |
| C2 | Three modes: in the loop (human is the bottleneck), near the loop (agents run around the clock, stop only at decision points), out of the loop (no human expected). TSF targets in -> near. | Owner currently decides at approvals and reviews; the near-loop equivalent is "stop only at decision points". |
| C3 | Two design goals: deployable in three steps (init, configure, specialize); takes the shape of the problem ("liquid software"), keeps changing shape as the problem is learned. | Our harness is repo-bound; no init/configure/specialize path exists. |
| C4 | Empty factory: a generic template with good defaults and no purpose until specialized. | Gap: our harness is mixed with ImageGen content, not a separable template. |
| C5 | Init = one skill, one command, copies the empty factory into a target (green or brownfield) repo. Done by an in-loop agent. | Gap. |
| C6 | Configure = owner and agent write a large plan prompt (~150 lines in the demo), passed to a configure skill; the agent builds the system that builds the system. | Resembles planner writing specs and tasks, but one level up (it configures the factory itself). |
| C7 | Specialize = the factory takes one concrete role (demo: product-analytics agent for ImageGen). One super-agent is the wrong shape; specialization is the advantage. | Our planner/executor split is specialization by role, not by problem. |
| C8 | Tool summaries: a cheap model summarizes tool calls so a human can follow at a glance; configurable frequency, lines, prompt. "Engineer and measure, or look away and pray." | Observability for the owner. We have task reports, no live summary. |
| C9 | Sub-agent delegation with per-agent model tier, system prompt, tools, time budget and spend budget; defaults e.g. 25 cents / 10 min / 4 agents; every run traced (input, tool calls, output). Downscale models when the answer need not be perfect. | We have "helper agents" rules in `docs/tasks.md`, but no budgets or traces. |
| C10 | Agentic access parity: everything a human can do in the UI (settings, theme, navbar, hotkeys), the agent can do too. | Principle to test against our harness: can the agent do every owner action? |
| C11 | Measurement as a principle: "if you don't measure, you can't improve"; all runs visible and customizable ("we own our agentic tooling"). | Gap: no metrics in the harness. |
| C12 | Feature map: levels 1-4, 14 features at level 2, each placed on effort, value, skill. Lesson 3 = small agent teams that communicate. | Useful structure for our own roadmap. |

## Terms to define in the concept

- loop position (in / near / out), liquid software, empty factory, init / configure / specialize, agentic access parity, tool summary, sub-agent budget.

## Open questions

Raised by the owner on 2026-11-17. Status: first-pass answers in `docs/factory/qa/01-questions-on-source-01.md` (2026-11-17); Q1, Q2, Q4, Q7 answered partially, Q3 and Q5 need independent sources, Q6 and Q8 not confirmed. "Anchor" = where the source touches the question. Answers must cite sources by number; the author's marketing claims are to be separated from verifiable facts.

| # | Question | Anchor in source 01 | Expected answer |
|---|---|---|---|
| Q1 | What is "liquid software"? | C3: software that changes shape with the problem, designed from day one to be customized by its user | definition, who coined it, prior art (if any), how it differs from configurable or extensible software |
| Q2 | What is a "liquid software factory"? | C3, C4: the tiny software factory is called liquid software | how the factory differs from liquid software in general; what is liquid in it (template, skills, settings, agents?) |
| Q3 | The source speaks of a meta-optimization of how a programmer works and of what is built. How to do it? | C1: leverage is in the meta-optimization, not in the models, tools and harnesses | a concrete method: what is measured, what is changed, how often, with which feedback |
| Q4 | Human in the loop -> near the loop -> out of the loop: more detail | C2 | exact definitions, decision points, stop conditions, examples, what must be true before moving to the next mode, risks of "out" |
| Q5 | How does agentic engineering work in general? | C1, C6, C7 (the term is used, not defined) | the discipline: practices, roles, loop structure, verification; sources beyond the video |
| Q6 | What are Tiny Software Factories (the course/product)? Find more than the video says | C3-C5, C12 (the video is an advertisement for it) | the product: author, content of levels 1-4, the template, price/license, independent descriptions or reviews |
| Q7 | How does a factory adapt to the problem and to the progress of development? | C3, C6, C10 (agent edits its own settings; configure step) | the adaptation mechanism: what changes (config, skills, agents, UI), who triggers it, how it is kept consistent over time |
| Q8 | Find the "tiny software factory" (TSF) skill that is shown | C5, C6: one "super skill" with init and configure workflows, `/tsf` command, `.tsf` directory | the actual skill file or repository, its commands and structure; if it is not public, say so |

Earlier open questions (still open):

1. The source describes a UI-driven runtime (settings menu, themes, hotkeys). How much of that is essential, and how much is demo detail for the concept?
2. Level 1 (foundations) and levels 3-4 are not in this source yet. Are more sources coming? (Part of this is Q6.)
3. Is "out of the loop" in scope, or only "near the loop"? (Depends on Q4.)
