# What "rules" are, and how they differ from skills and permissions

Question from the owner (2026-11-18): rules were on the slide in both planes; skills and permissions are understood, but rules seem separate from them.

## What a rule is, by the primary source
Warp docs, "Rules for agents" (`sources/warp-dev-site/docs-rules-for-agents.txt`): *"reusable guidelines that inform how agents respond to your prompts... tailor responses to match your coding standards, project conventions, and personal preferences."* Two kinds: **global rules** (apply everywhere, kept in the user's space) and **project rules** (kept in the repository in `AGENTS.md`, root and subdirectories; the most specific wins: subdirectory, then root, then global). The agent pulls in applicable rules automatically and the conversation shows which were used. `/init` can link other formats (`CLAUDE.md`, `.cursorrules`, `GEMINI.md`, `.clinerules`, copilot instructions).

So a rule is **always-on standing guidance put into the agent's context**, scoped by place (global, project, directory).

## The three things side by side

| | Rule | Skill | Permission |
|---|---|---|---|
| What it is | standing statement: "always do/avoid X in this place" (conventions, standards, constraints) | a named procedure for one kind of task ("how to do triage") | a system setting: what the agent is allowed to do or reach |
| When it acts | always, loaded automatically by scope | on demand, when the task matches | always, at the moment of the action |
| Who enforces | **nobody**: the model is asked to follow it; it may ignore or misread it | nobody: the model follows the procedure | **the system**: the action is blocked or allowed |
| Form | prose | prose + files/scripts | configuration |
| Can fail silently | yes | yes | no, a block is a visible event |

Short formula: **permission = what you can do; skill = how to do a given task; rule = what to keep in mind always.** Only the permission is enforced by the system.

## Why it matters for our concept
Our factory definition has the condition "system-enforced boundaries". A rule written in prose is **not** such a boundary. If a rule matters (for example "never publish keys", "irreversible actions go through a human"), it has to exist twice: as a rule that tells the agent (so it does not try), and as a permission or check that forbids (so it cannot). A rule with no enforcement is a **wish**, an advisory input.

## Where do rules belong (proposal, waiting for the owner)
They are **two different things under one word**:
1. **Advisory rules** (conventions, style, project knowledge): these are knowledge, so the **data plane** (versioned with the project, read by agents, improved by the outer loop).
2. **Enforced rules** (policy, limits): these are **permissions and checks**, so the **control plane** (administration and architecture).
The slide having "rules?" in both places fits this: the owner's own question mark was probably the same doubt.

## Our living example
This repository already has both. `AGENTS.md` is a set of project rules (language, main code rules, prohibitions, procedure): advisory. `scripts/check.sh` (non-Latin text rejected in checked paths) and CI are the enforced part: a rule backed by a check. Rules in `AGENTS.md` without a check ("do not commit keys", "do not rewrite the prompt in secret") rely on the agent's compliance; adding a check for each is a way to move rules from advisory to enforced. Which of the current rules have no check has not been counted.

## Open
- Confirm or correct the split above (`rules-placement`).
- Are there rule kinds that are neither advisory nor enforceable by a check (for example "keep the documentation honest")? Those would need the human+agent review of the factory's own rules (`review-factory-rules`).
