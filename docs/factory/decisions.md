# Decisions of the owner

A log of decisions the owner made in the working sessions, so that none depends on chat memory. Each has the decision, the date, its status and whether it is to be revisited. Names, not numbers (`mnemonic-names`).

| Decision | Date | Status | Revisit |
|---|---|---|---|
| `ask-rule-exception`: ask questions early, before starting; a flow that is already running is not interrupted with questions | 2026-11-17 | decided (the owner answered "yes" to this reading) | no |
| `independence-wording`: independence of verification is **measured, not assumed, and at least one of four things differs** between producer and checker (the executor, the information, the method, the source of truth); a different method or source of truth is the default. Chosen from three options (different executor; different method or source of truth; measured independence). | 2026-11-17 | decided, **provisional** | **yes, when the owner understands better what is going on** (open thread `independence-wording-revisit`) |
| `feedback-layer-mandatory`: what happens after release must return to the input | 2026-11-17 | decided | no |
| `goal-and-no-difference`: the goal is an instruction on how to build a universal factory; the definition is only a means; people and agents must not differ in the definition and the reason must be stated clearly | 2026-11-17 | decided | no |
| `review-factory-rules`: reviews of the factory's own rules (checks, prompts, skills, thresholds, boundaries) always go through a human **together with an agent**, possibly several humans; never sampled | 2026-11-17 | decided | no |
| `review-regularity`: a calendar schedule is not important for the factory; what matters is what is valid for the factory (triggers by events and evidence). The owner reviews several hours a day in his own work; that is his capacity, not a factory requirement | 2026-11-17 | decided as a direction; details open | yes (open thread `review-design`) |
| `zero-defects-direction`: aim at zero defects, accept that it is unreachable, and look for how to approach it; no threshold number is needed from the owner | 2026-11-17 | decided as a direction | no |
| `name-of-the-thing`: keep the open question of the name | 2026-11-17 | left open | later |
| `concept-explainers-deferred`: the concept files (`concepts/goodhart-campbell.md`, `knight-leveson.md`, `error-diversity.md`, `review-cadence.md`) are postponed as open questions; the owner finds reading files in this interface inconvenient and will return to them later. Answers go in chat first; the repository is storage | 2026-11-17 | decided | yes, later |
| `mission-command-thread`: deferred, return later | 2026-11-17 | deferred | yes |
| `outbox-meaning`: an outbox carries **messages to a human** (outcome, question, approval request, summary); agent work on objects (changes in code, specs, pull requests) is not an outbox | 2026-11-17 | **accepted** by the owner ("let us fix it") | no |
| `mnemonic-names`: names instead of numbered codes everywhere | 2026-11-17 | decided; documents migrated | no |
| `living-example`: every concept is shown on this repository | 2026-11-17 | decided | no |
| `irreversible-actions-through-human`: irreversible external actions (emails to customers, payments, publications, releases) **must pass through a human**; a cancel window only complements this and never replaces it | 2026-11-17 | decided | no |
| `owner-review-profile`: in the owner's own work he reviews mostly diffs and specs, less plans, and reports are not an accepted practice there; this is his capacity and habit, not a requirement of the factory | 2026-11-17 | recorded | no |
| `imagegen-done-criterion`: ImageGen is "done" when it carries out the tasks set so far and has no obvious bugs that cannot be written off as features; he checks by occasionally installing the app and trying it, and mostly relies on tests | 2026-11-17 | recorded | yes (who decides bug versus feature: `bug-vs-feature-arbiter`) |
| `imagegen-code-review-intent`: the owner believes he will one day review all the code but keeps postponing; the only reason is a feeling that part of the bugs can be solved architecturally | 2026-11-17 | recorded | yes (`architecture-review-instead-of-code-review`) |
| `risk-classes`: damage is limited by classes of risk; a first list for ImageGen is in `case-study-imagegen.md` | 2026-11-17 | proposal, awaiting the owner's reaction (`risk-classes-confirm`) | yes |
| `architecture-review-first`: the first step is an architecture review prepared by an agent (map of modules, contracts and failure policy); the final goal is still to go down into the details (a full code review), time unknown | 2026-11-17 | decided (first level yes) | yes, when the owner decides to go down |
| `bug-vs-feature-arbiter`: **the human decides** whether something is a bug or a feature | 2026-11-17 | decided | no |
| `risk-classes-universal-only`: the list of risk classes is accepted ("let it be"), but **only general universal rules for any project are kept** as the rules; the ImageGen list stays as an example only | 2026-11-17 | decided (reading to be confirmed) | no |
| `hybrid-claim-retracted`: the claim "the repository is a hybrid of two eras" was too loud; the deterministic scripts here scaffold the process, they do not generate the product from a model; the one real classic element is Gherkin scenarios turned into test code by Reqnroll | 2026-11-17 | retracted | no |
