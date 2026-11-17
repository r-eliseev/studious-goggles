---
id: 296
status: todo
assignee:
claimed:
created: 2026-11-17
---

# POC: formalize the current ImageGen code as a DSL, UML and other models

## Description
Owner's idea (2026-11-17), after the MDA and UML explanation in `docs/factory/qa/owner-session-answers.md` (`vs-classical-delegation` follow-up) and `docs/factory/research/classic-factory-adoption.md`: "try to formalize the current code as a DSL plus UML plus whatever else". The owner considers MDA a weak alternative ("roughly just generating DAOs from XML", `owner-view-mda`) and finds this experiment interesting in itself. Question to answer by experiment: how much of the current ImageGen code can be captured by formal models (for example a model of the core entities and contracts, the provider interface, the session and generation state machines, the scenarios already written in Gherkin) so that parts can be generated back or checked against the models, and where information is lost.

This is a proof-of-concept task. Per `docs/tasks.md`, proof-of-concept code goes to separate files in `poc/factory/296-poc-formalize-imagegen-as-models/` (rules in `poc/README.md`), not into `ImageGen/`; the task holds only the link and the conclusion. The Plan holds only concepts.

Depends on: the owner's approval of a plan (mode `ask-dont-assume`: the plan names which parts are formalized first, with the owner).
Environment: Linux (documents and small proof-of-concept tools).

## Acceptance criteria
- A short list of candidate formalizations with the model kind for each (for example a domain-specific language for provider contracts, UML class and state diagrams for the core, Gherkin already present for behavior), chosen with the owner before any work.
- For at least two of them a working proof-of-concept in `poc/factory/296-poc-formalize-imagegen-as-models/`: the model, a way to check the code against it or generate code or documentation from it.
- A report of what the models capture, what they lose (information not expressible), the cost of keeping them in sync with the code, and a comparison with the classic approaches (domain-specific languages with generators, model-driven architecture, product lines) using the evidence in `docs/factory/research/classic-factory-adoption.md`.
- A statement of whether an agent can keep such models current, and what independent checks would catch drift (`independent-check-source`).
- No change to product code in `ImageGen/`.

## Analysis

## Plan

## Report

## Review

## Comments

### 2026-11-17, planner-session
Created from the owner's reaction to the MDA explanation. Not started; the plan goes to the owner first.
