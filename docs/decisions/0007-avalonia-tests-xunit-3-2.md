# 0007. Avalonia tests on xunit v3 3.2.2

Status: accepted
Allowed statuses: proposed, accepted, superseded by NNNN, rejected
Date: 2026-10-28

Extends ADR 0004 (xUnit v3) and ADR 0006 (Avalonia). Concerns only `ImageGen.Avalonia.Tests`.

## Context
`Avalonia.Headless.XUnit` 12.1.4 (needed for `[AvaloniaFact]`, see ADR 0006 and task 039) is built against xunit v3 3.x. The central list `ImageGen/Directory.Packages.props` pins `xunit.v3` 4.0.2. Test discovery fails with `MissingMethodException` in `TestIntrospectionHelper.GetTestCaseDetails` (the tuple signature changed in 4.x), and the tests do not run. The other test projects (Core, Data, Presentation) work on 4.0.2 and need no change.

## Decision
In `ImageGen.Avalonia.Tests.csproj` the `xunit.v3` package is taken with `VersionOverride="3.2.2"` (the minimum version `Avalonia.Headless.XUnit` requires). The other projects stay on the version from the central list. The Avalonia tests were checked on this combination: 4 of 4 pass.

## Alternatives
- Move `xunit.v3` to 3.2.2 in the whole solution: one version, but it changes the pinned versions of projects that already work on 4.0.2.
- Do not use `Avalonia.Headless.XUnit` and start a headless app by hand in a test fixture: no version workaround, but more of our own code and a departure from the Avalonia documentation.
- Wait for an `Avalonia.Headless.XUnit` version built for xunit v3 4.x: postpones UI tests for an unknown time.

## Consequences
- Easier: UI tests work as in the Avalonia documentation (`[AvaloniaFact]`, `AvaloniaTestApplication`).
- Harder: the solution has two versions of xunit v3. When updating `Avalonia.Headless.XUnit` or `xunit.v3`, check whether `VersionOverride` can be removed and the shared version restored; after that this ADR is marked "superseded by NNNN" or rejected.
