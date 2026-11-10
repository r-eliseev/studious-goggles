# 0012. Message attachments and generation references

Status: accepted
Allowed statuses: proposed, accepted, superseded by NNNN, rejected
Date: 2026-10-28

Task: `tasks/backlog/018-user-image-in-chat.md`. The owner's decisions are in its "Comments" (2026-10-28).

## Context
The user attaches up to 4 pictures to a message. They go to the assistant (if the model accepts pictures) and become references of the run the assistant starts. Image models accept references differently: some have no `input_references`, others have a limit (0–4 in the OpenRouter catalog). Attachments must survive a restart and repeated actions (Repeat, New seed, Edit).

## Decision
- An attachment is a `chat_attachments` row (id, session, message, relative path, type, time). The file is in `images/<sessionId>/attachments/<id>.<ext>`, not in the generation folder. Deleting a session deletes the attachments too (cascade and the shared session folder).
- An attachment is created before the message is sent (empty `message_id`) and is bound to the message on sending. Write order: the file first, then the row; if the row cannot be written, the file is deleted.
- Limits: up to 4 attachments per message, up to 10 MB each, PNG, JPEG, WebP. A message may consist of pictures only.
- A model's reference limit comes from its catalog: `supported_parameters.input_references.max` (stored in `ImageModel.Parameters`, see ADR 0011). A model with a limit of 0 is skipped if the user attached pictures, and the run message gives the reason. If all models are skipped, no run is created and the assistant's answer is marked as an error.
- Models with a limit get the first attachments up to their limit; the extra ones are not sent and are named in the message.
- A generation stores `reference_attachment_ids_json`. Repeat, New seed and Edit repeat the same attachments. If a file is gone, the generation gets `Failed` with a text, and the run's other generations are executed.
- The message's pictures together with the pictures of the last run (scenario S2.4 in `docs/specs/chat-sessions.md`) go to the assistant only if its model accepts pictures; no more than 4 per message in total.
- The body format of `POST /api/v1/images` for references is an array of data URLs named `input_references`. The shape is not verified against the live API (the owner's assumption).
- Before the first picture is sent, the user confirms that it goes to the provider (F10.6). The consent lasts while the chat screen is open.

## Alternatives
- **One picture per message.** Simpler, but does not cover the "compare with these two" scenario. Rejected by the owner: up to 4.
- **A new "skipped" generation status.** Would need an ADR on statuses and a separate card. Rejected by the owner: the reason goes into the run message.
- **An attachment as a separate assistant answer or a separate "make it a reference" button.** Rejected by the owner: a message picture automatically becomes a reference of the run.

## Consequences
- Migration `0004_chat_attachments.sql`: a table and a column on generations. Old generations are read without references.
- The reference limit gets data only from a catalog loaded after migration `0003` (ADR 0011). For an old cache the limit is 0 and the model is skipped; the cache is reloaded on the first access.
- The F10.6 consent is kept in the chat screen's memory: after an app restart it is asked again. Keeping it between starts is a separate decision.
- Attachment thumbnails are not shown in the message feed (an attachment is visible as a file name in the input field before sending). This is a cut from the plan, noted in the task's report.
