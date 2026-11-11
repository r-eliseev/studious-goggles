---
id: 277
status: todo
assignee:
claimed:
created: 2026-11-11
---

# Avoid blocking disposal in the acceptance AppWorld restart test

## Описание
The complete acceptance test suite hangs in `ImageGen/ImageGen.Acceptance.Tests/Support/AppWorldTests.cs`, in `RestartAsync_ReusesDataFolderAndSecretStore` (line 11). The test constructs `AppWorld` inside `HeadlessApp.RunAsync` and declares it with `using var`; after the callback returns, synchronous `AppWorld.Dispose()` calls `DisposeAsync().GetAwaiter().GetResult()` while still on the Avalonia UI dispatcher. Running this test alone with `dotnet ImageGen/ImageGen.Acceptance.Tests/bin/Debug/net10.0/ImageGen.Acceptance.Tests.dll -method '*AppWorldTests.RestartAsync_ReusesDataFolderAndSecretStore' -reporter verbose` starts the test and does not finish. The same hang occurs in the full acceptance suite. The task 257 scenarios pass independently (11/11); task 257's plan does not include `Support/AppWorldTests.cs`.

## Критерии готовности

## Анализ

## План

## Отчёт

## Review

## Комментарии

### 2026-11-11, codex-gpt6 — Proposed plan
Update `AppWorldTests.RestartAsync_ReusesDataFolderAndSecretStore` to await asynchronous AppWorld disposal instead of blocking the UI dispatcher. Add a regression assertion or test structure proving the test completes after restart and cleanup. Run the isolated test, the full acceptance suite, `dotnet format ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj --verify-no-changes`, and `bash scripts/check.sh`.
