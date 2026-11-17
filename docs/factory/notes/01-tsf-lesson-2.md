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

1. The source describes a UI-driven runtime (settings menu, themes, hotkeys). How much of that is essential, and how much is demo detail for the concept?
2. Level 1 (foundations) and levels 3-4 are not in this source yet. Are more sources coming?
3. Is "out of the loop" in scope, or only "near the loop"?
