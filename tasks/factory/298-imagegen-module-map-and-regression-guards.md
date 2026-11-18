---
id: 298
status: todo
assignee:
claimed:
created: 2026-11-18
---

# ImageGen module map and guards against rolled-back fixes

## Description
Owner's request (2026-11-18), on the quote "what is missing is a module map for 737 C# files; `docs/architecture.md` has 160 lines but no detailed map of modules and contracts": add a task and do it. The owner also confirmed that fixes were rolled back by later tasks, in cases connected with touch and click handling differences between platforms in MAUI (`fix-regressions-across-tasks` in `docs/factory/open-threads.md`; related knowledge in `docs/knowledge/maui.md`, task 088).

The task has two parts: (1) a module map with contracts (what each module owns, what it may depend on, which contracts and invariants it must keep), kept in the repository so agents read it just in time instead of rediscovering it; (2) for the rolled-back fixes, find the mechanism and put a guard in place, so the next agent has something to fail on. Practices behind it: `docs/factory/research/context-management-practices.md`.

Depends on: none.
Environment: Linux for the map and headless tests; the platform checks of touch and clicks need the platforms named in the existing tasks (Windows, Android), unknown which ones are available.

## Acceptance criteria
- A module map exists: every project and each major folder of `ImageGen/` has an entry (purpose, owner module, allowed dependencies, public contracts, invariants, where the tests are); a script or check reports files and projects missing from the map (so the map cannot silently go stale).
- The map is short enough to be read in one window and links to details; it states what is NOT covered.
- The rolled-back fix cases are identified from the history (commits and task reports) with links; for each one: which task rolled it back, why, and whether a written fact existed.
- For each case, one of: an automated regression test, a note in `docs/knowledge/` with a check that references it, or an explicit statement why neither is possible (for example, behaviour verifiable only on a physical platform) with the manual check recorded.
- A test of usefulness: take at least three past tasks, give an agent the map and a description of the task, and record whether the map would have prevented the observed breakage (honest result, including "no").

## Analysis

## Plan

## Report

## Review

## Comments

### 2026-11-18, planner-session
Created on the owner's instruction ("add a task, we will do it") attached to the module map quote; if the owner meant another item, the task is to be redirected. Not started.
