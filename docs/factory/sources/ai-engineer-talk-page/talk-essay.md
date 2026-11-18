# Software Engineering Is Becoming Factory Engineering — Zach Lloyd, Warp

Zach Lloyd, Warp

[Talk page](https://ai.engineer/talks/tUPPVhBBcoM-software-engineering-is-becoming-factory) · [Video](https://www.youtube.com/watch?v=tUPPVhBBcoM)

## Software Engineering Is Becoming Factory Engineering

Zach Lloyd’s software factory moves work from ideas through specs, implementation, review and monitoring. The engineering challenge is to make that process improve—and keep human judgment focused on whether the product is useful.

### Shipping without writing the code

[0:13](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=13s)

Zach Lloyd, Warp’s founder and a former Google principal engineer who led engineering on Google Docs, opens with a striking change in his own work: he still ships frequently, but has not written a line of code in six months. Warp has changed alongside that shift. It began as a terminal, added agents, and is increasingly focused on automating development beyond an interactive session.

![Selected presentation frame from Software Engineering Is Becoming Factory Engineering — Zach Lloyd, Warp at 243 seconds](https://ai.engineer/api/talk-essay-media/13f5a091bb4e2e78dc023ad94678c494ae6fccec1a79deb218524e54726584e8)

Shipping without writing the code

The progression runs from chat and autocomplete tools such as Cursor and Copilot to interactive agents such as Claude Code and Warp. In the interactive model, a developer sits at a computer and tells an agent what to do. Automation extends that arrangement: work can move through several development stages without a person initiating every handoff. Lloyd predicts that transition will accelerate over six months to a year, while acknowledging that its pace is hard to predict.

The room’s show of hands makes the gap concrete. Almost everyone is using agents and running several at once; fewer than half appear to be running them in the cloud, and only some have automated the whole development life cycle. Using several coding agents is already familiar. Connecting triage, specification, implementation and review into a working system is the next step.

A software factory follows the familiar development loop. Ideas enter; agents triage them and write specs for complicated work; humans review those specs; agents implement; humans and agents review the code; agents verify; humans review the product; the change ships and gets monitored. The factory metaphor shifts the engineer’s responsibility toward building and managing the system that performs those steps.

### Why automation made open source more practical

[4:24](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=264s)

Warp’s move to open source provides the first application of that idea. After five years of building closed, the company wanted to build a public factory. Its public view shows issues moving through the system, their current states, and the agents and contributors working on them. Lloyd describes it as a working proto-factory at scale, with imperfections still to resolve. Earlier in the talk, he reports over 60,000 GitHub stars, a couple hundred contributors and over 800,000 active developers using Warp.

The business motivation starts with a cost problem: cheaper software development also makes competing products easier to clone. A great product remains necessary, but Lloyd’s judgment is that it becomes harder to capture value through the product alone. Distribution, an ecosystem, brand, proprietary data or capital can provide additional advantages. A startup may have little of any of them.

Building in the open offers a way to grow an ecosystem, community and brand while developing the product. Lloyd’s deliberately modest version of the reputational benefit is going from “hated on Hacker News” to “tolerated.” The obstacle is maintenance: more participation can bring noisy issues, sloppy pull requests, lengthy code reviews and expensive verification.

Automation changed Warp’s decision because it could take on some of that maintenance work. The company built a set of automations around managing the open-source project, making the public factory both a development system and a way to handle contributions. That is the causal link between the open-source digression and the engineering thesis: opening the project brings work in, and the factory helps move that work through.

### From incoming issue to verified change

[7:55](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=475s)

An effective factory needs automations, context and skills, ways to bring humans in when necessary, and mechanisms for improving itself. Those requirements apply beyond open source: agents can help contributors contribute and maintainers maintain, but an internal product team has the same underlying problem of moving work through development. Lloyd expects factories eventually to become as routine as CI/CD.

The factory floor is a graph of steps. Its inputs are ideas from a team or its users, arriving through task trackers, communication channels such as Slack, a terminal or IDE, or monitoring systems. Defining that graph means deciding how this particular product gets built and where work should pause for another decision.

Triage supplies the first consequential branch. An agent examines an incoming issue: if it is easy and unambiguous, it can go directly to implementation. A harder issue should produce specs first. This gives a team a small starting point for automation without requiring every task to pass through the same amount of planning.

Warp separates the specification work into two complementary outputs:

- **Product spec:** Describes the product properties the change should establish or preserve—the behavior the implementation is meant to achieve.
- **Tech spec:** Describes the architecture and the shape of the code that will implement it.

Human review of the spec provides a chance to correct the intended result before the implementation agent starts producing a diff.

The implementation agent is a coding agent running in the cloud. Its output is a diff, which then enters review. Lloyd recommends an agent review first, followed by human review where the risk warrants it. The human review step remains available; the engineering decision is when to use it, especially as reviewing large amounts of weak agent output becomes a bottleneck.

Verification asks whether the resulting product works. For a UI, computer use can exercise the code and produce videos and screenshots; CI/CD still performs its established role. Human product review precedes shipping in the initial loop. After shipping, agents should observe crashes and usage, then feed that information back into the factory as new work.

Where does work branch, and how does a shipped change generate the next task? The diagram follows the triage decision through implementation and review, then closes the loop through monitoring. Human checkpoints sit at decisions about intended behavior, code risk and the resulting product.

The issue-to-product loop
Easy, unambiguous work can skip specification. Monitoring returns observations to the same intake path that receives new ideas.
- Incoming work — Team ideas, user issues and monitoring observations
- Agent triage — Assess difficulty and ambiguity
- Specs and human review — Product behavior plus technical design
- Implementation — Cloud coding agent produces a diff
- Code review — Agent first; humans participate according to risk
- Verify and review the product — Computer use, CI/CD and human product review
- Ship and monitor — Observe crashes and usage
- Incoming work → Agent triage
- Agent triage → Implementation (Easy and unambiguous)
- Agent triage → Specs and human review (Harder work)
- Specs and human review → Implementation (Reviewed intent)
- Implementation → Code review (Diff)
- Code review → Verify and review the product
- Verify and review the product → Ship and monitor
- Ship and monitor → Incoming work (Monitoring feedback)

### The infrastructure beneath the factory floor

[11:57](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=717s)

A simple factory is approachable; a factory that scales brings a larger infrastructure burden. Lloyd points to an internal system at Uber as an example, but advises most organizations to consider whether building that infrastructure would distract from their own product. Either a built or purchased system still needs several layers underneath its workflow.

- **Work intake:** Provides the ways tasks enter the factory.
- **Control plane:** Decides how work gets distributed across the factory floor.
- **Execution:** Runs work in cloud sandboxes and selects the agent harness and model.
- **Data plane:** Lets agents remember what they have done and supports learning and improvement over time.

The workflow describes the development steps; these layers provide the machinery for running them.

Operating that machinery requires measuring its efficiency. Lloyd names shipped software, human time and token time as quantities to track. More generated code alone does not describe the factory’s performance: the useful comparison is what gets shipped and what it costs to get there. Those measurements give the team something to improve over successive runs.

### Turning a corrected review into a better next run

[13:58](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=838s)

Self-improvement adds another loop around the agents doing the work. Factory agents apply skills; observer agents examine how those skills are used, look for failures and try to improve them. The feedback concerns the development process itself, rather than only the product being developed.

![Selected presentation frame from Software Engineering Is Becoming Factory Engineering — Zach Lloyd, Warp at 880 seconds](https://ai.engineer/api/talk-essay-media/7efc49332072848e526d1dbd41fa4279f40b7c8a4a5fcc6405267318b495afd6)

Turning a corrected review into a better next run

The concrete example is a code-review agent leaving comments that a senior engineer then corrects. Without a skill loop, those corrections help the current review but may have no effect on the agent’s next run. With an observer, the corrected comments become input to improving the review skill. The intended change is that the next review uses the revised skill instead of repeating the same approach. This is a proposed feedback mechanism; the talk does not establish a measured improvement or specify how skill revisions are evaluated.

How does a human correction travel beyond the current pull request? The diagram makes that path visible: the observer connects corrected review output to the skill used on the next run. That connection is what turns an individual intervention into a possible improvement in the factory.

A code-review skill loop
Human corrections become feedback for changing the review skill before another run.
- Review skill — Instructions applied by the factory agent
- Agent review comments — Output of the code-review agent
- Senior engineer corrections — Human changes to those comments
- Observer agent — Examines corrections and improves the skill for the next run
- Review skill → Agent review comments (Apply skill)
- Agent review comments → Senior engineer corrections (Human correction)
- Senior engineer corrections → Observer agent (Observe feedback)
- Observer agent → Review skill (Improve for the next run)

### Engineering the thing that builds the product

[14:48](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=888s)

This changes where engineering effort goes. Building the product now includes building the thing that builds the product: arranging agents, improving their skills and finding where the process needs human intervention. Lloyd calls this “meta engineering.” His forecast of coding less and shipping more has a personal tradeoff: it may appeal to people whose satisfaction comes from shipping, while taking away some of the work enjoyed by people who love writing code.

![Selected presentation frame from Software Engineering Is Becoming Factory Engineering — Zach Lloyd, Warp at 982 seconds](https://ai.engineer/api/talk-essay-media/e8030e487e66eaccc4d409bcc230bcc82652507ffcaca6c1a7014cdf7a0e4750)

Engineering the thing that builds the product

For a practical starting point, Lloyd introduces an open-source GitHub repository for building factory agents, including triage and spec-writing agents. It uses Warp’s agent platform, though he says using that platform is optional. The repository is meant to bridge the gap between drawing a factory loop and setting up its individual workers.

The first audience question challenges an apparent contradiction: why become a factory engineer if building factory infrastructure is a distraction? The answer separates deploying the infrastructure from tuning it for a product. Even with a purchased system, engineers must decide whether its skills fit their domain and whether it builds their product in the right way. Some organizations can build the infrastructure themselves; Lloyd’s default advice is to keep attention on the core product while doing the tuning that makes the factory useful.

### Adaptability still needs systems knowledge—and taste

[17:50](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=1070s)

Asked where a graduating student should spend their time, Lloyd emphasizes adaptability, critical thinking and the speed of learning. Systems knowledge remains valuable because someone must understand the architecture, reason about generated code and assess the specs agents write. The desired engineer can solve product problems while the tools underneath the work keep changing.

Warp’s hiring experience, he says, has involved hiring more people than ever, rather than cutting hiring because of AI. That is an observation about Warp, not a conclusion about the whole labor market. The qualities he is looking for are adaptable, product-focused thinkers who can understand and solve problems as the underlying technology changes.

The final question puts discovery, ideation, design, vision and taste back into the picture. A factory metaphor can sound mechanizing or dehumanizing, but its output still has to be useful. A factory producing things nobody cares about has missed the purpose of the work, regardless of how efficiently it runs.

Human taste, input and product sense remain essential at the points that cannot be automated. Lloyd describes his own work here as figuring out what customers want and what will be valuable to them. The factory can organize how a change gets built; people still have to guide it toward something worth building.

## Key takeaways

- Start with triage: easy, unambiguous issues can go directly to implementation; harder work benefits from product and technical specs reviewed before coding.
- Keep review, product verification and monitoring in the factory. Shipping completes one pass; observations from production create the next inputs.
- A skill loop uses human corrections to improve future agent runs. Lloyd’s example connects corrected code-review comments to an observer that revises the review skill.
- Buying factory infrastructure still leaves engineering work: fit the skills and workflow to the product, measure shipped output against human and token time, and preserve human product judgment.

## Slides

### Self-Improving Software Factories: The New Open Source Model

Zach Lloyd introduces his view of how development is changing through self-improving software factories and a new open source model.

[0:13](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=13s) [Title slide for Zach Lloyd’s talk at AI Engineer World’s Fair 2026.](https://assets.ai.engineer/sites/aiecode2025/sha256/45228f5fe925f71dd4bc76c27ff48f668f4a8e4af2d3b5651b0a4ba61a620b68.webp)

Self-Improving Software Factories: The New Open Source Model

Zach Lloyd — Warp
AI Engineer World’s Fair 2026

warp

### Zach Lloyd

Warp founder Zach Lloyd led engineering on Google Docs and has over 20 years of experience. He says he still ships frequently despite writing no code in the last six months.

[0:26](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=26s) [Zach Lloyd’s engineering background and a portrait.](https://assets.ai.engineer/sites/aiecode2025/sha256/4e4f6baa350cafe6815966e18c74382294c9e38d4ff7331b84cc69dd2d0c36e5.webp)

Zach Lloyd

• Principal engineer at Google, lead on GDocs
• 20 years of engineering
• Founder of Warp

### About Warp

Warp began as a terminal and now combines it with agents. Its screenshot places sessions, a terminal, and a code editor side by side; Lloyd says Warp is increasingly focused on automating development.

[0:48](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=48s) [Warp overview with open-source and developer statistics alongside a product screenshot.](https://assets.ai.engineer/sites/aiecode2025/sha256/85f2be3e992fb026e4c8ebf59fe60071863fda7fc6113d5d5fec517149952e6d.webp)

About Warp

• Open Source ADE
• 62K stars
• 800k+ active devs
• From the terminal to the cloud

3

Interface text:
Search sessions, agents, files...
cargo
Code editor
Multiple files
Refactor the button component to use...
Terraform my gcloud setup to use t...
shared_sessions.rs
Testing block unfurling
Settings
MCP servers

Cargo check
Blocking waiting for file lock on build directory
warning: unused import: `std::iter::FromIterator`
 --> app/src/channel.rs:2:5
2 | use std::iter::FromIterator;
  |     ^^^^^^^^^^^^^^^^^^^^^^^
  = note: `#[warn(unused_imports)]` on by default
warning: 1 warning emitted
Finished `dev` [unoptimized + debuginfo] target(s) in 7.27s

[illegible code]

### Software engineering becomes factory engineering

Lloyd predicts that software engineers will build and manage software factories, where agents and humans share work across development.

[1:31](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=91s) [Thesis: software engineering will become factory engineering.](https://assets.ai.engineer/sites/aiecode2025/sha256/618c16aa1f26fbbfe5760c8b3c887c9828103fdc9537efc660334db71d6e3d16.webp)

Thesis:

Software engineering will become factory engineering.

4

### The evolution of agentic development

Lloyd places today at interactive agents, where developers direct tools from their computers. He expects a shift toward automation over the next six months to a year, though the pace is uncertain.

[1:48](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=108s) [A timeline from chat and AI autocomplete through interactive agents to automated agents, with interactive agents marked as today.](https://assets.ai.engineer/sites/aiecode2025/sha256/714979fdc8f14f797613b0dd31fc36bc327f55b8cbd86f600719efa51b22c097.webp)

The evolution of agentic development

Chat
AI Autocomplete
Interactive Agents
TODAY
Automated Agents

6

### Building with agents

Lloyd asks for a show of hands and says 100% of the audience is building with agents.

[2:24](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=144s) [Audience question: How many of you are building with agents?](https://assets.ai.engineer/sites/aiecode2025/sha256/c3c4854a80ee5c01014135368d39aa64e930b9864aca8bc3366cc8b289bd42c1.webp)

How many of you are
building with agents?

7

### Running multiple agents simultaneously

Lloyd asks who typically builds with multiple agents at once; almost everyone raises a hand.

[2:32](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=152s) [Slide asks how many attendees are running multiple agents simultaneously.](https://assets.ai.engineer/sites/aiecode2025/sha256/e893a7e619d900a37cfbd6cf154325ffaabb788e4c417465994cd0d377bdf916.webp)

How many of you are
running multiple agents
simultaneously?

8

### Running agents in the cloud

Lloyd asks for a show of hands and estimates that less than half the audience is running agents in the cloud, though the number is significant.

[2:46](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=166s) [Slide asks how many attendees are running agents in the cloud.](https://assets.ai.engineer/sites/aiecode2025/sha256/642e8154fde58e03924110465c8b48e2b6fc9f3011dd49fb868397de5afcb411.webp)

How many of you are
running agents in the cloud?

9

### Automating the software lifecycle

Lloyd asks who has automated the lifecycle from triage through shipping; some attendees raise their hands.

[2:57](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=177s) [Slide asks about automating the software lifecycle and lists triage, spec, implement, review, and ship.](https://assets.ai.engineer/sites/aiecode2025/sha256/ff55daa437d89231b1769ef18c31f2335d792019c7b015f3d05f3af43da5bb0a.webp)

How many of you are automating the whole software lifecycle?

Triage → Spec → Implement → Review → Ship

10

### A cloud software factory

Lloyd envisions agents triaging, implementing and verifying in a continuous loop, with humans reviewing specs, code and the product before shipping and monitoring.

[3:21](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=201s) [Circular software lifecycle diagram with triage, spec, implementation, verification, shipping, and monitoring, plus human review points.](https://assets.ai.engineer/sites/aiecode2025/sha256/8da18e95cf5465e0f2b2c0daaa07437a26dbf6239e92ca803fbf7cd7d68665e3.webp)

New product idea
or bug reported

A cloud software factory

Triage
Spec
Implement
Verify
Ship
Monitor

Reviewing the spec
Reviewing the code
Reviewing the product

11

### What about open source?

Lloyd turns to open source, saying one reason Warp open sourced was to build a public factory.

[4:18](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=258s) [Black slide asks, “What about Open Source?”](https://assets.ai.engineer/sites/aiecode2025/sha256/29dd7ab38875b3e278ad6cabb271fcfbca231f5ff2b56c786b0ae9f42a58e44f.webp)

What about Open Source?

### Warp Contributions

Warp’s public factory dashboard tracks issues by workflow state and lists top contributors. Lloyd calls it a working but imperfect proto-factory.

[4:27](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=267s) [Warp Contributions dashboard shows issues by workflow state and a contributor leaderboard.](https://assets.ai.engineer/sites/aiecode2025/sha256/961b2946022916b69445ae860169b568847635497dbf8cf88292cf1c9d03221b.webp)

Warp Contributions
162 contributors

Triaging 2,217
5 statuses
Agent working 0
Awaiting agent 65
Needs mocks 73
Needs info 235
Awaiting maintainer 1844

Ready to spec 103
1 status

Creating spec 22
5 statuses

Ready to impleme... 1,289
1 status

Implementing 32
5 statuses

Cannot select text in a markdown table
Opened 2h ago · #12751 · 6 labels
Needs info

Please allow independent scaling of the vertical tabs sidebar (font size and row height)...
Opened 4h ago · #12743 · 8 labels
Needs mocks

macOS: native prompt renderer can stop accepting input in git repositories (HistoryNotAppe...
Opened 7h ago · #12737 · 6 labels
Closed

Copilot Warp Integration
Opened 7h ago · #12736 · 6 labels
Awaiting maintainer

SSH Remote: Project Explorer file tree [illegible]
Opened 12h ago · #12732 · 8 labels

Stage Manager shows the default Warp [illegible]
Opened 13h ago · #12730 · #12735 [illegible]

Would like to have tab groups
Opened 15h ago · #12727 · 5 labels

As as context button doesn't add contex[illegible]
Opened 20h ago · #12721 [illegible]

Agent error: 'patch contains no changes[illegible]
Opened 21h ago · #12718 · 6 labels

932,409 lines of code contributed
Top contributors
View all
anshul-garg27
19 contributions +243 -97
#1
SagarSDagdu
12 contributions +704 -85
#2
maxmilian
11 contributions +1,370 -321
#3
exzshao
10 contributions +687 -453
#4
lonexreb
8 contributions +495 -21
#5

build.warp.dev
14

Interface text:
warp
Overview
Community
View repo
61.9K
New issue
Contributors
Labels
Badges
Search issues...
Sign in with GitHub

### Why Open Source?

Warp open sourced in part to build a public factory for software development.

[5:01](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=301s) [Slide asks, “Why Open Source?”](https://assets.ai.engineer/sites/aiecode2025/sha256/a855063ef15c720e3c08379bd154017b816143ccaf097ae181e250a067265667.webp)

Why Open Source?

15

### Cost to build

Lloyd argues that software is becoming much cheaper to build. The curve falls steeply at first, then levels off over time.

[5:04](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=304s) [A cost-versus-time graph shows a declining curve.](https://assets.ai.engineer/sites/aiecode2025/sha256/b87745c9db7eaaa5d2f83bfaf6fc4eda611c8343ecb3f19493f26445a6d9a871.webp)

Cost to build:

Y-axis: COST
X-axis: TIME

16

### Time to clone

Lloyd argues that cheaper development makes software trivial to clone, making it harder for software businesses to capture value. The slide shows Same.dev’s AI website builder.

[5:18](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=318s) [A slide titled “Time to clone” shows the Same.dev website.](https://assets.ai.engineer/sites/aiecode2025/sha256/5c4aae16827c9b523e34f84da9b16730b6d101ba6c82a2fcd9ab938c67557a35.webp)

Time to clone:
Same.dev

Interface text:
same
Docs
Sign Up
Log In
Make anything
Build websites by chatting with AI
make a food delivery app with restaurant management and order tracking
Tab
+
claude-[illegible]-opus
over 600,000+ projects built with Same

17

### Building a software business

Lloyd asks how software businesses can capture value when competitors can easily clone their products.

[5:26](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=326s) [The slide asks how to build a software business when it is free to build software.](https://assets.ai.engineer/sites/aiecode2025/sha256/adb2444827507a1a16a4e4d40c3774e1e00c543309881569bf1f1bca401afe7e.webp)

How do you build a software business when it’s free to build software?

18

### Patent your code: a joke

After noting that competitors can easily clone software, Lloyd jokes about patenting code and explicitly tells the audience not to do it.

[5:41](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=341s) [A black slide says, “Tip 1: Patent your code.” The speaker identifies it as a joke.](https://assets.ai.engineer/sites/aiecode2025/sha256/265c2b9603d8fb957b6576fd877a691908cbfe3a11ea4511f6085336d42ebd72.webp)

Tip 1: Patent your code.

### You need a great product, but it is not enough

As software becomes easier to clone, Lloyd argues that a great product is essential but unlikely on its own to build a successful software business.

[5:53](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=353s) [A slide says, “You need a great product...”](https://assets.ai.engineer/sites/aiecode2025/sha256/f4ecf0b2fdf1619f51581d63fd461daf0e53181f0ce3d051d0d83293f3e8ed60.webp)

You need a great product...

20

[6:01](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=361s) [A great product is necessary, but not enough.](https://assets.ai.engineer/sites/aiecode2025/sha256/b53dd6d5b353f532762bc885d1a832ee4fb95862ae2a24fed40cc1b3422fea39.webp)

You need a great product...
but a great product is not enough.

21

### Advantages beyond the product

Lloyd names distribution, ecosystem, community, brand, data and capital as potential advantages beyond a great product.

[6:14](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=374s) [Distribution, ecosystem, community, brand, data and capital are listed as advantages beyond the product.](https://assets.ai.engineer/sites/aiecode2025/sha256/eb670c6663a0f807c02d314b740b9bb99ea1b1fa98914cdc53d84874455568f5.webp)

You need an advantage beyond the product.

Distribution → Ecosystem → Community →
Brand → Data → Capital

22

### Startups can still break through

Lloyd recommends building in the open to help startups build an ecosystem. Warp made that shift after five years of closed development.

[6:33](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=393s) [Startups can still break through.](https://assets.ai.engineer/sites/aiecode2025/sha256/d317f5bec7f012e914ba628bba9b58ac0f84a14bad785783a70cde54723ed209.webp)

But startups can
still break through.

24

### Is open source a pain?

Lloyd says building in the open can grow an ecosystem and community, while traditional burdens such as noisy issues and code review can now be managed.

[6:36](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=396s) [White text on black asks whether open source is a pain.](https://assets.ai.engineer/sites/aiecode2025/sha256/1b2bf06e5c8eae83e845a16e4c964b9d8cc28edbb5833fe53ad8af054cab1b30.webp)

But isn’t open source a pain?

### Managing open source can be a lot of work

Mock alerts illustrate the competing demands of open-source maintenance: failed releases, repeated tasks, review requests, rising errors, and urgent messages.

[7:08](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=428s) [A headline about open-source work above several mock alerts and messages.](https://assets.ai.engineer/sites/aiecode2025/sha256/687ff5855cec33f689a28de9bbbb72b820ae2f226659b93c6e0cb58906cd8756.webp)

Managing open source
can be a lot of work.

CodeHub • now
Release Pipeline #482 FAILED

Messages • now
Sid
Why is the same task running 4,000 times???

Messages • now
Oliver
Why haven’t you reviewed my PR??

Databird • now
Error rate increased 840%

Messages • now
Everything is broken SOS!!!

27

### Open-source maintenance challenges

Warp built a software factory of automations to manage its open-source project, addressing noisy issues, sloppy PRs, code review, and the work of verifying changes.

[7:22](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=442s) [Four challenges: noisy issues, sloppy pull requests, code review, and verifying changes.](https://assets.ai.engineer/sites/aiecode2025/sha256/bb9e425c85e0e1043d5756b9d0ecdb004601bddc2017e71999e0ff9a82eb4e7f.webp)

Noisy issues
Sloppy PRs
Code review hell
Verifying changes

31

## Full transcript

[0:13](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=13s) Okay. Hello, everyone. Uh, I'm excited to be here. Uh, my name is Zach Lloyd. Um, today I'm gonna be talking about self-improving software factories, the new open source model, and basically what I think is happening to development. Little bit about me just to begin. So I am a former, uh, principal engineer from Google, basically lead engineering on the Google Docs suite. I've been an engineer now for over twenty years. Long time. I am still, uh,

[0:42](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=42s) shipping frequently, but I haven't written a line of code in the last six months. Uh, and I'm the founder of a company called Warp. Uh, Warp, if you're not familiar, is a open source agentic development environment. Uh, you may know us as a terminal. That is how the company started. We're basically a terminal that has agents built in. Uh, we open sourced it a couple months ago, and I'm gonna talk a little bit about that experience and the motivation for it. It's a popular open source project, over sixty thousand GitHub

[1:12](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=72s) stars. We've had a couple hundred people contributing. We have over eight hundred thousand active developers who are using Warp. And increasingly, we are focused not just on the terminal aspect and the interactive aspect of development, but more so on how do you automate development. I'm gonna talk mostly about that. So the thesis that I have, uh, is that the discipline of software engineering is going to become something more like

[1:42](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=102s) factory engineering, and I'll explain what I mean by this in a minute, but just keep that in mind. That's, that's what I think is gonna happen. Um, if you look at development over the past couple years, it's-- I mean, it's just crazy how it's changed. We've gone from a world of chat and AI auto-complete, so Cursor, Copilot, to the phase that we're in now, which I consider to be mostly interactive agents. So you're sorta sitting at your computer, and you

[2:12](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=132s) are telling Claude Code to do something. You're telling Warp to do something. And I believe what's gonna happen over the next six months, a year, hard to predict the pace, is that we're gonna move much more towards a world of automation. But before I get into that, just quick show of hands, how many folks in here are building with agents? Ev-every... A hundred percent. Makes sense. Uh, how many,  how many folks are building typically with m-multiple agents at one time? So again, almost everyone.

[2:42](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=162s) How many people are running an agent right now? I'm not offended. I would-- Okay, that's totally cool. I, I would be doing it too. Uh, how many folks are running agents in the cloud, out of curiosity?

[2:53](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=173s) So that looks like less than half, but still significant. Uh, and how many folks have, have set up a system internally to automate the whole software development life cycle? So everything from, like, triaging, speccing, implementing, reviewing. So I see some hands. So some people are doing this. So this is what's gonna happen. Uh, every project of significant size, I believe, is gonna have something like this. Um,

[3:24](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=204s) and it's gonna look kinda like this big loop, and everyone is talking about loops. There's nothing that complicated about loops. This loop says a cloud software factory. This loop could literally just say, like, the software development life cycle. It's the same thing. Um, but just to go through this loop, it's like ideas are gonna come in at the top. Agents are gonna do triage. If something is complicated, they will write a spec. Uh, these little blue boxes are where humans step in. Humans will review the spec. Uh, agents will do the implementation.

[3:54](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=234s) A human and agent will review the code. Agents will verify. Human will review the product. You ship, and then you monitor, and round and round you go. Uh, and this is what software development, for better or worse, I think is gonna end up looking like. So I repeat the thesis, which is that if this is what software engineering is gonna look like, um, software engineers are going to be the ones who end up building and managing these factories. Now, I promised at the beginning, and I put in the title of the talk that I was gonna talk about

[4:24](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=264s) open source, and so I wanna do that for a few minutes. I'm gonna take a quick digression. Um, I bring up open source because one of the main reasons that Warp open sourced was to build, uh, build a public factory. Um, and so this is a picture of this website we've built called build.warp.dev, which shows all of the issues that are flowing through our system and what state they're in, what agents are working on them, what contributors are working on them, and it's

[4:54](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=294s) kinda like a proto-factory done at scale. It's not working perfectly, but it is working. And one of the reasons we open sourced was to try to build this. Um, just in general, I think it's interesting to talk about open source in the time of agentic development. This is a really stupid graph, but it's like... It just-- It-- You get it. It's like, it's becoming much cheaper to build software. Um, a corollary of that is that it's

[5:24](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=324s) becoming trivial to clone software. And so if you are in the software business, and I don't know how many folks in this room are in the software business per se, but it's very hard to build a software business if it's free to build software. It's hard to capture the value, especially if a competitor can clone. And so my big takeaway or big tip for everyone in here is that the first thing you should do is patent your code. Uh, I'm kidding. Don't. This is a complete joke.  Don't, don't do this.

[5:55](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=355s) Uh, my, my first tip is obviously you need to have a great product. Uh, this has always been the case, um, but I would say a great product- Probably was never enough. But even now, more than ever, if you think that you're gonna build a great software business just by building and shipping a great product, you're probably n- not gonna succeed. Uh, you need advantages beyond the product. And so, those advantages could look like distribution, ecosystem, it could be that you have a great brand or

[6:25](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=385s) a data moat. You might have capital, um, but if you're a startup, again, I'm coming from the startup world here, you just don't have these advantages. And so, you, you still wanna break through, uh, and one of the ways that I suggest doing this is by building in the open. And so, to be clear, it took Warp five years of building closed to sort of make the leap into building in the open, and I'll explain why. But if you build in the open, um, it helps build your ecosystem.

[6:55](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=415s) It, it can take you from being, like, hated on Hacker News to, like, tolerated. Uh, it can burnish  your brand, it creates community. And so, there's all these advantages to it. Uh, and some of the things that I think have traditionally been a pain, um, can now be managed. And so, like, the traditional, you know, pain of open source might be something like, you get a lot of noisy issues. You get sloppy PRs. You can end up in code review hell. You can end up having to spend a lot of time

[7:25](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=445s) verifying changes. And so, the solution, so kind of a long-winded way of getting to this, for open source or at least for Warp in the case of open source, the thing that made us finally decide to do this, was that we built a whole set of automations, really a software factory, around managing the open source project. And so, like I said, this is what I think the future is gonna look like. I'm gonna drill into it a bit just to get a little bit more technical for folks who want to try to build

[7:55](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=475s) something like this for their own projects. So, what are the components of an effective software factory? Uh, it's really not that complicated to start or at a high level. You need a set of automations. You need a way of providing context and skills. You need a way of bringing humans in at the correct time to sort of like when things get stuck on the factory. And then a really important thing is you need some set of self-improvement capabilities. So, think of this as loops.

[8:26](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=506s) And if you do this right, uh, in the open source world, you can get a, you know, world where agents are helping contributors contribute, they're helping maintainers maintain. And I wanna emphasize, there's nothing special about open source here. I think every sizable project can benefit from this approach, and I, I predict that every, every company, every open source project will have at its core a software factory, kind of like the way that CI/CD became just like, "Oh, of course you have that."

[8:57](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=537s) Uh, maybe, I don't know when that happened, ten years ago. Let's tour the tac-- uh, the factory floor for a second here. So, you're not gonna look at this. This is too much. Uh, the, the point of this  slide is not to have you read the workflow. It's that the factory floor is basically a graph, um, of steps where you are defining, like, okay, how does software get built for my product? And it looks pretty similar for every product. Things come in, they flow through, um, they get stuck at certain

[9:27](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=567s) points. Um, and, you know, broadly speaking, just to back out a second, so there's the inputs. The inputs are really ideas. Um, the inputs could be coming from your team, they could be coming from your users. Um, the inputs themselves tend to come in through certain channels that you should think about as, like, your task tracker is an obvious one, or Slack, your teams, like, your communication channels. It could come directly from, like, your terminal or IDE.

[9:57](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=597s) They could come from your monitoring systems. But there's some set of inputs that bring work into the factory. There's triage. This is a really important step. So, again, I boil it down to something very simple, but, like, you want an agent that is looking at issues as they come in, and just saying, "You know what? If this is easy and this is unambiguous, just implement it," and this is how you can actually get going with a factory. If an issue is hard, uh, I recommend having, uh, an agent that

[10:27](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=627s) produces specs. Folks in here using spec-driven development? Show of hands. People follow this? Okay. You can do this many different ways, but I think it's very effective. The way that, uh, we do it at Warp that I recommend is having an agent write what we call a product spec and a tech spec. Product spec describes the product invariance that you're building towards. Tech spec describes the architecture and the shape of the code. Then you have an implementation agent. This is basically

[10:57](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=657s) a coding agent that runs somewhere in the cloud. It makes a diff. You can use all sorts of coding agents for this. You have review. This is, in many ways, the most painful part. Like, I expect that people are a little bit tired of reviewing agentic slop. Uh, I would have an agent do code review first, and then it becomes over time, like, a risk management exercise of, like, when do you bring in humans to do code review? But you wanna have a step in here where humans can do it.

[11:28](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=688s) This is a very important step, uh, for certain types of apps, the verification step. So, this would be things like computer use, uh, if you're building a, a sort of UI, having the computer actually use the code that the agent produced and producing videos and screenshots. CI/CD, still use it obviously. Uh, and then monitoring. So agents don't stop in your factory when code is shipped. They should observe what's been shipped. Is it crashing? Is it being used? And

[11:57](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=717s) round and round you go, 'cause you take the output of this monitoring step and you feed it back into the top of the factory. Now, you could try and build this and, um, I went to a talk earlier that my friend Adam gave, where Uber has built an internal version of this, um, and it's pretty cool. Um- I would say for most, you know, most organizations, it really depends where you are, you'll be able to build a simple version of this easily. But to build a thing that actually scales is probably-- Like, you should probably be

[12:27](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=747s) focusing on your own product, not building this infrastructure, because there's a lot of stuff that you end up wanting. You're not-- Again, you're not supposed to read this. Uh, it's just a lot of stuff. If you do build it, um, or if you buy it, you'll end up with something that looks kinda like this, which is, um, you're gonna have a bunch of ways of getting work into your factory. That's what's at the top here. You're gonna have a sorta control plane for figuring out how work gets distributed across your factory floor. You're gonna have the actual

[12:57](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=777s) place where the work happens, and so that's gonna be cloud sandboxes. It's gonna be figuring out what agent to run, so what's the harness, what's the model. And then finally, I think this is a really important thing, you're gonna wanna set up some kind of data plane that sits below your factory. And so that's something that lets agents remember what they've done, learn, um, improve over time.

[13:24](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=804s) The factory is not just like a product, it's also a mindset, and this brings me back to the thesis I had at the beginning. You need to measure and improve. So factory, this is where the-- I don't know, you can stretch this metaphor as far as you want, but like you should be thinking of efficiency. And so that means like how much software did you ship, how much did it cost in terms of human time and token time, and you're gonna wanna measure this and

[13:54](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=834s) try to improve it over time.

[13:58](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=838s) A key part of this is creating loops. So, uh, loops are, again, they, they sound complicated. They're not that complicated. Loops are basically ways of, uh, having agents improve, um, by like observing what they're doing, where they're failing. Um, and so a common kinda loop that you're gonna wanna put in your factory is like a skill loop. That means you're gonna have your factory agents that are running skills, and then you'll have observer agents that are seeing how those skills are being applied, looking for issues,

[14:27](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=867s) and trying to improve the skills. So for instance, if you had a code review agent, uh, and it was leaving comments, and a senior engineer on your team was going and correcting those comments, you'd want an observer agent that would look at that and basically, uh, improve the code review agent for the next run.

[14:48](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=888s) This is one thought just to leave folks with, like what is-- where does this leave engineers? Um, I think you're gonna have to get into this mindset, and I'm trying very hard to get our team into this mindset, it's not always easy, that you're not just building the product, but you're building the thing that builds the product. And that's like, it's just different. It's more like process engineering or manufacturing or something like that. Um, and you could think like, "Okay, maybe that's a

[15:18](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=918s) bummer." Like, is that a bummer? Is that, uh, you know... And it depends. Like, it depends what joy you get out of software engineering. If your joy is in writing the code, I think you're-- everyone in, in here who is a software engineer is going to be writing less code. But if your joy is in shipping product, like it's never been a better time, and this is actually where I find my joy. It's like I like building and shipping the thing. So everyone in here is gonna code less, but they're gonna ship more, and that's gonna be a

[15:47](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=947s) trade-off. But if you approach it like you're a, a factory engineer, I think you can see like that there's still a really cool set of engineering challenges. Um, you could almost think of it as like meta engineering. Like how do you engineer your system of agents to be the best possible at engineering? But I think it's a very compelling and interesting set of challenges to solve. So that's it. Um, uh, for folks who are interested, uh, this-- if you

[16:17](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=977s) follow the link on this QR code, I've set up, um, a open source GitHub repo where anyone who wants to try building their own factory agents can do it. This uses Warp's, uh, agent platform as part of it, but you honestly, you don't have to use it. I'm not trying to like push into our product. But this should give you a good sense of like, okay, if you wanna set up, uh, an agent that does, uh, triage or an agent that does, uh, spec writing, how do you actually do that? How do you get from

[16:47](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=1007s) like the theory of, uh, working with a factory to actually putting it into practice? Um, I don't know if we have the capability to do questions in here. I saved a few minutes for questions if anyone has questions. Otherwise, I will, I will wrap up. Yes. Yeah, I have a question. So there's a bit of a tension in what you said where it's like you don't want to build this because it's a lot of work. Yes. But you are also a factory builder. Like, where do you stand on that? It's a great question. So I said something that's almost contradictory. I think, um, you should-- The way you should think of it is

[17:17](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=1037s) like y- everyone's gonna deploy some sort of factory, but then the tuning of the factory, the like are these the right skills for my domain? Is this factory building my product in the right way? I still think there's a bunch of interesting engineering challenges. And for some places you can build this. But a-again, I, I think, I think that the-- you should probably be focusing on building the core product for your company for the most part. But there's a bunch of like tuning and like, uh, figuring out how to make the

[17:46](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=1066s) factory work for your product that matters. That's a great question. Yes.

[17:50](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=1070s) Hey, Zach. Thank you so much. Um, I had a question. So if you were a college student right now-

[17:55](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=1075s) Yes

[17:55](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=1075s) ... uh, graduating and entering the workforce, where would you be spending your time?

[18:00](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=1080s) Yeah, so the, the question in case people couldn't hear was like if I was a college student graduating and entering the workforce right now. So I think that the most important skills in this new world are adaptability. I think that that's critical thinking. It's like the s- the speed at which you can learn. I do think, I don't know if you're a computer science student, but I still think there's a ton of value in understanding like the underlying systems and architecture, and being able to reason and understand. The code,

[18:30](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=1110s) understand the specs that agents are written, sorry, are writing. So I, I would focus on, on those skills. Um, and like, I don't know, we're hiring more people than we've ever hired. There's a lot of kind of like misdirection around like, you know, people not being hired because of AI. That's not the experience we've had so far. And what I'm looking for are, like, really adaptable, product-focused thinkers who can, like, basically be great problem solvent- problem solvers, uh, even as

[19:00](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=1140s) the underlying technology changes. Thank you. Yeah. I think I have time for one more question. Yes. What about the product factory, discovery, ideation, design, vision, taste, how about those kinds of things? What about the... Like, so the question was, what about the, the product taste in like the... How do you actually build something useful, I think is probably the right, like, maybe the framing and like, uh, where do the ideas come from? And so I think that, um, the problem with the factory metaphor, even though I'm like leaning into it because I think

[19:30](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=1170s) that's like, that there's something to it, is that it can kinda sound like, uh, mechanizing or dehumanizing. Um, and I still think underlying all of this, the only thing that matters is like, are you building something useful? And if you have like a, a factory that is like churning out shit that no one cares about, it's like, what's the point? And I think that h-human taste, human input, human product sense, um, humans like guiding at those touch points where you can't automate stuff is

[20:00](https://www.youtube.com/watch?v=tUPPVhBBcoM&t=1200s) absolutely, like, essential and like, that's like what I do. Um, like I'm trying to figure out what do, what do customers want, what do people want, what's gonna be valuable to them? So I think that's an absolutely key point to it. I think I'm at time, so I, I have to go. I hope folks, uh, enjoyed this chat. Uh, I'm really grateful for being invited to speak. So thank you all very much.
