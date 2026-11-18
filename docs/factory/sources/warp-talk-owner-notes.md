# Source: the owner's handwritten notes on the Warp talk (transcription)

Type: two photographed pages of the owner's handwritten notes about the Warp talk (`warp-talk`), supplied 2026-11-18 together with the request "analyze these notes and add them as text to the same talk". Language of the notes: Russian with English terms, as written.
Status: **transcription by the assistant** from the photos; the originals are the photos (kept by the owner). Uncertain readings are marked `[?]`; arrows, braces and underlines are described in brackets. Nothing is added or corrected. This file contains Russian on purpose (verbatim source); per the decision `english-before-merge` all Russian text is removed or translated before merging to `main`.
Derived notes: `docs/factory/notes/warp-talk-owner-notes.md`.

---

## Page 1

[Top of the page, cut off: remains of the previous list. A struck-out line: "tsf skill?" Above it a circled line ending "... по мере продвижения" `[?]` (first words not readable).]

[Underlined heading:] Как это всё работает? и зачем? и почему?

Warp open source — есть фабрика
factory engineering

что такое фабрика, как функционирует? [a check mark at the end of the line]

    что на входе
    что на выходе
    какие этапы

[A loop drawn on the left: an arrow from the line "идея или баг" up to "triage" at the start of the chain below.]

    triage → spec → impl → verify →
        ship → monitor
    идея или баг

посмотреть на внутренние этапы

на входе джира + slack + метрики

triage: simple → cr `[?]`
        hard → spec (product / tech)

формальное доказательство

---

## Page 2

[Top: a struck-out word "Config" `[?]`, then underlined:] CP

config [brace over three lines]:
    skills + MCP
    model / harness
    env + permissions + memory `[?]`
arch [brace over two lines]:
    agent routing
    rules?
admin [brace over two lines]:
    cost, governance, billing
    evals, user management

[A long divider line.]

[Underlined:] DP
    artifacts, drive
    skills, memory, MCP, KB
    rules?

[A long divider line.]

эффективность = software shipped [underlined, a fraction bar]
                tokens + human time

[A long divider line.]

два уровня лупов
    внутренний выполняет работу
    внешний улучшает луп
