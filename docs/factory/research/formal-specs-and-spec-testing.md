# Formal specs, testing a spec with a small model, pseudocode, deterministic path, Gherkin to code

Questions from the owner (2026-11-18), attached to the previous spec-completeness answer. Labels: **read** = abstract or text read by me (full papers not read); **search** = only a search summary seen; **synthesis** = my inference.

## 1. Formal specifications (first pass of `formal-proof-architecture-and-code`)
- What it is: a proof that code satisfies a formal specification, checked by a small trusted proof checker.
- Historic cost (Kleppmann, 2025-12-08, read): seL4 as of 2009 was 8,700 lines of C and needed 20 person-years and 200,000 lines of Isabelle, "23 lines of proof and half a person-day for every single line of implementation"; "for most systems, the expected cost of bugs is lower than the expected cost of using the proof techniques that would eliminate those bugs".
- What LLMs change: the vericoding benchmark (arXiv 2509.22908, Sep 2025, abstract read): 12,504 formal specifications; LLMs produce verified code from formal specs with success 82% in Dafny, 44% in Verus/Rust, 27% in Lean; pure Dafny verification went from 68% to 96% in one year; natural-language descriptions added to the spec did not significantly help. I did not check how large the benchmark problems are (likely single programs, not services).
- Why LLM proofs are safe: the proof checker rejects an invalid proof, so a hallucinated proof does not get through (Kleppmann, a prediction and argument, not a measurement).
- What stays unsolved: "how do you know that the properties that were proved are actually the properties that you cared about?" (Kleppmann). This is validation versus verification (Boehm): a proof shows the code matches the spec, not that the spec is what the owner wants.
- Does it make sense (synthesis): worth it for small, isolated, high-value parts with a precise rule (state machines, money and budget invariants, parsers, protocol rules); not for UI and platform behaviour, where no formal model of the platform exists (the touch and click regressions of ImageGen). It does not replace independent acceptance tests; it is one more independent check if the formal spec is written independently from the code. Not researched: model checking of architecture (for example TLA+) and industrial case studies (for example at AWS): next pass.
- ImageGen candidates (my suggestion, not checked against the code): the job status model (queued, running, done, failed, cancelled, filtered: terminal states never change), "a result is saved on receipt", budget limits in `BudgetGuard`, price calculation.

## 2. Testing a spec with a small local model before the big model runs
- Established technique (read): ClarifyGPT (arXiv 2310.10996, ESEC/FSE 2024) detects an ambiguous requirement by a code consistency check: sample several solutions, run them on generated inputs, and compare outputs; low agreement means ambiguity, then it asks clarifying questions; GPT-4 Pass@1 rose from 70.96% to 80.80% on MBPP-sanitized (with human users). Cost noted by critics: high overhead (search).
- Model size and instruction following: IFScale (arXiv 2507.11538, read): 20 models, 500 instructions: even the best reach 68% at the maximum density; "model size and reasoning capability correlate with 3 distinct performance degradation patterns". I found no data for a typical spec-sized task by model size.
- A small model as a pre-reader (search, not read): DisambiguSLM (arXiv 2604.23263, Apr 2026) uses a small model to find and reconcile semantic risk points in a prompt before a large model answers.
- The confound (synthesis): a small model fails either because the spec is ambiguous or because it is weak. To separate: run the small model several times and look at **disagreement between runs** (ambiguity shows as disagreement), and check failures on a larger model on a sample; a failure in both points at the spec.
- Practical protocol (synthesis, untested): (1) cheap structural check of the spec; (2) a small local model, several runs, asked for a plan or tests and the **list of its assumptions** (not the full implementation); compare the runs; (3) fix the spec; (4) only then the large model. Risk: models of the same family make correlated errors (`concepts/error-diversity.md`): use different families. Needs an experiment on ImageGen: take past defects caused by an unclear spec and check whether the probe would have flagged them. Open thread `small-model-spec-probe`.

## 3. Can pseudocode solve the spec problem?
- Evidence (read): Mishra et al., EMNLP 2023 (arXiv 2305.11790): pseudo-code prompts instead of natural language on 132 tasks, BLOOM and CodeGen: +7 to 16 absolute F1 on classification, +12 to 38% relative ROUGE-L; code comments, docstrings and structure each contribute. Limits: 2023 models, NLP tasks, not application code.
- Analysis (synthesis): pseudocode removes ambiguity of order, conditions and data flow, but not domain ambiguity (what "valid" or "expensive" means); it is one rung on the scale: natural language, structured natural language (EARS), examples (Given/When/Then), **pseudocode and decision tables**, typed DSL, code. Good for the algorithmic core (price rules, retry, status transitions), poor for UI behaviour.
- ImageGen already uses it: `AGENTS.md` says the task Plan contains contracts, pseudocode, state machines and test cases in words, not code.

## 4. The owner's idea: deterministic generator for simple tasks, a full LLM for the rest
Recorded as `deterministic-path-for-simple-tasks`. Analysis (synthesis):
- It is the triage idea of the Warp factory (simple, unambiguous work goes straight through; hard work goes to specs) with a stricter criterion: **does a generator exist whose input is fully specified?**
- The **parser is the router**: if the input parses against the generator's grammar, run the generator; if it does not parse, hand the task to the LLM. A parse failure is a loud failure, so misrouting is caught.
- Gain: a generated output is verified by verifying the generator once, not each output.
- ImageGen already has it: other OpenAI-compatible providers are added through configuration (no code written by an agent), plus Reqnroll glue and SQL migrations as a fixed process (not checked which parts are generated).

## 5. Can Gherkin be turned into application code?
- Deterministically, in general: no. Scenarios give examples on a few inputs, steps are free text; there are no types, algorithms or architecture. Many programs pass finitely many examples. A generator is possible only if the **step vocabulary is fixed and has defined meaning** for a narrow domain (forms and CRUD, state-transition rules): then it is a domain DSL written in Gherkin syntax.
- With an LLM: possible (Kiro writes Given/When/Then acceptance criteria, Böckeler 2025), but then the scenarios are both spec and test, so independence is lost unless a hidden set is kept.
- Evidence: no empirical study of Gherkin to application code found (search found studies of the reverse direction, and one hardware case, a 16-bit ALU, arXiv 2512.17814, seen only in a search summary).
- ImageGen candidate: a table of provider status to the common status model as data, from which the mapping code is generated (my suggestion).
