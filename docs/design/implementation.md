# Design: a reference for layout work

Platform guidelines and the ImageGen design system. Based on the platform research and the design system of 2026-10-28 (both are in this file); when they differ from the current code and specification, check against those. The `tokens.json` tokens and the mockups were the source material for choosing the directions and are not a markup specification.

## Platform guidelines

### Layout and width

- Phone: one column, the main action within reach of the thumb; do not carry a dense desktop panel over literally.
- Tablet and desktop window: use the available width, but limit long lines and center forms. On a wide screen it is appropriate to show the session list and the chat at the same time.
- In a narrow window elements must rearrange instead of creating horizontal scrolling.

### Input and accessibility

- iOS: Dynamic Type, the system keyboard and the back gesture; interactive targets around 44 pt.
- Android: system Back, the keyboard must not cover the input; interactive targets around 48 dp.
- Windows and Linux desktop: keyboard navigation and mouse; interactive targets 32 px (minimum 24 px) with enough spacing.
- Text contrast — WCAG AA: 4.5:1 for ordinary text, 3:1 for large text and control borders.
- Check accessibility with a screen reader; every button has a meaningful name, decorative icons are hidden from the screen reader.

### System conventions

| What | iOS / iPadOS | Android | macOS | Windows | Linux |
|---|---|---|---|---|---|
| Back | top bar and an edge swipe | system button or gesture | ⌘[ | Alt+← | as Windows |
| Main action | top bar | top bar or FAB | window toolbar, ⌘N | window toolbar, Ctrl+N | as Windows |
| Settings | an app screen | an app screen | a separate window, ⌘, | a screen or a window | as Windows |
| List actions | swipe, long press | long press | right click | right click | right click |
| Dialogs | the main button on the right | the main one on the right | the main one on the right | the main one on the left | by the environment, usually as Windows |

These are general guidelines, not a claim that they were checked on each OS. The Apple HIG could not be reconciled with the primary sources in the research; clarify when a platform environment is available.

## Design system

The initial research of 2026-10-28 proposed three close directions: A — a calm tool, B — a gallery, C — an experiment journal. The owner decided to include all three as switchable styles (ADR 0014). The original HTML mockups and snapshots were deleted; the current tokens stay in `tokens.json`, and the implemented behavior is defined by the heads and the tests.

## 1. Appearance settings
- Style: `Quiet` (A), `Gallery` (B), `Notebook` (C). `Quiet` by default.
- Theme: `System`, `Light`, `Dark`. `System` by default.
- The file `appearance.json` in the data folder: `{"style":"quiet","theme":"system"}` (lowercase values). An unknown value or a corrupted file → the default value and a warning in the log.

## 2. Resource keys (the same in both heads)
The prefix is `Ig`. Resources are connected through `DynamicResource`, otherwise a change of style and theme does not apply without a restart.

### 2.1 Colors
For each `color.<theme>.<name>` token there are two resources: `Ig<Name>Color` (type `Color`) and `Ig<Name>Brush` (`SolidColorBrush`). `<Name>` is the token name with a capital letter.

| Token | Keys |
|---|---|
| bg | `IgBgColor`, `IgBgBrush` |
| surface | `IgSurfaceColor`, `IgSurfaceBrush` |
| sunken | `IgSunkenColor`, `IgSunkenBrush` |
| fg | `IgFgColor`, `IgFgBrush` |
| muted | `IgMutedColor`, `IgMutedBrush` |
| line | `IgLineColor`, `IgLineBrush` |
| control | `IgControlColor`, `IgControlBrush` |
| accent | `IgAccentColor`, `IgAccentBrush` |
| onAccent | `IgOnAccentColor`, `IgOnAccentBrush` |
| accentSoft | `IgAccentSoftColor`, `IgAccentSoftBrush` |
| ok | `IgOkColor`, `IgOkBrush` |
| err | `IgErrColor`, `IgErrBrush` |
| errSoft | `IgErrSoftColor`, `IgErrSoftBrush` |
| warn | `IgWarnColor`, `IgWarnBrush` |
| warnSoft | `IgWarnSoftColor`, `IgWarnSoftBrush` |
| placeholder | `IgPlaceholderColor`, `IgPlaceholderBrush` |

The values are the `$value.hex` field in `tokens.json` (`style.<a|b|c>.color.<light|dark>.<name>`). Style `a` = Quiet, `b` = Gallery, `c` = Notebook.

### 2.2 Style sizes and fonts

| Key | Type (Avalonia / MAUI) | Quiet | Gallery | Notebook |
|---|---|---|---|---|
| `IgRadiusSm` | `CornerRadius` / `CornerRadius` | 6 | 12 | 3 |
| `IgRadiusMd` | the same | 8 | 18 | 4 |
| `IgRadiusLg` | the same | 12 | 24 | 6 |
| `IgRadiusControl` | the same | 6 | 999 | 3 |
| `IgCardGap` | `double` / `x:Double` | 12 | 16 | 10 |
| `IgFontText` | `FontFamily` / `x:String` (alias) | system (see 4) | Onest | IBM Plex Sans |
| `IgFontMeta` | the same | system | Onest | IBM Plex Mono |

### 2.3 Font scale and element height (per platform, shared by the styles)

| Key | Desktop (Avalonia; MAUI WinUI and MacCatalyst) | iOS | Android |
|---|---|---|---|
| `IgFontSizeCaption` | 12 | 12 | 12 |
| `IgFontSizeMeta` | 12 | 13 | 13 |
| `IgFontSizeBody` | 14 (Gallery — 15) | 17 | 16 |
| `IgFontSizeTitle` | 15 | 17 | 16 |
| `IgFontSizeHeading` | 20 | 22 | 22 |
| `IgFontSizeDisplay` | 24 | 28 | 28 |
| `IgControlHeight` | 32 | 44 | 48 |

`IgFontSizeBody` on the desktop depends on the style (`style.*.size.bodyDesktop`), so it lives in the style dictionary; the others are in the shared dictionary. In MAUI the per-platform values are set through `OnPlatform`.

### 2.4 Markup variants as resources
The five variants (`$extensions.com.imagegen.variants`) are expressed as resource values: the screen markup is one, it reads these keys. There are no classes or branches by style in the markup.

| Key | Type (Avalonia / MAUI) | Quiet | Gallery | Notebook |
|---|---|---|---|---|
| `IgUserMessageBackgroundBrush` | `IBrush` / `Brush` | `Transparent` | = `IgSunkenBrush` | `Transparent` |
| `IgUserMessageAlignment` | `HorizontalAlignment` / `LayoutOptions` | `Left` / `Start` | `Right` / `End` | `Left` / `Start` |
| `IgUserMessagePadding` | `Thickness` | 0 | 14,10 | 0 |
| `IgUserMessageCornerRadius` | `CornerRadius` | 0 | 18,18,6,18 | 0 |
| `IgUserMessageRoleVisible` | `bool` / `x:Boolean` | True | False | True |
| `IgRoleFont` | as `IgFontText` | = `IgFontText` | = `IgFontText` | = `IgFontMeta` |
| `IgRunBorderThickness` | `Thickness` | 0 | 0 | 1 |
| `IgRunPadding` | `Thickness` | 0 | 0 | 12 |
| `IgPromptBackgroundBrush` | `IBrush` / `Brush` | `Transparent` | = `IgSunkenBrush` | = `IgSunkenBrush` |
| `IgPromptForegroundBrush` | the same | = `IgMutedBrush` | = `IgFgBrush` | = `IgFgBrush` |
| `IgPromptPadding` | `Thickness` | 0 | 10,8 | 10,8 |
| `IgPromptFontStyle` | `FontStyle` / `FontAttributes` | `Italic` | `Normal` / `None` | `Normal` / `None` |
| `IgPromptFont` | as `IgFontText` | = `IgFontText` | = `IgFontText` | = `IgFontMeta` |
| `IgCardBorderThickness` | `Thickness` / `x:Double` | 1 | 0 | 1 |
| `IgCardBackgroundBrush` | `IBrush` / `Brush` | = `IgSurfaceBrush` | `Transparent` | = `IgSurfaceBrush` |
| `IgCardImageCornerRadius` | `CornerRadius` | 0 | = `IgRadiusMd` (18) | 0 |
| `IgCardShowFullMeta` | `bool` / `x:Boolean` | True | False | True |
| `IgCardShowShortMeta` | `bool` / `x:Boolean` | False | True | False |
| `IgCardShowOverlay` | `bool` / `x:Boolean` | False | True | False |

`IgCardShowShortMeta` is the inverse of `IgCardShowFullMeta`: in Gallery there is a short block under the picture (model and price), in Quiet and Notebook it is hidden in favor of the full block.

"= Key" means the same value as the named key in the same dictionary (the color depends on the theme). In Notebook the author label in the mockup is lowercase; the heads do not change the case (text elements have no such property), only the font changes.

## 3. Fonts
TTF files, the OFL 1.1 license (the `OFL.txt` file is put next to the fonts). The source is the Google Fonts repository; it has variable fonts, the static faces are cut with the `fonttools` tool (Python), see the task on the appearance resources.

| File | Family (the name inside the file) | Where from | Axis |
|---|---|---|---|
| `Onest-Regular.ttf` | Onest | `https://raw.githubusercontent.com/google/fonts/main/ofl/onest/Onest%5Bwght%5D.ttf` | `wght=400` |
| `Onest-Medium.ttf` | Onest Medium | the same | `wght=500` |
| `Onest-SemiBold.ttf` | Onest SemiBold | the same | `wght=600` |
| `IBMPlexSans-Regular.ttf` | IBM Plex Sans | `https://raw.githubusercontent.com/google/fonts/main/ofl/ibmplexsans/IBMPlexSans%5Bwdth,wght%5D.ttf` | `wght=400 wdth=100` |
| `IBMPlexSans-Medium.ttf` | IBM Plex Sans Medium | the same | `wght=500 wdth=100` |
| `IBMPlexSans-SemiBold.ttf` | IBM Plex Sans SemiBold | the same | `wght=600 wdth=100` |
| `IBMPlexMono-Regular.ttf` | IBM Plex Mono | `https://raw.githubusercontent.com/google/fonts/main/ofl/ibmplexmono/IBMPlexMono-Regular.ttf` | already static |
| `IBMPlexMono-Medium.ttf` | IBM Plex Mono Medium | `https://raw.githubusercontent.com/google/fonts/main/ofl/ibmplexmono/IBMPlexMono-Medium.ttf` | already static |

Licenses: `https://raw.githubusercontent.com/google/fonts/main/ofl/onest/OFL.txt`, `.../ofl/ibmplexsans/OFL.txt`, `.../ofl/ibmplexmono/OFL.txt`. Checked 2026-10-28: all addresses answer 200; `fonttools varLib.instancer --update-name-table` gives the right face names.

MAUI aliases (`ConfigureFonts`): `OnestRegular`, `OnestMedium`, `OnestSemiBold`, `PlexSansRegular`, `PlexSansMedium`, `PlexSansSemiBold`, `PlexMonoRegular`, `PlexMonoMedium`, `LucideIcons`.

## 4. The system font (Quiet)
- Avalonia: the `IgFontText` resource = `FontFamily` `$Default` (the default system font on each OS; on Linux whatever fontconfig gives). We do not add new font packages (a new package requires an ADR, `docs/conventions.md`).
- MAUI: the platform's system font (the `FontFamily` property is not set, the `IgFontText` resource = an empty string).

## 5. Icons
The Lucide font (`lucide-static@1.56.0`, the ISC license, the file `package/font/lucide.ttf` from `https://registry.npmjs.org/lucide-static/-/lucide-static-1.56.0.tgz`; the license is `package/LICENSE`). An icon is a text element with the `LucideIcons` font and a character from the table. The color is that of the text. An icon next to a label is hidden from the screen reader; a button of a single icon gets an accessible name.

| Constant name (`IconGlyphs`) | Lucide | Code |
|---|---|---|
| `Plus` | plus | U+E13D |
| `Search` | search | U+E151 |
| `Send` | arrow-up | U+E04A |
| `Attach` | paperclip | U+E12D |
| `Parameters` | sliders-horizontal | U+E29A |
| `Models` | layers | U+E529 |
| `More` | ellipsis | U+E0B6 |
| `Repeat` | repeat | U+E146 |
| `NewSeed` | dices | U+E2C5 |
| `Edit` | pencil | U+E1F9 |
| `Describe` | eye | U+E0BA |
| `PromptFromImage` | type | U+E198 |
| `Copy` | copy | U+E09E |
| `Save` | download | U+E0B2 |
| `Delete` | trash-2 | U+E18E |
| `Key` | key-round | U+E4A3 |
| `Settings` | settings | U+E154 |
| `Queued` | clock | U+E087 |
| `Error` | circle-alert | U+E077 |
| `Filtered` | shield-minus | U+E518 |
| `Refresh` | refresh-cw | U+E145 |
| `Image` | image | U+E0F6 |
| `BackIos` | chevron-left | U+E06E |
| `Forward` | chevron-right | U+E06F |
| `Expand` | chevron-down | U+E06D |
| `Back` | arrow-left | U+E048 |
| `Close` | x | U+E1B2 |
| `Check` | check | U+E06C |
| `Info` | info | U+E0F9 |
| `Minus` | minus | U+E11C |

## 6. Window width
- A wide screen — window width ≥ 840: the session list on the left (272), the chat on the right; models and parameters are a third column (360).
- A narrow one — < 840: one screen at a time, as now.
- The chat header: the title in one line with an ellipsis, the chips wrap to the next line; on a narrow MAUI screen the title in the header is hidden — the navigation bar shows it (task 098). In Avalonia the title always stays.

## 7. Button order in dialogs
- Windows and Linux: the main button first (on the left), "Cancel" after it.
- macOS: "Cancel" on the left, the main one on the right.
- iOS and Android in MAUI: system dialogs (`DisplayAlert`, `DisplayActionSheet`, `DisplayPromptAsync`) — the platform sets the order.

## 8. Conformance check
The values in the heads' dictionaries must match `tokens.json`. The theme tasks add a test that reads `docs/design/tokens.json` and compares it with the loaded resources (Avalonia — a headless test; MAUI — a test on the parsed XAML of the dictionaries, see the task).


## The original platform research (2026-10-28)

Collecting guidelines for the design system and the mockups (task 051). Only what the mockups rely on is here; the tokens and mockups themselves are next to it in `docs/design/`.

Marks: "checked" — taken from the primary source's page; "by retellings" — the primary source did not open, the numbers were taken from copies and retellings, reconcile with the primary source before approving the design system.

## Principle
What is shared by all platforms is the tokens: colors, the font scale, spacing, radii, the look of cards. What each platform has of its own is navigation, menus, keyboard shortcuts, the button order in dialogs, gestures. The same pixels everywhere is not the goal: otherwise the app looks foreign on iOS and macOS.

## Layout by window width (Material 3, checked)
- Width classes: compact < 600 dp, medium 600–839 dp, expanded ≥ 840 dp (there are also large and extra-large from 1200 dp).
- Typical layouts and our screens:
  - **list-detail** — the session list and the chat. Side by side in a wide window; on compact and medium one at a time, "Back" returns to the list. When the window narrows, the open chat stays, the list hides.
  - **supporting pane** — the chat as the main one, the session models and parameters in a second pane. On a phone the pane becomes a separate screen or a bottom sheet.
  - **feed** — a grid of cards: the future gallery (task 022) and comparison (023).
- iPad (by HIG retellings): horizontal classes compact and regular; in Split View at ⅓ of the screen the app gets compact. This matches the Material 3 classes, no separate iPad layout is needed.

## Adaptation techniques and spacing (Fluent 2, checked)
- Techniques: reposition, resize, reflow into columns, show/hide details, re-architect (split the screen). The mockup of each screen records which technique is used at which width class.
- The grid step is 4; the spacing row: 0, 2, 4, 6, 8, 10, 12, 16, 20, 24, 28, 32, 36, 40, 48, 52, 56. The values 2, 6, 10 are for aligning icons.
- Fluent breakpoints (for comparison, we take Material 3): 320, 480, 640, 1024, 1366, 1920.
- Fluent touch size: iOS and Web 44×44, Android 48×48.

## The input method matters more than the width
- A wide window does not mean a mouse: tablets, Windows touch screens. Density and element size depend on the input method, the layout on the width.
- Everything that appears on hover (actions on a card) is reachable another way on a touch screen: a visible "…" button or a long press.
- On the web this is `pointer: coarse | fine` and `hover` (web.dev, checked). MAUI and Avalonia have no direct analog: we decide by platform (Android, iOS — touch; Windows, macOS, Linux — mouse). How to recognize a Windows touch screen in Avalonia and MAUI is unknown; check during layout work.

## Touch size and contrast
- WCAG 2.2: 2.5.8 (AA) — at least 24×24 CSS px or enough spacing; 2.5.5 (AAA) — 44×44 (checked through Smashing Magazine and Adrian Roselli).
- Apple, the table from HIG Accessibility (by retellings): iOS and iPadOS — 44×44 pt by default, minimum 28×28; macOS — 28×28 pt by default, minimum 20×20.
- Android (Material): 48×48 dp.
- Text contrast — not lower than WCAG AA: 4.5:1 for ordinary text, 3:1 for large text and control borders.
- The owner's decision (2026-10-28, approving the 051 mockups): 44 pt on iOS, 48 dp on Android, 32 on the desktop with a mouse (not less than 24) — `core.target` in `tokens.json`.

## Platform conventions
The source for Apple is HIG retellings (the developer.apple.com pages did not open, reconcile); for Android — Material 3; for Windows — Fluent 2 and general Windows conventions.

| What | iOS / iPadOS | Android | macOS | Windows | Linux |
|---|---|---|---|---|---|
| Back | a button in the top bar and a swipe from the left edge; no system button | system button or gesture | history in the window, ⌘[ | history in the window, Alt+← | as Windows |
| Main action ("New session") | in the top bar; no FAB | in the top bar or a FAB | window toolbar, ⌘N | window toolbar, Ctrl+N | as Windows |
| Settings and key | a screen inside the app | a screen inside the app | a separate window, ⌘, | a screen or a window | as Windows |
| Menus | context menu (long press) | context menu | the menu bar is mandatory: File, Edit, Window | context menu, a window menu optional | as Windows |
| Actions on a list row | a swipe on the row, long press | long press | right click | right click | right click |
| Button order in a dialog | the main one on the right, "Cancel" on the left | the main one on the right | the main one on the right | the main one on the left ("OK", "Cancel") | depends on the environment, by default as Windows |
| Bottom sheet | yes, with an intermediate height, closed by a swipe | yes | a sheet attached to the window | a dialog | a dialog |
| Window buttons | — | — | top left, do not occupy the top left | top right | top right (depends on the environment) |
| Density | body text about 17 pt | body text about 14–16 sp | body text about 13 pt | body text about 14 px | as Windows |
| System font size | Dynamic Type is mandatory | the system font scale | no | the system scale | no |
| System font | SF Pro | Roboto | SF Pro | Segoe UI Variable | none single (Cantarell, Ubuntu, Noto Sans) — a fallback is needed |
| Safe areas | the notch on top, the home indicator at the bottom | cutouts and the gesture bar | — | — | — |

Technically: the menu bar is `NativeMenu` in Avalonia, `MenuBarItem` in MAUI (Mac Catalyst); keyboard shortcuts are described once per action, the key is per platform (⌘ on Apple, Ctrl on the others).

## Icons
- SF Symbols by their license are used only in apps for Apple platforms (from memory, reconcile the license) — they do not suit us.
- Candidates with a free license: Lucide (ISC), Material Symbols (Apache 2.0), Fluent System Icons (MIT). Lucide and Material Symbols look more neutral on iOS than Fluent.

## Fonts
- The "system font" option: works best with Dynamic Type and looks native; Linux needs a bundled fallback font.
- The "our own font" option (one for all platforms): needs the OFL license and Cyrillic (a Russian UI was planned later); on iOS scaling with Dynamic Type is set up separately.

## Liquid Glass (iOS 26, macOS 26)
Apple's new visual language (2025). Native MAUI elements on iOS probably adopt it by themselves (not checked). Faking it with our own drawing is not worth it: on other platforms it looks foreign.

## Generative UI patterns
- **Variations** (Shape of AI): several variants with a shared seed as a grid of thumbnails — this is our "N runs per model".
- **Regenerate** next to the result; the original result is not replaced without confirmation (Cloudscape) — matches the rule "the result is never lost".
- **A regeneration carousel**: paging through the variants of one run (AI UX Playground) — a candidate for viewing a picture on a phone.

## Tokens
The W3C Design Tokens Community Group format, the stable version 2025.10 (checked; this is a Community Group report, not a W3C standard). A candidate for keeping the tokens in `docs/design/` as one file from which the `ResourceDictionary` of both heads is produced. Whether a generator is needed or the tokens are carried over by hand is decided in the shared theme resources task.

## Sources
- Android: [Canonical layouts](https://developer.android.com/develop/ui/compose/layouts/adaptive/canonical-layouts), [Design for large screens](https://developer.android.com/design/ui/large-screens)
- [Fluent 2: Layout](https://fluent2.microsoft.design/layout)
- Apple HIG: [Layout](https://developer.apple.com/design/human-interface-guidelines/layout), [Designing for iOS](https://developer.apple.com/design/human-interface-guidelines/designing-for-ios), [Designing for macOS](https://developer.apple.com/design/human-interface-guidelines/designing-for-macos), [Accessibility](https://developer.apple.com/design/human-interface-guidelines/accessibility) — did not open; [the Apple forum on macOS button size](https://developer.apple.com/forums/thread/739201)
- [Smashing Magazine: the minimum element size per WCAG](https://smashingmagazine.com/2024/07/getting-bottom-minimum-wcag-conformant-interactive-element-size/), [Adrian Roselli: Target Size and 2.5.5](https://adrianroselli.com/2019/06/target-size-and-2-5-5.html)
- [web.dev: Interaction](https://web.dev/learn/design/interaction)
- [Shape of AI: Variations](https://www.shapeof.ai/patterns/variations), [Cloudscape: Response regeneration](https://cloudscape.design/gen-ai/patterns/response-regeneration/), [AI UX Playground: Regeneration carousel](https://aiuxplayground.com/pattern/regen-carousel)
- [Design Tokens Format Module 2025.10](https://www.w3.org/community/reports/design-tokens/CG-FINAL-format-20251028/)
