# Notes: the AI Engineer talk page and the warp.dev definition of a factory

Sources: `sources/ai-engineer-talk-page/` (page, essay, machine transcript, slide index) and `sources/warp-dev-site/` (homepage text). Both fetched 2026-11-18.

## 1. The AI Engineer page of the talk

- It is the same talk as `warp-talk` (video tUPPVhBBcoM). The title there is "Software Engineering Is Becoming Factory Engineering"; the first slide says "Self-Improving Software Factories: The New Open Source Model", AI Engineer World's Fair 2026.
- The page text is an **editorial essay by the site** plus a timed machine transcript. The essay adds nothing the speaker did not say; its value is the chapter structure (7 chapters, with times) and one honest caveat it states itself: the skill loop "is a proposed feedback mechanism; the talk does not establish a measured improvement".
- **Slides**: the slide index has only the first ~447 s. Slides seen as text: the thesis ("Software engineering will become factory engineering"), the evolution (chat, autocomplete, interactive agents (today), automated agents), "A cloud software factory" (triage, spec, implement, verify, ship, monitor; review points: spec, code, product), the contributions dashboard (issue states: triaging, ready to spec, creating spec, ready to implement, implementing), open-source maintenance pains (noisy issues, sloppy PRs, code review hell, verifying changes).
- **Answer to the owner's question about the notes**: the control-plane and data-plane lists are **not in the transcript**. At about 12:27 the speaker shows a dense slide and says "you're not supposed to read this. It's just a lot of stuff"; the lists are most likely on that slide, but its text is not in the slide index, so this is **a likely but unverified origin**. The efficiency formula is spoken as words only ("how much software did you ship, how much did it cost in terms of human time and token time").
- "Formal proof", "cr": not in the transcript or the slide index either way (still open).

## 2. The definition of a factory on warp.dev vs the repository

Warp's site (FAQ "what is a cloud software factory?"): *"a factory is a fleet of agents wired to your SDLC — triggered by an issue, a slack message, or a schedule, and coordinated by warp factories from triage through review to a mergeable PR."*
Repository `cloud-factory-demo` (README, first lines): *"the canonical source for a simple cloud factory: a set of agent skills and GitHub Actions workflows for moving work from an incoming issue to a verified change"*, organized around six stages (triage, spec, implementation, verification, review, with monitoring a later stage). It also says the pattern is platform-independent.

**Answer: not the same wording and not the same level.**

| Aspect | warp.dev | cloud-factory-demo | the talk |
|---|---|---|---|
| What a factory is | a fleet of agents, wired to the SDLC, coordinated by the product | a set of skills and workflows | a graph of steps; a mindset |
| Trigger | issue, Slack message, schedule | issue labels, PR events, a daily schedule | ideas, from trackers, chat, IDE, monitoring |
| Stages | "triage through review" | triage, spec, implementation, verification, review (monitor later) | triage, spec, implement, review, verify, ship, monitor (loop) |
| End result | a mergeable PR | a verified change | shipped software, then monitored |
| Humans | put work in; agents "loop you in proactively"; "approval gates" and checkpoints are defined in `factory.yaml` | spec PR reviewed by a human; review.json before publish | human points: spec, code (by risk), product |
| Who coordinates | the product (a routing agent "Foreman") | GitHub Actions + labels | not named (control plane) |
| Self-improvement | built in: every run scored, observer agents open PRs against the factory config | the daily review-skill improvement job | the skill loop |

What is the same: the core idea (agents move work from an incoming item through stages with human checkpoints). What differs: the site defines the **product** (a managed platform, ends at a mergeable PR); the repository defines a **pattern** you can reimplement; the talk adds verification by computer use, shipping, monitoring and the feedback into new inputs. The site's own page adds notions not in the other two: **factory as code** (`factory.yaml`: repositories, secrets, MCP servers, cloud providers, integrations, agent defaults; separate files for agents, automations, runners, skills, webhooks, scorers, benchmarks), **scorers** (grade completed agent conversations), **benchmarks** (replay the team's conversations to compare models), and "any surface" (watch, steer, hand off from web, mobile, terminal, IDE).

Comparison with our `definition.md`: the site's definition is a **mechanism** statement (what it is built of); ours is a statement of **responsibility** (principals state intent; executors do everything between intent and verified result; four conditions). Warp's wording has no independent-verification condition and no boundary-enforcement condition; "approval gates" and "checkpoints" are close to our human decision points. Vendor claims on the page (20-30% of PRs fully automated at the start, cost per PR from $80 to $30 in one case study) are marketing and weak evidence.

## 3. New mechanisms for the catalog
`factory-as-code`, `scorers-on-agent-runs`, `model-benchmarks-on-own-work`, `routing-agent-foreman`, `any-surface-steering`.
