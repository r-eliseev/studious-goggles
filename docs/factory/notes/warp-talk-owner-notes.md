# Analysis of the owner's notes on the Warp talk

Source: `docs/factory/sources/warp-talk-owner-notes.md` (transcription of two handwritten pages). Companion of `warp-talk` (talk transcript) and `cloud-factory-demo` (the repository recommended by the speaker). Labels: **in the transcript** = the point is in the verbatim talk text; **not in the transcript** = the point is not in the transcript, so it may come from the slides, from another source, or from the owner's own synthesis (the slides are not in the transcript and cannot be checked here).

## Page 1: what a factory is, inputs, outputs, stages

| Note | Maps to in the talk and sources | Status |
|---|---|---|
| "Warp open source, there is a factory; factory engineering" | the thesis `factory-engineering-thesis` and the open-source public factory `public-factory` (`warp-talk`) | in the transcript |
| Questions: what a factory is, how it functions; what is the input; what is the output; which stages | the owner's own frame; compare `definition.md` (parts, conditions) | the owner's structure |
| Input: "idea or bug", from "jira + slack + metrics" | the talk: inputs are "ideas", from the team and users, channels "task tracker, Slack, terminal or IDE, monitoring systems" (`lifecycle-loop`, `monitoring-feedback`); "metrics" = the monitoring output; Jira is the owner's example of a tracker | in the transcript (Jira is the owner's wording) |
| Stages: triage, spec, impl, verify, ship, monitor, with a loop back to "idea or bug" | the lifecycle loop: idea, triage, spec, implementation, review, verification, ship, monitor, back to the top; the notes omit the review stage (`review-risk-routing`) | in the transcript (review not drawn) |
| Triage: "simple → cr [?], hard → spec (product / tech)" | `triage-agent`: easy and unambiguous goes straight to implementation, hard goes to specs; the specs are the product spec and the tech spec (`product-and-tech-spec`). "cr" is read here as code review or change request; unclear | in the transcript ("cr" unclear) |
| "look at the inner stages" | the plan to study each stage; compare `cloud-factory-demo` (the stages as skills: triage, spec, implementation, code review, verification) | the owner's plan |
| "formal proof" | not found in the transcript or in the repository notes; possibly a point about verification that was said or shown elsewhere | **not in the transcript, meaning unknown** (question to the owner) |

**Output of the factory** is not named on page 1, but page 2 gives it: "software shipped" (the numerator of the efficiency formula).

## Page 2: control plane, data plane, efficiency, two loops

| Note | Maps to in the talk and sources | Status |
|---|---|---|
| **CP** (control plane) | `control-execution-data-planes`: "a control plane for figuring out how work gets distributed across your factory floor" | the name is in the transcript; the contents are not |
| CP **config**: skills + MCP; model / harness; env + permissions + memory [?] | the talk says the execution plane decides "what's the harness? what's the model?" and runs in cloud sandboxes; the notes place these choices as **configuration of the control plane** and add skills, MCP, environment, permissions and memory | **not in the transcript** |
| CP **arch**: agent routing; rules? | "figuring out how work gets distributed" = routing; "rules?" is the owner's open question whether rules belong here | routing in the transcript; "rules?" is a question |
| CP **admin**: cost, governance, billing; evals, user management | not in the transcript (the talk mentions measuring cost and efficiency, `efficiency-metrics`, but not billing, governance, user management or evals as parts of a plane) | **not in the transcript** |
| **DP** (data plane): artifacts, drive; skills, memory, MCP, KB; rules? | `control-execution-data-planes`: "a data plane that sits below your factory... lets agents remember, learn, improve"; the notes list its contents: artifacts and storage, skills, memory, MCP, knowledge base, and again "rules?" | name and purpose in the transcript; the list is not |
| **Efficiency = software shipped / (tokens + human time)** (layout read as a fraction) | `efficiency-metrics`: "how much software did you ship? how much did it cost in terms of human time and token time"; the notes make it a formula with the output in the numerator | in the transcript as words; the formula is the owner's |
| **Two levels of loops**: the inner one does the work, the outer one improves the loop | `skill-loop` (observer agents improve the skills); `skill-improvement-outer-loop` in `cloud-factory-demo` uses the very words "inner loop" (the review agent works all day) and "outer loop" (a daily job that learns from human reactions) | the skill loop is in the transcript; the "two levels" naming is the owner's, and matches the repository |

## Remnants at the top of page 1

The struck-out "tsf skill?" and the circled line ending "... as it progresses" look like the end of the list of questions about the TSF lesson (`tsf-skill`, `adaptation` in `tsf-lesson-questions.md`): the owner's own notes are probably the origin of those questions, crossed out once covered. Marked probable, not verified.

## What the notes add to the concept

- A **definition of inputs and outputs** (input: idea or bug; output: shipped software), added to `definition.md`.
- A **breakdown of what each plane holds** (control plane: configuration, architecture, administration; data plane: artifacts, storage, skills, memory, MCP, knowledge base): catalog entries `control-plane-contents` and `data-plane-contents`.
- A **single efficiency formula** (`efficiency-formula`) to measure the factory (`attention-metrics` is its human-time part).
- The **two-level loop** naming (`two-level-loops`), which confirms the structure found in `cloud-factory-demo`.

## Open questions (asked to the owner)

1. Where do **rules** belong: in the control plane (architecture) or in the data plane (knowledge)? The owner wrote "rules?" in both places (`rules-placement`).
2. What does **"formal proof"** refer to?
3. Does **"cr"** mean code review, change request, or something else?
4. Are the control-plane and data-plane lists from the talk's slides, or the owner's own synthesis?
