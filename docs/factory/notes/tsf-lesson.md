# Notes on `tsf-lesson` (TSF lesson 2)

Source: `docs/factory/sources/tsf-lesson.md`. Each claim below is stated by the source; the "Maps to" column is our interpretation, to be checked.

## Claims

| # | Claim in the source | Maps to in this repo (hypothesis) |
|---|---|---|
| `meta-optimization` | The leverage is a meta-optimization of how you work, not the primitives (model, tools, harness). | The harness (ADR 0032-0036) is already such a meta layer. |
| `loop-position` | Three modes: in the loop (human is the bottleneck), near the loop (agents run around the clock, stop only at decision points), out of the loop (no human expected). TSF targets in -> near. | Owner currently decides at approvals and reviews; the near-loop equivalent is "stop only at decision points". |
| `liquid-software` | Two design goals: deployable in three steps (init, configure, specialize); takes the shape of the problem ("liquid software"), keeps changing shape as the problem is learned. | Our harness is repo-bound; no init/configure/specialize path exists. |
| `empty-factory` | Empty factory: a generic template with good defaults and no purpose until specialized. | Gap: our harness is mixed with ImageGen content, not a separable template. |
| `init-step` | Init = one skill, one command, copies the empty factory into a target (green or brownfield) repo. Done by an in-loop agent. | Gap. |
| `configure-step` | Configure = owner and agent write a large plan prompt (~150 lines in the demo), passed to a configure skill; the agent builds the system that builds the system. | Resembles planner writing specs and tasks, but one level up (it configures the factory itself). |
| `specialize-step` | Specialize = the factory takes one concrete role (demo: product-analytics agent for ImageGen). One super-agent is the wrong shape; specialization is the advantage. | Our planner/executor split is specialization by role, not by problem. |
| `tool-summaries` | Tool summaries: a cheap model summarizes tool calls so a human can follow at a glance; configurable frequency, lines, prompt. "Engineer and measure, or look away and pray." | Observability for the owner. We have task reports, no live summary. |
| `subagent-budgets` | Sub-agent delegation with per-agent model tier, system prompt, tools, time budget and spend budget; defaults e.g. 25 cents / 10 min / 4 agents; every run traced (input, tool calls, output). Downscale models when the answer need not be perfect. | We have "helper agents" rules in `docs/tasks.md`, but no budgets or traces. |
| `agentic-access-parity` | Agentic access parity: everything a human can do in the UI (settings, theme, navbar, hotkeys), the agent can do too. | Principle to test against our harness: can the agent do every owner action? |
| `measure-everything` | Measurement as a principle: "if you don't measure, you can't improve"; all runs visible and customizable ("we own our agentic tooling"). | Gap: no metrics in the harness. |
| `feature-map` | Feature map: levels 1-4, 14 features at level 2, each placed on effort, value, skill. Lesson 3 = small agent teams that communicate. | Useful structure for our own roadmap. |

## Terms to define in the concept

- loop position (in / near / out), liquid software, empty factory, init / configure / specialize, agentic access parity, tool summary, sub-agent budget.

## Open questions

Raised by the owner on 2026-11-17. Status: first-pass answers in `docs/factory/qa/tsf-lesson-questions.md` (2026-11-17); `liquid-software`, `liquid-factory`, `loop-modes`, `adaptation` answered partially, `meta-optimization-how` and `agentic-engineering` need independent sources, `tsf-product` and `tsf-skill` not confirmed. "Anchor" = where the source touches the question. Answers must cite sources by number; the author's marketing claims are to be separated from verifiable facts.

| # | Question | Anchor in `tsf-lesson` | Expected answer |
|---|---|---|---|
| `liquid-software` | What is "liquid software"? | `liquid-software`: software that changes shape with the problem, designed from day one to be customized by its user | definition, who coined it, prior art (if any), how it differs from configurable or extensible software |
| `liquid-factory` | What is a "liquid software factory"? | `liquid-software`, `empty-factory`: the tiny software factory is called liquid software | how the factory differs from liquid software in general; what is liquid in it (template, skills, settings, agents?) |
| `meta-optimization-how` | The source speaks of a meta-optimization of how a programmer works and of what is built. How to do it? | `meta-optimization`: leverage is in the meta-optimization, not in the models, tools and harnesses | a concrete method: what is measured, what is changed, how often, with which feedback |
| `loop-modes` | Human in the loop -> near the loop -> out of the loop: more detail | `loop-position` | exact definitions, decision points, stop conditions, examples, what must be true before moving to the next mode, risks of "out" |
| `agentic-engineering` | How does agentic engineering work in general? | `meta-optimization`, `configure-step`, `specialize-step` (the term is used, not defined) | the discipline: practices, roles, loop structure, verification; sources beyond the video |
| `tsf-product` | What are Tiny Software Factories (the course/product)? Find more than the video says | `liquid-software`-`init-step`, `feature-map` (the video is an advertisement for it) | the product: author, content of levels 1-4, the template, price/license, independent descriptions or reviews |
| `adaptation` | How does a factory adapt to the problem and to the progress of development? | `liquid-software`, `configure-step`, `agentic-access-parity` (agent edits its own settings; configure step) | the adaptation mechanism: what changes (config, skills, agents, UI), who triggers it, how it is kept consistent over time |
| `tsf-skill` | Find the "tiny software factory" (TSF) skill that is shown | `init-step`, `configure-step`: one "super skill" with init and configure workflows, `/tsf` command, `.tsf` directory | the actual skill file or repository, its commands and structure; if it is not public, say so |

Earlier open questions (still open):

1. The source describes a UI-driven runtime (settings menu, themes, hotkeys). How much of that is essential, and how much is demo detail for the concept?
2. Level 1 (foundations) and levels 3-4 are not in this source yet. Are more sources coming? (Part of this is `tsf-product`.)
3. Is "out of the loop" in scope, or only "near the loop"? (Depends on `loop-modes`.)
