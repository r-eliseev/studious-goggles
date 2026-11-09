# 0018. CommunityToolkit.Maui in the MAUI head

Status: accepted
Allowed statuses: proposed, accepted, superseded by NNNN, rejected
Date: 2026-10-29

## Context
The `CommunityToolkit.Maui` package has been referenced in `ImageGen.Maui` since the start of the skeleton (task 011, 2026-10-27; the version is in `ImageGen/Directory.Packages.props`), but no decision about it was recorded, although `docs/conventions.md` ("Dependencies") requires an ADR for every new package. The audit of 2026-10-29 noted it. From the package we use `IsNotNullConverter` and `TouchBehavior` (long press and tap on a card); registration is `UseMauiCommunityToolkit()` in `MauiProgram`.

## Decision
The owner (2026-10-29): keep `CommunityToolkit.Maui` (MIT license, a .NET Foundation project) and record this decision retroactively. The package is used only in `ImageGen.Maui`; shared code (`Core`, `Presentation`) does not reference it.

## Alternatives
- Our own converter and gesture: a little code, but writing `TouchBehavior` (long press on all MAUI platforms) again is expensive.
- Remove the package: would require rewriting the card long press (tasks 069, 076).

## Consequences
- Package updates go together with MAUI versions (an incompatibility shows up as error MCT001 without `UseMauiCommunityToolkit()`, `docs/knowledge/maui.md`).
- New elements from the package may be used without a separate ADR while the package stays in one head.
