# Software factory concept: working folder

**Goal (owner, 2026-11-17):** an **instruction (guide) on how to build a universal software factory**. The definition, the sources, the catalog and the concepts are means to that goal, not the goal. The guide must treat people and agents without a difference in kind and must state clearly why (see `definition.md`, "executor profile"; task 292).

We are building a **universal** software-factory system, not a patch for one repository. This repository's harness (planner and executor roles, task files, `scripts/`) is one implementation and the first test bed.

## Principles (owner, 2026-11-17)

- **`document-everything`. Document everything, filter nothing.** Every mechanism, idea, risk or fact found in a source is recorded even when it does not apply to the current harness as it is. Applicability is a column (`exists / candidate / partial / n/a here / unknown`), never a reason to drop an item. "Not applicable here" is information for other implementations.
- **`sources-verbatim`. Sources are verbatim and immutable**; interpretation lives in notes; every claim cites its source number.
- **`verification-labels-and-evidence`. Verification labels and evidence grading.** Claims from the web are marked by how they were checked; empirical studies additionally carry sample, population, venue and a weight grade in `evidence-register.md` (weak data must not carry a decision alone). Labels:  Claims taken from the web are marked by how they were checked (read on the page / search snippet only / corrected / synthesis without a source), see `docs/factory/qa/owner-dialog-walkthrough.md`.
- **`ask-dont-assume`. Do not assume, ask (owner, 2026-11-17).** Ask clarifying questions instead of assuming; the earlier the better; "not counting the current flow" (exact meaning to be confirmed: `open-threads.md` `ask-rule-exception`). Open questions are recorded in `open-threads.md`.
- **`mnemonic-names`. Use mnemonic names, never numbered codes (owner, 2026-11-17).** Questions, claims, mechanisms, threads and sources are referred to by short meaningful names (for example `outbox`, `mission-command`, `warp-talk`), in documents and in chat. Harness task numbers are the only numbers kept, because the task format requires them.
- **`living-example`. Use a living example (owner, 2026-11-17).** A definition alone says little. Every concept is shown on a running example; the example is this repository (ImageGen), because the owner knows what he wants from it and has hardly seen its code since the first pull request. See `case-study-imagegen.md`.
- **`primary-source`. The primary source is the owner's dialog (`owner-dialog`)**; other sources are supporting, and the owner's later statements win over any source.
- **`method-of-work`. Method of work (owner, 2026-11-17).** New questions are recorded verbatim (`sources/owner-session-questions.md`), answered with data from the dialog and new research, **cross-linked** to other questions, the catalog and tasks, and everything stays in the repository.

## Map

| Path | Content |
|---|---|
| `sources/` | the verbatim sources (`tsf-lesson`, `warp-talk`, `owner-dialog`, `always-on-agents`, the copied repository `cloud-factory-demo`, and the owner's notes `warp-talk-owner-notes`), the owner's messages (`owner-dialog-questions` from the web chat, `owner-session-questions` from the working session) and the index `sources/README.md` |
| `notes/` | interpretation per source (claims tables, open questions) |
| `qa/` | answers to open questions (`tsf-lesson`), the walkthrough of all 43 owner questions (`owner-dialog`) and answers to the working-session questions (`owner-session-answers.md`) |
| `research/` | research: public factory kits (README level and code reading) |
| `definition.md` | the **living definition** of a software factory (versions, parts, open points) |
| `concepts/` | explainers: `goodhart-campbell.md`, `knight-leveson.md`, `error-diversity.md`, `review-cadence.md` |
| `evidence-register.md` | weight grades of the empirical studies used |
| `open-threads.md` | questions and topics kept open |
| `human-org-parallels.md` | the same problems in organizations of only humans, with solutions and what differs for agents |
| `catalog.md` | **universal catalog of mechanisms** from all sources with an applicability column |
| `owner-notes.md` | things the owner asked to remember |
| `../../tasks/factory/` | tasks of the concept work in the harness task format |
