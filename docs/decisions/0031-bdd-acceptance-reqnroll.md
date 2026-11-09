# 0031. Acceptance scenarios in Gherkin (Reqnroll) over headless Avalonia

Status: accepted (2026-11-09)
Allowed statuses: proposed, accepted, superseded by NNNN, rejected
Date: 2026-11-09

## Context
On 2026-11-09 the owner asked to adopt "something like Cucumber" and to plan the app's test scenarios so that another agent (GPT Luna) carries them out.

How the UI and flows are checked now:
- unit tests of the core, storage and page models — xUnit v3 (ADR 0004), about 1400 tests;
- flows through the real composition — `ImageGen.Avalonia.Tests/Flows/*FlowTests.cs` on `ShellHarness` (headless Avalonia, a temporary data folder, keys in memory, OpenRouter from the `OpenRouterFakes` stub);
- UI tests of the built MAUI app — NUnit + Allure, Appium and FlaUI (ADR 0029), so far only the first-launch screen.

The user scenarios (the specifications `chat-sessions.md`, `image-client.md`, `media-generation.md`) are not collected anywhere in one readable form. The flow tests are written as code, the owner cannot read them, and they do not show which specification requirements are covered.

Facts checked on 2026-11-09 on nuget.org:
- `Reqnroll` 3.3.4 (2026-03-23) is the open successor of SpecFlow, Gherkin and Cucumber Expressions for .NET; there are `Reqnroll.xunit.v3` 3.3.4 and `Reqnroll.NUnit` 3.3.4.
- `Reqnroll.xunit.v3` 3.3.4 depends on `xunit.v3.extensibility.core` >= 2.0.0. The solution has `xunit.v3` 4.0.2, and `Avalonia.Headless.XUnit` 12.1.4 fails at test discovery with xunit.v3 4.x, so `ImageGen.Avalonia.Tests` is pinned to 3.2.2 (`VersionOverride`, ADR 0007). Whether `Reqnroll.xunit.v3` works with 4.0.2 is unknown.
- `Allure.Reqnroll` 2.15.0 is stable (for the NUnit run of E2E).
- The app's composition lets only the OpenRouter network be replaced (`AppServices.Create(openRouterHandler: …)`); the other `HttpClient`s (fal, Together, custom providers) cannot be replaced from outside.
- `TimeProvider` and `ISeedSource` are registered in the core (`CoreServiceCollectionExtensions`) and can be replaced.

Owner's decisions of 2026-11-09: the plan is approved; the scenarios run in CI; "we are essentially replacing the current e2e tests with these, the current tests must be migrated to the new framework"; everything in the repository, including the scenarios, is in English.

## Decision
1. **The tool is Reqnroll** with the xUnit v3 runner (`Reqnroll.xunit.v3`) for the headless project, so that no third test framework is introduced (ADR 0004). If the first step of task 252 shows that xUnit v3 with Reqnroll and Avalonia headless works neither on 4.0.2 nor on 3.2.2, the task stops with a question to the owner (the fallback is `Reqnroll.NUnit`, as in E2E).
2. **A new project `ImageGen/ImageGen.Acceptance.Tests`.** Scenarios are `.feature` files in English Gherkin (the default language, `Feature`, `Scenario`, `Given`, `When`, `Then`). UI texts in steps are as in `AppResources`. Step bindings and code are in English per `docs/conventions.md`.
3. **The level is the real app without the outside world:** headless Avalonia (`HeadlessUnitTestSession`), the real composition `AppServices.Create`, a temporary data folder, keys in memory, the whole network through one scenario fake. Steps act through page models and navigation, like a user (invoke a command, pick an item), and check what is visible on the screen: texts and states of page models and, where cheap, the control tree. The core and storage are not substituted.
4. **The network is forbidden.** `AppServices.Create` gets an optional parameter for test registrations (a test seam only; the app does not pass it). Any request to an unknown address fails the scenario.
5. **The scenario catalog is `docs/specs/acceptance-scenarios.md`.** Every scenario has a permanent id (`KEY-01`), a link to a specification requirement and a status. A scenario's tags are its id and the requirement number (`@KEY-01 @F1.2`).
6. **Discrepancies found are not fixed in a scenario task.** The scenario is tagged `@ignore @bug-NNN`, the discrepancy becomes a new task (`docs/tasks.md`, "New tasks").
7. **The flow tests `ImageGen.Avalonia.Tests/Flows` are replaced by scenarios** (task 267): each flow test is mapped to a catalog scenario, missing scenarios are added to the catalog, and the flow test is deleted once its scenario is green. View and service tests in `ImageGen.Avalonia.Tests` stay: they are unit tests of controls, not user flows.
8. **E2E.** The existing UI tests of the built apps (`ImageGen.E2E.Android`, `ImageGen.E2E.Windows`, later iOS) are migrated to Reqnroll: `Reqnroll.NUnit` + `Allure.Reqnroll`, the same `.feature` files with the `@e2e` tag, step bindings on top of the existing Appium/FlaUI fixtures, videos and evidence (task 266). The Appium/FlaUI runs themselves stay (the owner's sentence "we are replacing the current e2e tests" is read as replacing the test framework, not dropping the built-app runs; open question in `docs/status.md`).
9. **CI.** The acceptance project runs in the `avalonia` job of `.github/workflows/build.yml` (the owner allowed this change on 2026-11-09).

## Alternatives
- **Gherkin over `ImageGen.Presentation` without Avalonia.** Faster and no headless needed, but the app's composition lives in `ImageGen.Avalonia.Composition` and would have to be repeated in the tests: the test would check a different dependency graph than the app has.
- **Gherkin only in E2E (Appium/FlaUI).** Closest to the user, but without the provider mock (243) one cannot get past the key screen, a run takes minutes per scenario and only in CI.
- **LightBDD or xBehave.** Scenarios are written as C# code, which the owner cannot read any easier than the current flow tests.
- **Russian Gherkin.** Considered in the first draft; rejected by the owner's decision that everything is in English.

## Consequences
- The solution gets one more test project and the Reqnroll packages; `docs/conventions.md` and `AGENTS.md` name the project and the run command.
- `AppServices.Create` gets a test seam for registrations.
- Headless Avalonia scenarios do not check the MAUI markup: it is still checked by `Presentation.Tests/Design`, E2E and the manual tasks 016, 046, 140.
- The E2E projects get a second NUnit-based layer (Reqnroll); the step bindings for E2E and headless are separate classes over the same step texts.
