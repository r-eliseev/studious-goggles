---
id: 294
status: todo
assignee:
claimed:
created: 2026-11-17
---

# Plan an experiment: error diversity and independence of verification

## Description
Owner (W08, W14.2): copies of one model fail alike; how to make even one model err differently, and what minimum of independence the definition requires. The state of knowledge is in `docs/factory/concepts/error-diversity.md` and `knight-leveson.md`: changing the model dominates, changing the prompt barely helps, ensembles realize 0.43-0.44 (different models) and under 0.3 (one model) of the independence gain, the specification is a source of common failures. Missing: a comparison of personas of one model against different models on code tasks, and any measurement of independence in this factory's own verification.

Depends on: the owner's choice of the wording (A, B or C) of independence in `definition.md` (OT04); access to more than one model family is a question for the owner (P6).
Environment: any with the model access the owner allows.

## Acceptance criteria
- A plan of an experiment that measures failure correlation between producer and checker on a set of tasks drawn from this repository's own tasks, comparing at least: same model same context, same model different information, different model, executable property or differential check.
- The plan states the measure (pairwise failure correlation and the share of the independence gain realized), the sample size and its limits, and how results will be graded in `docs/factory/evidence-register.md` (our own data, graded by sample size).
- The plan lists what must be asked of the owner before running (model access, cost limit).
- No experiment runs before the owner approves the plan.

## Analysis

## Plan

## Report

## Review

## Comments

### 2026-11-17, planner-session
Created from W08 and W14.2; not started.
