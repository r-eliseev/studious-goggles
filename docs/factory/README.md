# Software factory concept: working folder

We are building a **universal** software-factory system, not a patch for one repository. This repository's harness (planner and executor roles, task files, `scripts/`) is one implementation and the first test bed.

## Principles (owner, 2026-11-17)

- **P1. Document everything, filter nothing.** Every mechanism, idea, risk or fact found in a source is recorded even when it does not apply to the current harness as it is. Applicability is a column (`exists / candidate / partial / n/a here / unknown`), never a reason to drop an item. "Not applicable here" is information for other implementations.
- **P2. Sources are verbatim and immutable**; interpretation lives in notes; every claim cites its source number.
- **P3. Verification labels.** Claims taken from the web are marked by how they were checked (read on the page / search snippet only / corrected / synthesis without a source), see `docs/factory/qa/03-owner-questions-walkthrough.md`.
- **P4. The primary source is the owner's dialog (source 03)**; other sources are supporting, and the owner's later statements win over any source.

## Map

| Path | Content |
|---|---|
| `sources/` | verbatim sources 01-04, 03a (owner's messages) and the index `sources/README.md` |
| `notes/` | interpretation per source (claims tables, open questions) |
| `qa/` | answers to open questions (source 01) and the walkthrough of all 43 owner questions (source 03) |
| `research/` | research: public factory kits (README level and code reading) |
| `catalog.md` | **universal catalog of mechanisms** from all sources with an applicability column |
| `owner-notes.md` | things the owner asked to remember |
| `../../tasks/factory/` | tasks of the concept work in the harness task format |
