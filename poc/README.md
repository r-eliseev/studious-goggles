# Proof-of-concept code

This directory contains code written to resolve uncertainty before implementation: whether a dependency builds for a target platform, what an API actually returns, or how a component renders. It is not authoritative and is not part of the solution: projects in `ImageGen/` do not compile this code.

- Layout: `poc/<release>/<NNN-task-name>/<repository-path>`. A file is stored at the path where it would appear under `ImageGen/`.
- A task may link to a `poc/` directory as a reference. The source of truth is the task description and plan, `docs/architecture.md`, and the specifications. If proof-of-concept code differs, update the plan.
- Proof-of-concept code is not kept up to date: it may stop building after renames or refactoring. Do not copy it verbatim; compare it with the current code.
- Write new proof-of-concept code only to resolve real uncertainty, not for renames, language-version changes, or similar small edits.
