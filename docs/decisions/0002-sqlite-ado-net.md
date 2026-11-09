# 0002. SQLite through ADO.NET and our own migrations

Status: accepted
Allowed statuses: proposed, accepted, superseded by NNNN, rejected
Date: 2026-10-27

## Context
The result repository, by the owner's decision, is a local DB and files behind an interface in Core (on the server the same in a Docker volume). The storage is needed by MAUI (Android, iOS with trimming and AOT, Mac Catalyst, Windows) and Web. The sample project already runs on `Microsoft.Data.Sqlite`, but duplicates CRUD, reads columns by index, has no migrations and uses `DropTable`.

## Decision
A separate `ImageGen.Data` project on `Microsoft.Data.Sqlite` (ADO.NET) implements the interfaces from `Core/Results` and the catalog cache.
- A shared helper for commands with parameters and reading columns by name, not by index.
- Migrations: an ordered list of SQL scripts, the schema version in `PRAGMA user_version`, applied in a transaction at startup. Released migrations are not changed, only new ones are added.
- One connection per unit of work, writes in a transaction; WAL mode.
- Result files live in the folder from `IAppPaths`, the DB stores a relative path. The file is written first, then the DB record; orphaned files are removed by a check at startup.
- Deleting a picture does not delete the spending record (F7.7).

## Alternatives
- EF Core: migrations and LINQ out of the box, but heavier on mobile and with caveats on trimming and AOT on iOS.
- Dapper: less boilerplate, but mapping is reflection-based; on iOS with AOT it needs care.

## Consequences
Easier: no reflection, predictable SQL, the same code on all platforms and in Docker. Harder: mapping and migrations are written by hand and must be covered by tests (`ImageGen.Data.Tests` on a temporary DB).
