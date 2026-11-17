# Owner notes: things to remember

## Principle `document-everything` (owner, 2026-11-17)

**Goal (2026-11-17):** the aim is an instruction on how to build a universal software factory, not the definition. **`ask-dont-assume`:** do not assume, ask questions, the earlier the better (not counting the current flow; meaning to be confirmed, `open-threads.md` `ask-rule-exception`).

The system being built is **universal**. Everything found is documented even if it does not apply to the current harness as it is; applicability is a column in `docs/factory/catalog.md`, not a filter. See `docs/factory/README.md`.

Short items the owner asked to keep. Each cites where it came from. Not sources, not decisions: candidates for the concept.

| # | Item | Status | Origin |
|---|---|---|---|
| `attention-metrics` | Metrics for the "near the loop" shift: owner minutes per merged task, number of returns, approval waiting time. | remember | QA on `tsf-lesson` (B/`meta-optimization-how`, [mine]); close to the pilot metrics in `owner-dialog` ("time from spec to merge, errors after release, review minutes") |
| `mode-per-action` | Set the human's mode per **action type**, not per factory (approve a spec, merge, release, ...). | remember; explained to the owner, see below | QA on `tsf-lesson` (B/`loop-modes`, [mine]); resembles the per-action LoopRails ladder [ext] and `owner-dialog`'s remark that autonomy level depends on the repository, not on the developer or the model |
| `checks-before-autonomy` | Autonomy rises only as far as automatic checks can be trusted ("checks first, then lift review"). | remember | `owner-dialog` (verification gates the levels) |

`mode-per-action` in one example (corrected 2026-11-17 by the owner: a task passes the **planner agent**, not the human): today every task passes the planner agent twice (it writes the plan, it reviews the result); the owner's part is plan approval, which under the standing instruction of 2026-11-07 (`docs/status.md`, ADR 0033) is already given in advance. So per task the factory is closer to "near the loop" than I first wrote; what stays with the human is the standing instructions themselves, the product goals and the cases the planner escalates. The per-action question becomes: which **actions of the planner agent** need the human (e.g. approving a plan that changes the data schema or key handling, accepting a review of a high-risk change) and which do not. Same factory, different modes per action, raised or lowered as trust in the action's checks grows.

## From the dialog (`owner-dialog`): everything the owner asked to remember

The owner wrote "запомни" / "запомним" five times. The web chat saved them into Claude's own memory ("Updated memory"), which this repository cannot read, so they are recorded here. Quotes are the passages the owner marked (verbatim, Russian); "L" = line in `docs/factory/sources/owner-dialog.md`.

| # | Owner's words | What to remember (the marked passage) | Notes |
|---|---|---|---|
| `remember-old-new-comparison` | "вавй это запомним в целом. оно в каких-то моментах мне не нравится, но потом разберемся" (L469) | "В старой версии проблема была в выразительности. DSL слишком узкий, и реальные задачи в него не помещались. Зато результат был предсказуем. В новой версии наоборот: агент может сделать что угодно, но результат непредсказуем. Поэтому вся сложность уходит в проверку (holdout-сценарии и прочее). То есть старая фабрика заменяла неопределённость жёсткими рамками, а новая принимает её и ловит ошибки проверками." Plus the hybrid idea: deterministic generation for the routine part, agents for the rest, which reduces what must be verified (L457-467). | **Reservations:** the owner dislikes some points; not yet specified. The dialog refined it: the real difference is what happens to the unspecified (L443-451). Revisit (question 3 in `notes/03`). |
| `remember-quiet-vs-loud-failure` | "вот это очень хорошая мысль, запомни ее" (L1130) | "Риски: корень общий: расхождение между намерением и результатом. Старая: упираетесь в стену. Выразить нельзя, значит это видно сразу, и вы пишете вручную. Отказ громкий. Новая: почти всё можно «выразить», но результат может быть тихо неверным. Отказ тихий." (L1123-1128) | Basis for "checks first": a quiet failure is caught only by verification. |
| `remember-people-answer-questions` | "так, тоже хорошая мысль, запомни" (L1876) | "Люди плохо пишут документацию, но охотно отвечают на вопросы." (L752) The channel: an interviewer agent reads the code, finds odd places, asks the owner concrete questions, answers are written as short notes; 20-30 minute sessions (L752). | Extracts tacit knowledge into the repository. |
| `remember-pre-mortem` | "это интересная мысль, запомни" (L2088) | "Pre-mortem. Перед стартом спросить: «представь, что это провалилось, почему?» Работает на людях, работает и на агентах: можно попросить сначала перечислить, что может пойти не так." (L805) | Candidate step before approving a plan. |
| `remember-assumptions-list` | "тоже интересная мысль, запомни" (L2107) | "Заставлять показывать допущения. Перед действием агент пишет: что я предполагаю, чего не знаю, что мог упустить. Это не гарантия, но делает пробелы видимыми." (L815) | Candidate mandatory section in a plan or a report. |

`remember-quiet-vs-loud-failure`-`remember-assumptions-list` are accepted ideas; `remember-old-new-comparison` is stored with reservations.


## More rules (owner, 2026-11-17)

- **`mnemonic-names`:** refer to things by names, not numbered codes.
- **`living-example`:** show every concept on this repository (ImageGen) as the running example; see `case-study-imagegen.md`.
- **`ask-dont-assume`, exception confirmed:** ask before starting; do not interrupt a flow that is already running with questions (the owner answered "yes" to this reading).
