---
id: 292
status: todo
assignee:
claimed:
created: 2026-11-17
---

# Write the guide: how to build a universal software factory

## Description
The owner's goal (2026-11-17, `goal-and-no-difference`) is an **instruction on how to build a universal software factory**; the definition (`docs/factory/definition.md`), sources, catalog (`docs/factory/catalog.md`), concepts and Q&A are means. The guide must treat people and agents without a difference in kind and state clearly why (executor profile, `definition.md`), name where differences of degree change the design, and be usable for any repository, not only this harness.

Depends on: owner's answers to open points `independence-wording`, `review-design` (`docs/factory/open-threads.md`) for the chapters on verification independence and human review; the guide can start with the other chapters.
Environment: any (documents).

## Acceptance criteria
- An outline of the guide (chapters, order, what each chapter decides) is proposed to the owner and approved before the text is written (rule `ask-dont-assume`: ask first).
- Each chapter cites the catalog items it uses (C-ids), the concepts and the evidence grade of the empirical claims it relies on (`docs/factory/evidence-register.md`).
- The guide states the executor model and the four conditions of the definition (independent and measured verification, enforced boundaries, feedback loop, decision points and regular reviews) as steps with checks, not as slogans.
- For each mechanism the guide says what it is for, how to build it, how to verify that it works, and what it costs; mechanisms not applicable to a given kind of repository are kept with a note (principle `document-everything`).
- Where a section depends on an open owner decision it says so and lists the options.

## Analysis

## Plan

## Report

## Review

## Comments

### 2026-11-17, planner-session
Created from the owner's statement that the goal is the instruction, not the definition. Outline not written yet; per `ask-dont-assume` the outline goes to the owner first.

### 2026-11-17, planner-session
Owner's rules for the guide: mnemonic names instead of numbered codes (`mnemonic-names`) and a living example in every chapter, the example being this repository (`living-example`, `docs/factory/case-study-imagegen.md`).
