# Factory concept: source index

Raw material for the software-factory concept. Sources are stored verbatim; interpretation goes to `docs/factory/notes/`.

| Name | Source | Role | Language | Notes |
|---|---|---|---|---|
| `owner-dialog` | `owner-dialog.md`: the owner's web chat with Claude | **PRIMARY** (owner's thinking and decisions; the conversation continues) | ru | `notes/owner-dialog.md` |
| `owner-dialog-questions` | `owner-dialog-questions.md`: the owner's 43 messages from `owner-dialog` with the quoted context each refers to | **PRIMARY** (extract of 03, no wording changes) | ru | n/a |
| `owner-session-questions` | `owner-session-questions.md`: the owner's questions in the working session (all session questions) | **PRIMARY** (the owner's later statements win) | ru | `qa/owner-session-answers.md` |
| `tsf-lesson` | `tsf-lesson.md`: Tiny Software Factories lesson 2 | supporting (the tool: init / configure / specialize, liquid software) | en | `notes/tsf-lesson.md` |
| `warp-talk` | `warp-talk.md`: Zach Lloyd (Warp) talk | supporting (the process: lifecycle loop, skill loops) | en | `notes/warp-talk.md` |
| `always-on-agents` | `always-on-agents.md`: Anthropic, "Building always-on agents" (W. Hu) | supporting, vendor (runtime skeleton: inbox, timers, memory, permissions, outbox) | en | `notes/always-on-agents.md` |

| `cloud-factory-demo` | `cloud-factory-demo/`: the repository warpdotdev-demos/cloud-factory-demo, copied files with `PROVENANCE.md` (MIT) | supporting (a working implementation of the factory in `warp-talk`; repository recommended by the speaker according to the owner) | en | `notes/cloud-factory-demo.md` |

Rules: sources are never edited; when sources conflict, the primary source and the owner's later statements win; every claim in a note names its source by name.
