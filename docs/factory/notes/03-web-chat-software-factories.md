# Notes on source 03 (PRIMARY): the owner's web chat

Source: `docs/factory/sources/03-web-chat-software-factories.md`. Line numbers "L..." refer to that file. Language: Russian (continuation of the dialog).
Rule: the owner's own statements are the signal; claims in Claude's answers are secondary (web blogs and vendor texts, several flagged as unverified inside the chat itself).

## 1. Thesis of the dialog

The value of a factory is not the agents but **stating intent precisely and verifying the result reliably** (L81, L340). Autonomy level is a property of a **task class, service or repository, not of a person or a model** (L246, L571, L1406, L1516). The owner is at Shapiro level 3 and wants to (a) move on to level 4 and (b) move other people there, **without losing control and understanding** (L560, L1253).

Owner's context (L1393, L1477): a team; 10+ microservices, multiple languages (TypeScript CDK and frontend, Java, Go, C, Python, Rust), AWS serverless (Lambda, Fargate, Step Functions, API Gateway), separate pipelines, some event-driven parts; one package has 3M+ lines, 100k+ is common.

## 2. Aggregated threads (question or objection -> where it landed -> status)

Status: **agreed** = owner accepted or built on it; **contested** = owner objected, not resolved; **open** = raised, no conclusion; **asked to remember** = the owner said "запомни".

