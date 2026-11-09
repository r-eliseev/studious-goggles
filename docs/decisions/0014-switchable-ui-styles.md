# 0014. Three UI styles switched by a setting

Status: accepted
Allowed statuses: proposed, accepted, superseded by NNNN, rejected
Date: 2026-10-28

The owner's decision is item 1 of "Decision"; the rest was proposed by the executor of task 051 and approved by the owner together with the mockups on 2026-10-28 ("I like everything").

## Context
Task 051 proposed three design directions (`docs/design/directions.html`): A — a quiet tool, B — a gallery, C — an experiment journal. The owner (2026-10-28): "I like all three, they look quite similar in most places; let's do all three and switch them with a setting".

Constraints: two heads with different markup (MAUI and Avalonia, ADR 0006), the shared `ImageGen.Presentation` layer; a light and a dark theme (F10.1); every screen task is done in both heads. Every style that differs in markup multiplies the work on each screen, so the differences must be kept within narrow limits.

## Decision
1. **Three styles are a user setting.** "Style" (Quiet, Gallery, Notebook) and "Theme" (as in the system, light, dark) are two independent appearance settings, six combinations in total. Device settings, not session settings; stored in Preferences (not a secret). A change applies immediately, without a restart.
2. **A style is tokens plus a closed list of markup variants.** Everything the styles differ in is described in `docs/design/tokens.json` (W3C Design Tokens format 2025.10):
   - tokens: colors (per theme), fonts, radii, the size of body text on the desktop, the gap between cards;
   - markup variants (`$extensions.com.imagegen.variants`), exactly five: the look of a user message (`label`, `bubble`, `label-mono`), the run frame (`none`, `framed`), the look of the prompt (`italic`, `filled`, `mono`), the card (`bordered`, `borderless`), the data on the card (`full`, `short-overlay`).
   A new markup variant is added only through a new ADR or an edit of this one: otherwise the styles drift apart and every screen has to be laid out three times.
3. **Shared by all styles:** screen layouts, the order and set of elements, texts, icons, touch target sizes, the font scale per platform (`core` in `tokens.json`), status semantics (filtered — amber, not red), accessibility rules.
4. **The mechanism in the heads (a proposal, checked by proof-of-concept code in the shared theme resources task):** six resource dictionaries (style × theme) with the same keys, connected through `DynamicResource`; when the setting or the system theme changes, the app swaps the dictionary in `MergedDictionaries`. Markup variants are value resources read by element styles (Avalonia — classes on the root element and selectors; MAUI — a binding to a resource or a `DataTemplateSelector`). The dictionaries are generated from `tokens.json` or written by hand from it — decided in the same task.
5. **Fonts:** Onest (B), IBM Plex Sans and IBM Plex Mono (C) — OFL license, with Cyrillic; shipped in both heads. A uses the system font; on Linux, where there is no single system font, the fallback font is chosen in the theme resources task.
6. **The default style is A** (the executor's assumption: the most native on all platforms; the owner may choose another).

## Alternatives
- **Choose one style:** less work, but the owner wants all three.
- **Free markup differences between styles:** more expressive styles, but every screen is laid out three times in two heads, tests and manual checks multiply.
- **Only color themes (without fonts and markup variants):** cheap, but styles B and C lose what distinguishes them (bubbles, data over the picture, monospace data).

## Consequences
- A settings screen is needed (now there is only the key page): key, style, theme; later the language (task 028).
- Every mockup and every screen task is checked in three styles and two themes; the prototype in `docs/design/` switches them the same way as the app.
- Three fonts increase the build size (on the order of hundreds of kilobytes per face; the exact size is found during layout).
- Contrast is checked for each of the six palettes (WCAG AA): text 4.5:1, field borders 3:1 (the `control` token).
- Whether MAUI switches all dictionaries without a restart on all platforms (Windows, Android, iOS, Mac Catalyst) is unknown: check with proof-of-concept code.
