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

| `warp-talk-owner-notes` | `warp-talk-owner-notes.md`: the owner's handwritten notes on the Warp talk (two photographed pages, transcribed) | **PRIMARY** (the owner's own notes; companion of `warp-talk`) | ru/en | `notes/warp-talk-owner-notes.md` |

| `ai-engineer-talk-page` | `ai-engineer-talk-page/`: the AI Engineer page of the Warp talk (site essay, machine transcript, slide index of the first 447 s) | secondary (the essay is derived; the transcript is machine) | en | `notes/warp-site-and-ai-engineer-page.md` |
| `warp-dev-site` | `warp-dev-site/`: text of the warp.dev homepage, with the vendor's definition of a factory (FAQ) | secondary (vendor marketing, state of 2026-11-18) | en | `notes/warp-site-and-ai-engineer-page.md` |

Rules: sources are never edited; when sources conflict, the primary source and the owner's later statements win; every claim in a note names its source by name.