| # | Thread | Owner's move | Where it landed | Status |
|---|---|---|---|---|
| T1 | Old (Microsoft DSL/templates) vs new (AI) software factory | "everything except verification and risk is the same thing in different words" (L391); verification in the old one is not "correct by construction" (L405) | one axis: how rigidly the input->output mapping is specified. Same root risk (intent vs result gap); difference is **what happens to what you did not specify**: old = template default or inexpressible, visible, loud failure; new = model's guess, invisible, quiet failure (L443-451) | the quiet/loud-failure idea **asked to remember** (L1121); the rest "I don't like some points, will sort out later" (L469) -> **contested/open** |
| T2 | Digital twins vs mocks | "sounds like a mock of a web service"; mocks have state, written by agents too, capture arguments and order (L495); "literally a mock" (L529) | the term is partly marketing; a twin is a fake of a whole external service run as a real server. Real novelty is economic (agents make full fakes cheap) and purpose (environment for the agent's scenario loop) (L500-552); Fowler's taxonomy dummy/stub/spy/mock/fake (L1151-1181) | **agreed** (owner: "I like this", L1145) |
| T3 | Twin cost | "a twin that catches 80%+ of bugs is ~95% of the implementation" (L1654) | cost is nonlinear, bugs sit in the tail; but a twin needs only the behavior surface your code depends on. For rich AWS semantics (IAM, Step Functions, API Gateway) use real AWS in a sandbox account, not a twin; twins for third-party/rate-limited/dangerous APIs, always with conformance tests (L1662-1683) | open (no data on catch rate, said in L1663) |
| T4 | Shapiro autonomy levels | "this thought I like. I am at it. need to understand how to move on and move others" (L560) | transition 3->4 gated by verification, specs, written context, psychology; assign levels to tasks not people; "checks first, then lift review" (L564-611, L623-690) | **agreed**; no verified method exists (stated L565) |
| T5 | Holdout scenarios | "'cannot be fitted' is a strong claim"; scenarios come from the same business requirements as the implementation, can have bugs; a real judge is needed (L1820) | concession: only "harder to fit", feedback channel leaks (adaptive overfitting); same-source error (oracle problem); **human arbiter** needed where requirements are ambiguous; cheap oracle for "preserve behavior" tasks (migration, refactor, port), invariants, contracts (L1826-1868) | **agreed**; links to T6 |
| T6 | "Human adds nothing" criterion (4->5) | "how does the author define that the human adds nothing?" (L1469) | the source has no operational definition; proposal: sample reviews of changes that passed all checks, rule of three (0 findings in n reviews -> <= ~3/n at 95%), seed known bugs to check the reviewer (L1486-1498) | open; dark factory only where oracle is cheap |
| T7 | Scale | "that sounds like a small system" (3M lines vs ~32k) (L1477) | spec describes the change, not the system; selective context; local verification; mutation testing only on changed code; autonomy per area; StrongDM proves the principle, not your scale (L1502-1518) | **agreed**; to be tested by the owner on narrow areas |
| T8 | Graded satisfaction metric | "sounds like populism" (L1526) | owner was right: the 95%/5% phrase was an interpreter's addition and logically wrong (mean hides tails). Hard invariants binary and blocking, soft qualities graded; per-category minimums; calibrate the LLM judge against human labels; confidence intervals (L1530-1581) | **agreed** (Claude retracted) |
| T9 | Independent checking | "what other options?" (L1585) | the check source must differ from the code source: different models/roles/contexts; different truths (record/replay, historical bugs, reference implementation, external contracts); different methods (property-based, fuzzing, mutation, static, N-version); production checks; org separation (L1594-1650) | open (a menu, no choice made) |
| T10 | Tacit knowledge ("rubbish knowledge") | "how to extract it?" (L741); "people have this too, how to solve?" (L788); "how do humans do it?" (L833) | channels: mine history, interviewer agent, agent mistakes (every human correction becomes a rule), characterization tests, "what must never be touched" questions; write into the repo, short, with reason/date/owner, prefer turning rules into checks; humans resolve conflicts via trust hierarchy, freshness, running the thing, asking (L746-878) | "people write docs badly but readily answer questions" **asked to remember** (L1872) |
| T11 | Splitting agents | owner's idea: separate agents, each gets results of only its own errors; integration by a separate agent in a separate run (L886) | reasonable, = modular architecture; needs contracts first, contract tests at every seam, the integration agent routes mismatches back to owners instead of fixing them, a cross-cutting layer to catch local optimum (L890-927) | **agreed** (owner's design) |
| T12 | Context management | "can't agents manage context?" (L278); "people have the same, we know how to solve it" (L935); "how?" (L965) | agents search/compress; source quality stays human; human-vs-agent technique table; append-only decision log, raw output to files (L282-1016) | **agreed**; "split into narrow agents already covers most" (L961) |
| T13 | Specs: completeness and hidden tests | "how to write complete specs?" and "sounds like an olympiad judge: you see how many tests failed, not inputs" (L1024) | structured spec; ask agent to find holes; list failure paths; iterate; two test sets (visible for debugging, hidden for acceptance), graded/categorical feedback, rotation, attempt limits; guardrails: least privilege, sandbox, human confirmation for dangerous actions, enforce by the system not by prompt (L1037-1089) | open |
| T14 | Control and understanding | "I do all of this, but keeping control is the most important thing for me, psychologically and practically; if I'm asked how it works and why I must know" (L1253) | legitimate. Control has four layers: intent, boundaries, verification, **understanding**; level 5 gives up all four, the owner's goal is 4 with understanding kept; keep the core at level 3, the periphery at 4; "understanding debt" must be ordered and measured: predict before reading, agent quizzes you, architecture diff, bounded change size, manual incident analysis, explain to others (L1259-1297, L1695-1716) | **agreed** (key constraint of the owner) |
| T15 | Proving checks work | "that is mutation testing; still needs proof, unclear how it was verified" (L1307); "no guarantee the experience transfers" (L1350) | one injected bug is a demo, not proof; use mutation tools, replay historical real bugs, independent mutation generation, numbers per area, real escaped defects as the final counter; pilots do not transfer automatically: state conclusions narrowly, control group, new pilot per task class (L1311-1385) | **agreed** |
| T16 | Post-mortems of failures | "this is an obituary or a post-mortem; need more like this" (L1724) | studies on legacy migration, PocketOS prod DB deletion (broad token, backups on same volume, rules in a prompt not enforced), Grigoriev Terraform/AWS deletion, monday.com (weekly on-call for verification ownership), 6sense; recurring causes (L1728-1777) | open; Claude flagged the Django story as an anecdote and most items as read only in part |
| T17 | Rule -> check | "turn 'this variable's type must not be changed to an unsigned type not multiple of 16 bits' into a lint rule" (L1893); "so code review cannot be avoided" (L1904) | C `_Static_assert`, Rust trait bound, Go generic constraint (checked by compiling, L1921-1948); keep fixtures: one compliant, one violating, to test the rule itself; review changes its object (intent, not diff lines) but stays (L1911-1970) | **agreed** |
| T18 | Making ignorance cheap | "sounds too good; how to reach it fully, without general words about blast radius" (L1978) | cost = frequency x time-to-detect x time-to-recover x damage; mechanisms per factor (business-invariant alarms, synthetics, canary + auto-rollback, restorable data, state protection, deferred external effects); never zero (L1982-2075) | **agreed** as a list; owner said "yes" to going through one real flow (L2079) |
| T19 | Pre-mortem; show assumptions | "interesting, remember" (L2087, L2103) | before starting, ask "imagine it failed, why?"; before acting, the agent writes assumptions, unknowns, what it may have missed (L805, L815) | **asked to remember** (both) |

