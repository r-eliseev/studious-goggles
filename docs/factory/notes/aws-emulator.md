# Notes: AWS Emulator, a factory that builds a large system with no human reviewing the code

Sources: `sources/aws-emulator/` (two articles by the lead engineer, the launch post, the public repository's `AGENTS.md` and `README.md`). Owner supplied the second article on 2026-11-25. Status of the evidence: **practitioner report by a vendor employee**, one case, numbers not independently verified; but unusually specific and candid about failures, and the code is public (not inspected by me).

## 1. What it is (as stated by the sources)
- A local emulator of AWS services, written in Rust: about **20 million lines written entirely by agents without any human reviewing it, over roughly four weeks**; the team wrote about **1 million lines** itself over five months (the framework and tooling). The author calls "100% by agents" "mostly true" (article 1).
- Scale: 17,392 issues fixed by 2026-11-19, 1,400 to 2,200 issues a day after launch, about 400 agents working concurrently, about 6,300 branches a day, 130K branches, mainline about 40K commits, repository about 80 GB; releases every 2 hours when it works (article 2).
- Three teams of agents (launch post and article 2): (1) **a test suite team** writes "fidelity tests" from the public AWS API models and **verifies each against real AWS** before submitting; (2) **a finding team** replays the fidelity tests against the emulator and files issues, grouping failures that share a root cause and **referencing the failing tests explicitly**; (3) **a fixing team**: one agent per issue.
- A **merge gate** (compile, review agents for coding standards, security, and "did not regress fidelity"); failures send the change back to the agent. Then a release pipeline built on AWS developer tools.
- Every workflow is a **deterministic state machine** (AWS StepFunctions) around the agents (agents run in AgentCore Runtime): claim the issue, create a branch, start the agent session, poll for the result, run the fidelity suite, mark complete. Breaking the workflow into small steps made failures retryable outside the expensive agent session.
- Origin: the author took the idea from Anthropic's autonomous C compiler: "if given a well-defined spec and a reference implementation, agents can autonomously build a solution that faithfully implements the specification"; here the spec is the AWS API models and the reference implementation is real AWS.

## 2. Mapped to our definition (`definition.md`, four conditions)
| Condition | What the sources show | Strength |
|---|---|---|
| Independent, measured verification | Fidelity tests are checked against a **reference implementation outside the agents (real AWS)**; the factory publishes a fidelity score per release; canaries rebuild a small emulator from scratch from the API models and measure the score; public GitHub issues are classified as factory defects (about 20% already fixed, 35% known but not fixed, 40% not found because no fidelity test existed, 5% feature requests) | strong oracle; but the tests themselves are written by agents, and coverage is the weak point (the 40%) |
| System-enforced boundaries | `vendor/` is human-owned and **agent-immutable, enforced by checks**; agents have no internet (prompt injection) and no real AWS usage data (customer data); merge gate reviewers; a reviewer limits any touched file to 5,000 lines; root `Cargo.toml` and some test registry files are marked not editable in `AGENTS.md` | strong |
| Feedback loop to input | the finding team's issues are the inputs of the fixing team; the release pipeline files a high-priority issue when the build breaks; public issues feed back; the humans improve the machine, not the code | strong |
| Humans only at decision points | humans own the framework, tools, context and guardrails ("five touchpoints"), and decide on features; no code review | present, with a large upfront human investment |

## 3. Lessons stated in the sources (all from the author; ✔ read)
- **Agents overfit to incomplete tests** (RDS): they implemented "just enough SQL" for the fidelity tests to pass; fixed by guidance to reuse libraries (SQLite, RustPython). Same mechanism as Goodhart (`concepts/goodhart-campbell.md`) and as the ImageGen acceptance-test problem.
- **A task must carry its own check**: issues that said only "what is broken" were fixed badly; issues that reference the failing tests were fixed in about half the time (the author's own test, no data shown).
- **Structure of the code is a control lever**: a 48,000-line EC2 file (about 850K tokens) filled the context window and caused merge conflicts; a merge-gate reviewer with a 5K-line limit led agents to split 59 files in a day and roughly halved conflicts; one-file-per-test with automatic module registration removed a contention point. (Compare `path-scoped-spec-loading` and task 298.)
- **Agents break each other without a textual conflict**: a test and a change to business logic from two agents were each fine, together broken (a DNS name format changed); and flaky timing-dependent tests written by agents (a hard-coded 3-second window). With 400 agents this is routine. Compare ImageGen's regressions. Not solved: "the next step is to help them not break each other".
- **Treat the factory as a distributed system**: deterministic orchestration, retries with backoff and jitter in the workflow engine instead of inside the agent (an agent session starts in over a minute at p50; retrying branch creation inside it throttled the git service until it moved to a workflow step), shallow clones, caching, canaries, alarms.
- **Escape hatches**: standard behaviour in the vendored framework, with a hook where a service can override it (error serialization); lets agents handle non-standard cases without forking the framework.
- **Outage**: a 39-hour gap in releases at the launch (2026-11-11 to 2026-11-12), and a 10-hour gap while writing; the cause was an agent pushing a build-breaking change; the pipeline files an issue and agents fix it themselves.
- **Reactive start**: the first version (February) fixed customer-reported issues one by one on the author's machine (about 1,000 issues in 9 months); it hit a ceiling "because it's reactive to customer feedback"; the factory instead finds issues proactively.

## 4. What this adds to the concept
- **A precondition for the strongest factory**: a **spec plus a reference implementation as an oracle** (the author's own statement). With it, verification independent of the agents is cheap. ImageGen has no reference implementation (what is "right" is the owner's taste), so its oracle problem is different: see `imagegen-acceptance-not-independent`.
- **The emulator is a digital twin built by agents and scored against the real service**: direct evidence for the thread on twins (`twin-95-percent`, `twins-just-mocks`), including how its fidelity is measured (published scores per release) and where it fails (about 40% of public reports were scenarios with no fidelity test).
- **Division of agents by role**: tests, finding and fixing are different agents: matches `split-agents-by-errors`.
- **A factory metric from outside**: the classification of public issues as "defect in our system" is a recall measure of the factory itself, comparable to the efficiency formula (`efficiency-formula`).
- **Rules turned into checks** (`type-rule-to-linter`): immutability of `vendor/`, the 5K-line limit, no empty auth schemes, "do not edit" markers: all in the repository's `AGENTS.md`, and the first two enforced by the gate according to the articles.
- **Counter-point to cautious positions**: this is evidence that the top level (no human review of code) is reachable for a well-specified domain with an oracle; it is also one vendor's claim about itself.

## 5. Not verified / questions
- The numbers (20 million lines, 400 agents, issue counts) are the author's; I did not inspect the repository, the fidelity scores or the `issues/` folder.
- How the fidelity tests are validated against real AWS (what if real AWS is non-deterministic or changes) is not described in what I read; the launch post promises later articles on how fidelity is measured and how quality and security are ensured (the series says 2 articles so far).
- The cost of the machine (agents, StepFunctions, compute) is not given.
- The "three teams" framing: the launch post says the split exists because agents "have more success on bounded tasks"; no data.
