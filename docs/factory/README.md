# Software factory concept: working folder

**Goal (owner, 2026-11-17):** an **instruction (guide) on how to build a universal software factory**. The definition, the sources, the catalog and the concepts are means to that goal, not the goal. The guide must treat people and agents without a difference in kind and must state clearly why (see `definition.md`, "executor profile"; task 292).

We are building a **universal** software-factory system, not a patch for one repository. This repository's harness (planner and executor roles, task files, `scripts/`) is one implementation and the first test bed.

## Principles (owner, 2026-11-17)

- **P1. Document everything, filter nothing.** Every mechanism, idea, risk or fact found in a source is recorded even when it does not apply to the current harness as it is. Applicability is a column (`exists / candidate / partial / n/a here / unknown`), never a reason to drop an item. "Not applicable here" is information for other implementations.
- **P2. Sources are verbatim and immutable**; interpretation lives in notes; every claim cites its source number.
- **P3. Verification labels and evidence grading.** Claims from the web are marked by how they were checked; empirical studies additionally carry sample, population, venue and a weight grade in `evidence-register.md` (weak data must not carry a decision alone). Labels:  Claims taken from the web are marked by how they were checked (read on the page / search snippet only / corrected / synthesis without a source), see `docs/factory/qa/03-owner-questions-walkthrough.md`.
- **P6. Do not assume, ask (owner, 2026-11-17).** Ask clarifying questions instead of assuming; the earlier the better; "not counting the current flow" (exact meaning to be confirmed: `open-threads.md` OT07). Open questions are recorded in `open-threads.md`.
- **P4. The primary source is the owner's dialog (source 03)**; other sources are supporting, and the owner's later statements win over any source.
- **P5. Method of work (owner, 2026-11-17).** New questions are recorded verbatim (`sources/05-...`), answered with data from the dialog and new research, **cross-linked** to other questions, the catalog and tasks, and everything stays in the repository.

## Map

| Path | Content |
|---|---|
| `sources/` | verbatim sources 01-04, 03a (owner's messages in the web chat), 05 (owner's questions in the working session) and the index `sources/README.md` |
| `notes/` | interpretation per source (claims tables, open questions) |
| `qa/` | answers to open questions (source 01), the walkthrough of all 43 owner questions (source 03) and answers to the working-session questions W01.. (`05-workshop-answers.md`) |
| `research/` | research: public factory kits (README level and code reading) |
| `definition.md` | the **living definition** of a software factory (versions, parts, open points) |
| `concepts/` | explainers: `goodhart-campbell.md`, `knight-leveson.md`, `error-diversity.md`, `review-cadence.md` |
| `evidence-register.md` | weight grades of the empirical studies used |
| `open-threads.md` | questions and topics kept open |
| `human-org-parallels.md` | the same problems in organizations of only humans, with solutions and what differs for agents |
| `catalog.md` | **universal catalog of mechanisms** from all sources with an applicability column |
| `owner-notes.md` | things the owner asked to remember |
| `../../tasks/factory/` | tasks of the concept work in the harness task format |
