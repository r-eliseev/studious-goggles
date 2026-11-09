# 0011. The generation parameter plan is stored in the generation

Status: accepted
Allowed statuses: proposed, accepted, superseded by NNNN, rejected
Date: 2026-10-28

Task: `tasks/backlog/020-generation-parameters.md`. The owner's decisions are in its "Comments" (2026-10-28).

## Context
The user sets the aspect ratio, resolution, format and quality once per session. Models accept them differently: one does not know `webp`, another knows only a few aspect ratios. Needed: send what the model accepts; show on the card what was replaced and what was dropped; store it in the generation record (F4.3).

## Decision
- The planner `GenerationParameterPlanner` is a pure function: from the request's parameters and the model's catalog it builds an outcome for each parameter: `Applied`, `Replaced` (the nearest allowed value) or `Dropped` (with a reason).
- The outcomes are stored in the generation itself (`generations.outcomes_json`), the run's parameters in `runs.parameters_json`. Repeat, New seed and Edit take the parameters of the source run, not the current session settings.
- Only what the plan accepted (`Applied` and `Replaced`) goes into the provider request. Storage and sending do not diverge, because both come from one plan.
- The model catalog stores parameters as the provider declared them (`supported_parameters`). `null` means "unknown" (a cache row from before the migration), an empty list means "not declared".
- Nearest-value rules: aspect ratio — by the logarithm of the side ratio (`auto` does not take part in the comparison); resolution — by the number, `K` means thousands; format and quality — exact match only. On a tie the value that comes earlier in the catalog wins.
- The number of pictures does not become a parameter: it is the existing `RunsPerModel`.

## Alternatives
- **The plan only in memory, not stored.** After a restart the card would not show a replacement. Rejected: the task's criterion requires storing.
- **Parameters in a separate table.** An extra table and joins for three JSON fields. Rejected: JSON in the same way as already done for session settings and the catalog.
- **Checking parameters before the run (as in the specification, section "Applying parameters to models").** Requires the user's confirmation before every run. Rejected by the owner: replacements are shown on the card after the run.

## Consequences
- The body shape of `POST /api/v1/images` for `aspect_ratio`, `resolution`, `output_format` and `quality` (names at the top level) is checked only by tests against a stub. The real service may accept them differently. A check with one paid request is manual, in `docs/status.md` (open questions).
- The resolution values `512` and `768` are treated as pixels of a side; the unit is not confirmed.
- Old records (from before migration `0003`) are read without parameters and outcomes: the card has no parameter lines.