## 3. What the owner asked to remember (with the dialog's own memory entries)

1. Quiet vs loud failure: old factory fails loudly on what it cannot express; new one fails quietly on what was not specified (L1121).
2. The whole old/new comparison (L455), with reservations: some points the owner does not like, to be revisited.
3. People write docs badly but readily answer questions -> interviewer agent (L1872).
4. Pre-mortem (L2087).
5. Force the agent to show assumptions (L2103).

## 4. Where the dialog stopped

Last owner message: "also an interesting thought, remember" (assumptions, L2103). Last Claude message (L2099): asks the owner to **pick one real flow** (example: API Gateway -> Lambda -> Step Functions; best one where an error would cost most) and describe what it does, what data it reads and writes, which external calls it makes (mail, payments, third-party APIs), to walk it through the L2057-2075 list: what exists, what is missing, what to add first. **No answer yet** (this is the continuation point).

Unanswered or only partly answered inside the dialog: the choice of a first task class and first service ("which service is best covered by checks" L1461); who and how often asks the owner questions (L1299); the owner's reservations on T1; whether the "rule of three" sampling (T6) is adopted.

## 5. Relation to sources 01 and 02 (secondary)

- 02 (Warp) and 03 agree that review should move from lines to risk-based human entry points; 03 adds the missing piece: **how to earn the right to lift review** (checks first, independent oracle, arbiter).
- 01 (TSF) talks about "near the loop" as a mode of the human; 03 says the mode is a property of task class and repository, which is the per-action idea from `docs/factory/owner-notes.md` (N2, N3).
- Neither 01 nor 02 addresses what 03 treats as central: independent verification, the oracle problem and understanding debt. The concept should start from 03 and use 01/02 for shape and tooling.

## 5b. Source 04 (always-on agents) in relation to this dialog

Source 04 supplies a runtime skeleton (inbox, timers, memory, permissions, outbox); its permissions part answers several causes found in the dialog's incident thread (credentials within the agent's reach, rules in prompts instead of the system), and its deferred-tool-call pattern is the asynchronous "decision point" the owner wants (near the loop). It says nothing about independent verification (the dialog's core). See `docs/factory/notes/04-always-on-agents.md` and task 291.

## 5a. Walkthrough of every owner question

All 43 questions with their quoted context, a direct answer and a verified, aggregated answer (13 threads, with corrections to the original dialog) are in `docs/factory/qa/03-owner-questions-walkthrough.md`; the verbatim question list is `docs/factory/sources/03a-owner-questions.md`.

## 6. Questions for the next step

1. Which real flow do we walk through (the pending request, L2099)?
2. Is our concept for the owner's team (microservices, AWS) or for this repository's harness, or both? (T14/T18 are written for the first; the repository is the second.)
3. The reservations on T1: which points did you dislike?
