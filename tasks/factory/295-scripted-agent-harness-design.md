---
id: 295
status: todo
assignee:
claimed:
created: 2026-11-17
---

# Design a scripted harness around a ready-made agent

## Description
Owner's request (2026-11-17): add the topic "script harnesses around ready-made agents", for example running Claude Code in ACP mode with tasks while a script controls it. Verified background and four questions for the owner: `docs/factory/research/scripted-harness-around-agents.md`. Today in this repository the agent follows the rules of `docs/executor.md` and calls scripts; a controller script would own the order of work, rights by risk class (`risk-classes`), budgets and acceptance, and call a human at decision points.

Depends on: the owner's answers to the four questions (one agent or a queue, what is controlled first, where it runs, why ACP).
Environment: any (documents); the design may later need access to Claude Code in ACP mode, to be checked.

## Acceptance criteria
- The owner's answers to the four questions are recorded.
- The design compares the control surfaces (ACP, `claude -p`, the Agent SDK, hooks) for a script-controller and says what each allows (permission requests, streamed updates, cancellation, structured output), citing the documentation.
- The design shows, on this repository, the flow from picking an approved task to submitting it, with the points where the script answers a permission request by policy and where it routes to a human (`irreversible-actions-through-human`, `factory-rules`).
- It states what the script can enforce that the current rules cannot (independence of verification, risk-class policy, budgets) and what stays with the agent.
- Nothing is installed or run before the owner approves a plan.

## Analysis

## Plan

## Report

## Review

## Comments

### 2026-11-17, planner-session
Created from the owner's request; queued as the next topic. Analysis and Plan not written yet.

### 2026-11-17, planner-session
Owner's answers (recorded in `docs/factory/decisions.md`, `scripted-harness-requirements`): one agent per task **and** a queue of tasks with several agents; on the owner's machine **and** in the cloud; the script controls rights, budget, checks and escalations first; any control surface is fine as long as **the agent cannot bypass the control**. Verified background added to `docs/factory/research/scripted-harness-around-agents.md`: the ACP file-system and terminal methods are optional client capabilities (not enforcement), the Claude Code adapter enables the built-in tools by default and can disable them with a client flag, the Agent SDK evaluates hooks, deny rules, ask rules, permission mode, allow rules and then the callback, with traps (no callback in `bypassPermissions`, pattern denies match only as written). New criterion: the design includes an end-to-end check that the agent cannot bypass the host (tools served by the host or OS isolation).

