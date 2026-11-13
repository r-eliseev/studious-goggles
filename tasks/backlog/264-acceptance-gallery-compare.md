---
id: 264
status: approved
assignee:
claimed:
created: 2026-11-09
---

# Acceptance scenarios: gallery, viewer and comparison

## Description
Automate the “Галерея, просмотр и сравнение” section of the catalog: GAL-01 … GAL-05. File `Features/Gallery.feature`, tag `@gallery`.

Depends on: 253, 257 (run steps).
Environment: Linux or Windows (headless).

## Acceptance criteria
- Add GAL-01 … GAL-05 to `.feature`; each scenario passes or has `@ignore @bug-NNN` and a tracking task.
- GAL-01: two sessions, each with one completed and one failed card → the gallery (`SessionsPageModel.OpenGalleryAsync`, `GalleryPageModel.Items`) contains only the two completed images, newest first.
- GAL-02: filter by model (`Models`, `SelectedModel`) and period (`SelectedPeriod`, `ApplyFiltersAsync`); create “старые” images by advancing `FakeTimeProvider` between runs.
- GAL-03: `GalleryPageModel.OpenAsync` opens the source session (chat route with its id).
- GAL-04: test the viewer using Avalonia's real `IImageViewerService` or a fake, whichever is easier to verify headless (compare with `Services/OverlayImageViewerServiceTests.cs`); navigation stays within a run (`ImageViewerNavigationState`).
- GAL-05: comparison (`CompareAsync` on a card, `ComparisonPageModel`: `Mode`, `Zoom`, `SetZoom`, `SetOffset`) switches modes and applies the same zoom to both images.
- Update GAL-* statuses in the catalog.

## Analysis
- `ImageGen/ImageGen.Presentation/PageModels/GalleryPageModel.cs`, `GalleryItemViewModel.cs`, `GalleryModelFilter.cs`, `ComparisonPageModel.cs`, `ComparisonMode.cs`; `ImageGen/ImageGen.Presentation/Services/ImageViewerNavigationState.cs`, `IImageViewerService.cs`, `IImageComparisonService.cs`.
- Avalonia: `ImageGen/ImageGen.Avalonia.Tests/Services/OverlayImageViewerServiceTests.cs`, `OverlayImageComparisonServiceTests.cs`, `Views/GalleryViewTests.cs`.
- Specification: `docs/specs/image-client.md` F6.2, F6.3, F7.3, F7.4.

## Plan
Implement scenarios GAL-01 → GAL-05 one at a time.

Files: `ImageGen/ImageGen.Acceptance.Tests/Features/Gallery.feature`, `Steps/GallerySteps.cs` (new), `Steps/CommonSteps.cs`, `Support/FakePlatform.cs` (if the viewer is replaced), `docs/specs/acceptance-scenarios.md`.

### Verification
```
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj -- --filter-trait "Category=gallery"
dotnet format ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj --verify-no-changes
bash scripts/check.sh
```
(The tag filter applies if Reqnroll passes tags as xUnit categories; task 252 records how this works in `docs/knowledge/acceptance-tests.md`.)

### Execution order (for Luna)
- First read only the files in the “Files” line, the named tests, and sections of `docs/specs/acceptance-scenarios.md`; compare signatures with current `main`.
- Follow steps in order (ub-mode): add a scenario to `.feature`, run it and confirm it fails because the step is unbound or the expectation is unmet; add the minimal binding; get a passing scenario. One scenario per commit (include the scenario id in the commit message).
- Derive scenario expectations from the specification, not the code. If the code behaves differently, do not weaken the scenario or fix code: add `@ignore @bug-NNN`, create a task with reproduction steps using `scripts/new-task.sh --publish backlog <name>`, and set the catalog status to “расхождение (NNN)”.
- Steps act as a user: use page model commands, select items, and navigate. Do not call repositories, Core services, or SQL directly, except in “Given” steps that prepare the world and in “file exists” checks.
- Add shared steps to `Steps/CommonSteps.cs`; first check whether a step already exists with different wording.
- Do not change files outside the “Files” line, rename existing members beyond the plan, or fix neighboring findings; those belong to separate tasks.
- If the code differs from the plan (missing method, different signature), do not improvise; stop and write a question in “Comments”.
- Run `dotnet` commands one at a time, not in parallel. After each changed project, run `dotnet format <project> --verify-no-changes`.
- In the “Report”, list passing scenarios, ignored scenarios and their tracking tasks, commands and results, and differences from the plan. Update scenario statuses in `docs/specs/acceptance-scenarios.md`.

## Report

## Comments

### 2026-11-09, claude-bdd-plan
Created at the owner's request to plan application test scenarios for GPT Luna. Scenarios and expectations are in `docs/specs/acceptance-scenarios.md`. The plan awaits approval.

### 2026-11-09, owner (in the chat, recorded by claude-bdd-plan)
Plan approved, status `approved`. Changes of the same day apply: the Gherkin scenarios, step texts, bindings, comments and the "Report" are in English (`docs/conventions.md`, "Language"; ADR 0031 is accepted); a "Structure of a scenario" in this plan is a `Scenario Outline`. Scenario expectations and wording are in the English catalog `docs/specs/acceptance-scenarios.md`. The acceptance project runs in CI (task 252). The flow tests in `ImageGen.Avalonia.Tests/Flows` that a scenario of this task covers are deleted by task 267, not here.
