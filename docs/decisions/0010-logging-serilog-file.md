# 0010. Logging: ILogger everywhere, a file through Serilog in Presentation

Status: accepted
Allowed statuses: proposed, accepted, superseded by NNNN, rejected
Date: 2026-10-28

Task: `tasks/backlog/045-detailed-logging.md`. The plan was approved by the owner in the chat on 2026-10-28 (Serilog).

## Context
The app is debugged first of all through logs (the owner's priority). Before: `ILogger` only in two core services, MAUI wrote to `Debug`, Avalonia only its own `LogToTrace`, `FireAndForgetSafeAsync` lost errors in Release, there was no log file, unhandled exceptions were not caught. Needed: one file per day in the data folder, 14 days of retention, a level without rebuilding, and a guarantee that keys and base64 pictures do not get into the log.

The core (`Core`) already references `Microsoft.Extensions.Logging.Abstractions`. `Microsoft.Extensions.Logging` cannot write to a file with rotation and retention, so a writing library is needed.

## Decision
- Wherever there is logging, we write through `ILogger<T>` with message templates. The core and `Presentation` do not know about the writing library, except in one place.
- The writing library is Serilog (`Serilog.Extensions.Logging` 10.0.0, `Serilog.Sinks.File` 7.0.0). It is plugged in only in `ImageGen/ImageGen.Presentation/Logging/` (`ImageGenLogging.AddImageGenFileLogging`). The heads call this method; `Core` and `Data` do not reference Serilog.
- File: `{DataDirectory}/logs/imagegen-yyyyMMdd.log`, a new day — a new file, files older than 14 days are deleted (`retainedFileTimeLimit`), at 10 MB — the next file of the same day.
- Level: the `IMAGEGEN_LOG_LEVEL` variable, then `logging.json` (`{"level": "..."}`) in the data folder, then the build's value (Debug in a Debug build, Information in Release). An unknown value is ignored and logged as a warning.
- The first line of every start: version, head, OS, architecture, runtime, data folder, log folder, level.
- The log contains no keys and no prompt text at the Information level. HTTP request and response bodies and the text of user and assistant messages are written only at Debug. Pictures in bodies are replaced by their size (`<base64, N bytes>`), the body is cut to 4 KB. HTTP headers (the key is there) are never written.
- Provider HTTP clients get `LoggingHttpHandler` through `ConfigureHttpClientDefaults` in `AddImageGenCore`.
- Unhandled exceptions: `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException` (through `CrashLog.Attach`), exceptions of the Avalonia UI thread (`Dispatcher.UIThread.UnhandledException`). `FireAndForgetSafeAsync` writes the error through `TaskExtensions.Logger`, which the head sets at startup.

## Alternatives
- **Our own `ILoggerProvider` without a library.** No dependencies, but size rotation, age retention and thread-safe writing would have to be written and tested by us. Rejected: the risk of errors in exactly what is needed for debugging.
- **Serilog in `Core`.** The core would depend on a specific writing library, and `ImageGen.Core.Tests` would not check that. Rejected: the core knows only `ILogger`.
- **Serilog in the heads (each configures it itself).** Duplication and diverging formats. Rejected: one method in `Presentation`.
- **`Microsoft.Extensions.Logging.Debug` and a file through the OS log.** Does not cover the "one file in the data folder" requirement. Rejected.

## Consequences
- `Presentation` now references Serilog. The `Presentation` architecture test forbids only UI frameworks, so it passes. Replacing the library affects one folder (`Presentation/Logging`) and two calls in the heads.
- Tests that create `ApiKeyService`, `ModelCatalogService`, `FileImageStore`, `SqliteDatabase` and `AvaloniaNavigationService` directly got a logger parameter (`NullLogger` or `CapturingLogger` in tests). Hosts must call `AddLogging()` if they use `AddImageGenCore`/`AddImageGenData` without logging.
- Internal Avalonia warnings and errors (`ILogSink`, `Logger.Sink`) go to the same file through `AvaloniaLogSink`; Avalonia messages at Information and below are dropped so as not to clutter the log. Messages raised before `App` starts go only to the trace.
- The log is written synchronously from the app's threads; with very large volumes of Debug logging it may become a bottleneck. Not measured yet.
