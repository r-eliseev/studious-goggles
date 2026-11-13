---
id: 236
status: approved
assignee:
claimed:
created: 2026-11-07
---

# Selected model disappeared from catalog — show it and allow removal

## Description
Owner's question from 2026-11-07: "If the selected model is unavailable, will it simply disappear from the list?" Current behavior: after a catalog refresh, the model row disappears from the session model screen (rows are built only from the catalog), but the model remains in session settings until the screen is saved, and a run with it produces a "Failed: model unavailable" card (`ErrorKind.ModelUnavailable`, `GenerationService`). No warning is shown.

Required: a selected model that disappeared from the catalog remains visible as "unavailable" and can be removed with one tap; a run does not attempt to use it.

Depends on: 235 (catalog refresh), 223 and 228 (audio and embedding model screens are also affected; do this after they are merged).
Environment: Linux (Presentation, Avalonia headless, MAUI Android build).

## Acceptance criteria
- `SessionModelsPageModel`: session settings models (image, video, audio) that are missing from the catalog appear at the top of their sections in separate selected rows marked "Unavailable — no longer offered by the provider" (resource), with a "Remove" button; saving without them removes them from the session.
- A session assistant model missing from the chat model catalog is handled the same way (an "unavailable" row; selecting a different model is required before the next message: sending shows an alert linking to the model screen).
- The default assistant model in Settings (`SelectedAssistantModel`, task 188) is already shown separately; add an "unavailable" marker if it is missing from the catalog.
- Embedding model: 227/228 already provide the `model_unavailable` status; verify it appears after catalog refresh (235).
- The model chip in the chat header (212) shows a warning icon and the count of unavailable models ("Models: 2 × 1 · 1 unavailable", resource format).
- `GenerationService`: when starting a run, skip models that are not in the catalog (do not create a generation); if none remain, throw `InvalidOperationException` with clear text, which the assistant/UI displays (instead of a Failed card for each model). If the catalog is unavailable (offline, empty cache), do not discard models (as now, the provider decides).
- Presentation tests (unavailable rows, Remove, save, chip) and Core tests (skip unavailable models, all unavailable → exception, empty catalog → do not discard).
- MAUI Android build; checks in tasks 140 and 046.

## Analysis
`ImageGen/ImageGen.Presentation/PageModels/SessionModelsPageModel.cs` (`LoadAsync`: catalog rows, `selected.Contains(...)`), `ChatPageModel.cs` (chip text `ModelsButtonText`, sending), `SettingsPageModel.cs` (default model), `ImageGen/ImageGen.Core/Services/GenerationService.cs` (around lines 513–580: catalog model check and `ModelUnavailable`), MAUI markup `SessionModelsPage.xaml`, `ChatPage.xaml`, Avalonia `SessionModelsView.axaml`, `ChatView.axaml`.

## Plan
1. Write failing Core and Presentation tests.
2. Skip unavailable models in `GenerationService`.
3. Add unavailable and Remove rows, the chip and assistant validation; add resources.
4. Update both shells' markup; build; check items 140 and 046.

Files: `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `ImageGen/ImageGen.Presentation/PageModels/SessionModelsPageModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/ChatPageModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/SettingsPageModel.cs`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.cs`, `ImageGen/ImageGen.Maui/Pages/SessionModelsPage.xaml`, `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, `ImageGen/ImageGen.Avalonia/Views/SessionModelsView.axaml`, `ImageGen/ImageGen.Avalonia/Views/ChatView.axaml`, Core, Presentation and Avalonia tests, `tasks/backlog/140-android-manual-check.md`, `tasks/backlog/046-desktop-manual-check.md`.

### Verification
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
bash scripts/check.sh
```

### Execution order (for Luna)
- First read only the files in the "Files" line and the named tests; compare signatures with current `main`.
- Follow the steps in order (ub-mode): failing test → run it and confirm it fails for the described reason → minimal fix → green test.
- Do not change files outside the "Files" line, rename existing members beyond the plan, or fix neighboring findings; those belong to separate tasks.
- If the code differs from the plan (missing method, different signature, test already exists), do not improvise; stop and write a question in "Comments".
- Run `dotnet` commands one at a time, not in parallel. After each changed project, run `dotnet format <project> --verify-no-changes`.
- In the "Report", record what changed, commands and results, and differences from the plan.

## Report

## Comments

### 2026-11-07, claude-audit
Created from the owner's question about missing models (verified in code: a model disappears from the list but remains in the session and results in Failed). Plan for Luna.

### 2026-11-07, owner (in chat, recorded by claude-audit)
Proposal approved ("Yes"); the plan was approved under the earlier instruction "approve all tasks that come up".
