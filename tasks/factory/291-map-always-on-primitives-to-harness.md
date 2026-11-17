---
id: 291
status: todo
assignee:
claimed:
created: 2026-11-17
---

# Map the always-on agent primitives onto this harness and the factory concept

## Description
Source 04 (`docs/factory/sources/04-anthropic-always-on-agents.md`, notes in `docs/factory/notes/04-always-on-agents.md`) describes five primitives of an agent that runs unattended: event inbox, timers, memory, permissions (credential proxy, auto mode, deferred tool calls) and outbox (with pacing). The notes contain a first hypothesis table of how each maps to this repository's harness (task files and claim scripts as inbox, `docs/status.md` standing instructions as user instructions, task Report/Comments as task progress, status `blocked` as a deferred call) and which parts are missing (timers, outbox with pacing, dreaming, credential proxy). The mapping has not been checked against the scripts and ADRs.
Related: owner note N1 (attention metrics), N2 (human mode per action), tasks 287 (autonomy ladders) and 288 (factory kits compared by code).

Depends on: none.
Environment: any (documents).

## Acceptance criteria
- For each of the five primitives: what this harness has today (with the file or script), what is missing, and a proposal sized small / medium / large, with the owner's decision marked where needed.
- A decision proposal for the "deferred tool call" equivalent: how a planner or executor agent asks the owner a question at a decision point and gets the answer back as an event (status, file, notification), compared with the current `blocked` status.
- A proposal for the outbox: what the owner is told, when, and how the number of messages is measured and tuned (N1), including "start quiet and earn more attention".
- A statement of how the always-on runtime relates to sources 01-03 (which parts of the concept it covers, which it does not: verification stays with source 03).
- No change to `docs/tasks.md`, ADRs or `scripts/`; the Report lists the decisions needed from the owner.

## Analysis

## Plan

## Report

## Review

## Comments

### 2026-11-17, planner-session
Created when source 04 was integrated (owner's request: read the article and integrate it into the system). Analysis and Plan not written yet (status `todo`).
