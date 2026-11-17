---
id: 293
status: todo
assignee:
claimed:
created: 2026-11-17
---

# Design the regular human reviews: what and how often

## Description
Owner (`review-design`): the human is needed at regular reviews; it must be decided how often and what he reviews. A first design with sources and open parameters is in `docs/factory/concepts/review-cadence.md`: six artifact classes (intent; samples of results with seeded defects; the factory's own rules; indicators; high-risk classes; the owner's own understanding) and three cadence mechanisms (event-based decision points, scheduled reviews, sampling with switching by track record). Sources read: DORA on change approval, acceptance sampling standards (MIL-STD-105 / ANSI/ASQ Z1.4), Warp's three review points, Kohavi on ideas versus reality, the owner's dialog (`keeping-control`, `mutation-testing-as-proof`, `how-author-defines-human-adds-nothing`, `restore-understanding`, `cannot-fit-claim-too-strong`, `code-review-unavoidable`).

Depends on: the owner's answers to the five questions at the end of `review-cadence.md`.
Environment: any (documents).

## Acceptance criteria
- The owner's answers to the five questions are recorded verbatim in `docs/factory/sources/owner-session-questions.md`.
- A table fixes, per artifact class, the reviewer, the cadence and the sampling rule with its parameters (initial share, how many clean samples lower it, what raises it), each with a stated reason and source.
- The switching rule for the review share is written with a worked example (zero findings in n samples gives an upper bound, rule of three) and the limits of its assumptions (independence, n about 30 or more).
- The design says how the human's review time is measured (owner note `attention-metrics`) and when the design is re-evaluated.
- The result is added to the guide (task 292) as a chapter.

## Analysis

## Plan

## Report

## Review

## Comments

### 2026-11-17, planner-session
Created from `review-design`. Waiting for the owner's answers (`ask-dont-assume`).

### 2026-11-17, planner-session
Owner's input (recorded in `docs/factory/decisions.md`): he reviews several hours a day in his own work; calendar regularity does not matter for the factory, what matters is what is valid for it; he has no factory to take data from; he does not need to give a threshold ("upper bound of an accepted defect" removed); the aim is zero defects, unreachable, so look for how to approach it; the factory's own rules are **always** reviewed by a human together with an agent, possibly several humans. The concept file `concepts/review-cadence.md` is deferred (the owner finds files in this interface inconvenient). Remaining question for the owner: what his review hours are spent on.
