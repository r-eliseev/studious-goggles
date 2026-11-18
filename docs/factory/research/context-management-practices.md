# Context management for agents: practices, large outputs in files, what to rely on

Question from the owner (2026-11-18), on the quotes of `agents-manage-context`: are there best practices, or is it better to rely on the agent's built-in functionality? And: agents often write big answers into files; how to deal with that? Sources: `sources/context-management/` (labels: **primary** = the vendor's own engineering text or docs; **synthesis** = my inference).

## What the primary sources say
1. **Compaction** (Anthropic engineering, "Effective context engineering"): summarize the history near the limit and continue; in Claude Code the summary keeps architectural decisions, unresolved bugs and implementation details and drops redundant tool output, plus the five most recently accessed files. Warning in the same text: "overly aggressive compaction can result in the loss of subtle but critical context whose importance only becomes apparent later." Lightest form: clearing old tool results.
2. **Structured note-taking**: the agent writes notes (a to-do list, a `NOTES.md`) outside the context and reads them back later; recommended for iterative work with clear milestones.
3. **Sub-agents**: clean window, returns a "condensed, distilled summary (often 1,000-2,000 tokens)"; best for research and analysis with parallel exploration.
4. **Just-in-time context**: keep light identifiers (file paths, queries, links) and load data with tools at runtime, instead of loading everything up front.
5. **What survives compaction** (Claude Code docs, memory page): the project-root `CLAUDE.md` is re-read from disk and re-injected after `/compact`; nested `CLAUDE.md` and path-scoped rules reload only when needed; an instruction given **only in conversation** can be lost. The docs' fix: put it into `CLAUDE.md`. Also in best-practices: `/compact <focus>` steers what is kept; `/clear` starts clean; and a sentence in the conversation "can get lost when the conversation compacts, so anything that matters belongs in a deny rule" (the same point as `rules-advisory-vs-enforced`).

## Best practices or the built-in functionality?
Both, in layers (synthesis from the sources):
- **Rely on the built-in** for the mechanics: automatic compaction, tool-result clearing, sub-agent isolation. Compaction quality is tuned by the vendor on real traces; reimplementing it is rarely worth it.
- **Do not rely on the built-in for what must not be lost.** Anything that must survive goes to files that are re-read from disk: project rules (`AGENTS.md`/`CLAUDE.md`), decisions (ADRs), task state, the module map. The conversation is a cache, the repository is the memory.
- **Make the repository the shared memory between agents**: decisions written to the common place, contracts before parallel work, per-task reports. This is exactly the "write decisions to a shared place" condition of the original quote.
- **Cut task size** so one task fits one window; a fresh session per task with the state in files beats one long session with several compactions (synthesis, not measured here).
Not found in these sources: measurements of how much is lost per compaction; no evidence grade can be given beyond the vendor's own warning.

## Agents write big answers into files: is it a problem, and what to do
It is partly **intended design**: the multi-agent research article recommends that sub-agents "store their work in external systems, then pass lightweight references back to the coordinator" to avoid the "game of telephone" (information loss when everything goes through the lead agent's summary) and token overhead. So files are the correct channel for large results.
What goes wrong (synthesis): (a) the result exists only as a file nobody reads; (b) the orchestrator loads the whole file and the context fills anyway; (c) the file is not tied to the task and gets lost or stale; (d) the summary in chat contradicts the file.
Countermeasures:
1. **Fixed output contract**: the agent must return a short structured result (status, one-paragraph summary, file path, list of decisions and open questions) and put the bulk in a file at a **fixed place per task** (for example under the task's folder). This is the `harness-as-function` thread.
2. **Reader reads selectively**: the orchestrator or next agent reads the summary and the path, then reads parts of the file (search, line ranges), not the whole file.
3. **Size limit and format check by the harness**: the result is validated; no summary or no path means rejection (an enforced rule, not advice).
4. **The file is an artifact of the task**: committed or registered, so it can be found, reviewed and expired; temporary drafts go to a temp folder, not into the repository (our repository already requires this: drafts only in `tmp/`).
5. **Review of the file by someone other than its author** when the result matters (independent verification).
