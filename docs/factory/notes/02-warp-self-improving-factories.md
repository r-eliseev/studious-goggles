# Notes on source 02 (Zach Lloyd, Warp)

Source: `docs/factory/sources/02-warp-self-improving-factories.md`. Claims are the speaker's; "Maps to" is our hypothesis. Comparison with source 01 is in the last section.

## Claims

| # | Claim in the source | Maps to in this repo (hypothesis) |
|---|---|---|
| W1 | Thesis: software engineering becomes factory engineering; engineers build and manage the factory ("you build the thing that builds the product", meta-engineering, like process engineering). | Matches the planner/executor harness; the owner's job is already harness design. |
| W2 | Evolution: chat and autocomplete -> interactive agents (now) -> automation of the whole lifecycle (next 6-12 months). Every sizable project will have a factory, like CI/CD. | Positions our harness as an early factory. |
| W3 | Lifecycle loop: idea -> triage -> spec (if complex) -> implementation -> review (agent first, then human) -> verification -> ship -> monitor -> back to ideas. Humans step in at: spec review, code review, product review. | Ours: owner idea -> planner spec/ADR/task -> approval -> executor -> review. Missing: triage, verification, monitoring, feedback into intake. |
| W4 | Triage agent: easy and unambiguous issues go straight to implementation; hard ones get a spec. The way to start a factory. | Gap: all our tasks go through full planning. |
| W5 | Two specs: product spec (product invariants) and tech spec (architecture, shape of code). | Ours: `docs/specs/` plus task "Plan". Worth comparing: do we separate invariants from architecture? |
| W6 | Review is the most painful step. Agent reviews first; humans enter by risk management (when is a human needed). | Ours: planner review of every task. No risk-based routing. |
| W7 | Verification step: computer use, videos, screenshots, CI/CD. | Partly: Acceptance tests (Reqnroll), CI. No computer-use verification of UI. |
| W8 | Monitoring: agents observe what shipped (crashes, usage) and feed it back to the top of the loop. | Gap. |
| W9 | Components: automations, context and skills, human-in-the-loop at the right moment (when stuck), self-improvement loops. | Skills = `.claude/skills/`; knowledge = `docs/knowledge/`. Self-improvement is manual (ADRs after incidents). |
| W10 | Architecture of a scaled factory: intake channels (tracker, Slack, terminal/IDE, monitoring) -> control plane (distributes work) -> execution plane (cloud sandboxes; harness and model choice) -> data plane (agents remember, learn, improve). | Control plane ~ claim/release scripts; data plane ~ docs + task reports; gap: automatic intake and memory. |
| W11 | Measure and improve: software shipped, cost in human time and token time. | Same gap as C11 in source 01. |
| W12 | Skill loop: observer agents watch how skills are applied (e.g. humans correcting a review agent's comments) and improve the skills for the next run. | Strongest concrete mechanism for self-improvement; we have no equivalent. |
| W13 | Build vs buy: a simple factory is easy; one that scales is infrastructure, so focus on your own product; but tuning the factory to your domain stays your job. | Argues for a thin, domain-specific layer over generic infrastructure. |
| W14 | Open source as a public factory (build.warp.dev shows each issue's state and its agents); automation tames noisy issues and sloppy PRs. | Optional; the repo's multi-agent claim/branch protocol is a small private version. |
| W15 | Human taste, product sense and judgment at touch points are essential; a factory churning out what nobody wants is pointless. Engineers still need to understand systems and specs. | Owner role: goals (`docs/product.md`) and approvals. |

## Comparison with source 01 (TSF lesson 2)

| Topic | Source 01 (TSF) | Source 02 (Warp) |
|---|---|---|
| Human position | in / near / out of the loop; goal: near | Humans at defined blue boxes (spec, code, product review); risk-based entry |
| Factory shape | liquid software: an empty template, init / configure / specialize, reshaped by agents | a graph of lifecycle steps, similar for every product; tuning per domain |
| Self-improvement | agent edits its own settings (access parity) | observer agents improve skills from human corrections (skill loop) |
| Measurement | tool summaries, sub-agent traces and budgets | efficiency: output vs human time and token cost |
| Granularity | small specialized agents with budgets | agents per lifecycle stage |
| Scope | the factory tool itself | the process around the product |

They are complementary: 01 describes how to make and reshape the factory (the tool); 02 describes what the factory does (the lifecycle) and how it improves (loops).

## Terms to define

- software factory, factory floor (a graph of steps), triage, product spec vs tech spec, control plane / execution plane / data plane, skill loop, observer agent, human touch points.

## Open questions

1. Which lifecycle stages should be in our v1 factory: the full loop (including monitoring) or planning-to-review only?
2. Where is the line between "factory infrastructure" and "domain tuning" for us (W13)?
3. A skill loop needs a signal (human corrections). What is that signal in our harness: review comments, returned tasks, ADR amendments?
