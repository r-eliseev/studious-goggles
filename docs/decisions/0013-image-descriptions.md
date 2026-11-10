# 0013. Image description: the assistant model, a table of descriptions

Status: accepted
Date: 2026-10-28

Task: `tasks/backlog/019-img2text.md`. The owner's decisions are in its "Comments" (2026-10-28).

## Context
The user wants a description of a picture or a prompt that reproduces it (F4.9). The text must get into the input field only by the user's action, and the provider, model and price must be visible.

## Decision
- The description is written by the assistant model of the current session. There is no separate setting in the skeleton. If the assistant model does not accept pictures, the request is not sent and the user sees the reason.
- Two modes: `Description` (a description in the UI language) and `Prompt` (an English prompt). The system text of each mode is in `DescriptionPrompts`.
- The description and its price are saved in the `image_descriptions` table (migration `0005`, per session, cascading with the session). The price is `NULL` when the provider did not report it; `NULL` is not shown as zero.
- An empty answer and a provider error are not saved. A repeat on the same picture is a new request, there is no cache.
- The text is not inserted automatically: the dialog shows the description, the model, the provider and the price, and insertion happens on a button. If the input field is already filled, the user chooses "replace" or "append".
- The source is a result picture (a generation). Attachment pictures (018) are not described in this task.

## Alternatives
- **A separate description model in the session settings.** Needs a screen and a settings field. Rejected by the owner: the assistant model; a separate setting is in 027.
- **Not storing descriptions.** The price would not survive a restart, and it must be accounted for (task 021). Rejected by the owner.
- **A description cache per picture.** Adds complexity without a request from the user. Rejected by the owner.

## Consequences
- The description language is the UI language (currently only English); the prompt is always in English.
- Failed chat completions requests: whether they are billed is unknown. The price of a failed request is not recorded.
- The description text is user data; it is written to the log only at Debug (like assistant messages, ADR 0010).
