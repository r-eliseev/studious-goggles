---
id: 003
status: done
assignee:
claimed:
created: 2026-10-27
---

# Хранилище: проект ImageGen.Data, база и схема

## Описание
Создать проект `ImageGen.Data` с классом `SqliteDatabase`: путь к файлу БД, открытие соединения, применение миграций через `PRAGMA user_version` (ADR 0002). Первая миграция создаёт все таблицы скелета. Создать тестовый проект `ImageGen.Data.Tests` и добавить его в CI.

Зависит от: 002.
Среда: Linux или Windows (нужен только .NET 10 SDK, MAUI-нагрузки не нужны).

## Критерии готовности
- Проекты `ImageGen.Data` и `ImageGen.Data.Tests` есть в `ImageGen/ImageGen.slnx`.
- Схема в `Migrations.cs` совпадает с планом дословно.
- Тесты из плана проходят.
- CI запускает тесты `ImageGen.Data.Tests` (шаг 6).

## Анализ
- Значения пишутся в БД так: `Guid` → `ToString("D")`; `DateTimeOffset` → `ToString("O", CultureInfo.InvariantCulture)`; `decimal` → `ToString(CultureInfo.InvariantCulture)`; `bool` → `0`/`1`; enum → целое число. Эти правила используются в задаче 004.
- Внешние ключи с `ON DELETE CASCADE` работают только при включённых внешних ключах: строка подключения содержит `Foreign Keys=True`.
- `PRAGMA user_version = N` нельзя передать параметром; число подставляется в строку. Это единственное исключение из правила «SQL только с параметрами», допустимое потому, что `N` — индекс из кода, а не ввод пользователя.
- Перед удалением временного файла БД в тестах нужно вызвать `SqliteConnection.ClearAllPools()`, иначе файл занят.

## План
1. Создать `ImageGen/ImageGen.Data/ImageGen.Data.csproj`:
   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
       <TargetFramework>net10.0</TargetFramework>
     </PropertyGroup>
     <ItemGroup>
       <PackageReference Include="Microsoft.Data.Sqlite" />
       <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
     </ItemGroup>
     <ItemGroup>
       <ProjectReference Include="../ImageGen.Core/ImageGen.Core.csproj" />
     </ItemGroup>
     <ItemGroup>
       <InternalsVisibleTo Include="ImageGen.Data.Tests" />
     </ItemGroup>
   </Project>
   ```

2. Создать `ImageGen/ImageGen.Data/Migrations.cs`:
   ```csharp
   namespace ImageGen.Data;

   /// <summary>Ordered schema migrations. Never edit a released migration; append a new one.</summary>
   internal static class Migrations
   {
       public static readonly IReadOnlyList<string> All =
       [
           """
           CREATE TABLE sessions (
               id TEXT NOT NULL PRIMARY KEY,
               title TEXT NOT NULL,
               created_at TEXT NOT NULL,
               updated_at TEXT NOT NULL,
               settings_json TEXT NOT NULL
           );

           CREATE TABLE chat_messages (
               id TEXT NOT NULL PRIMARY KEY,
               session_id TEXT NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
               role INTEGER NOT NULL,
               text TEXT NOT NULL,
               created_at TEXT NOT NULL,
               run_id TEXT NULL,
               is_error INTEGER NOT NULL
           );

           CREATE INDEX ix_chat_messages_session ON chat_messages(session_id, created_at);

           CREATE TABLE runs (
               id TEXT NOT NULL PRIMARY KEY,
               session_id TEXT NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
               prompt TEXT NOT NULL,
               origin INTEGER NOT NULL,
               created_at TEXT NOT NULL
           );

           CREATE INDEX ix_runs_session ON runs(session_id, created_at);

           CREATE TABLE generations (
               id TEXT NOT NULL PRIMARY KEY,
               run_id TEXT NOT NULL REFERENCES runs(id) ON DELETE CASCADE,
               session_id TEXT NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
               model_id TEXT NOT NULL,
               prompt TEXT NOT NULL,
               seed INTEGER NOT NULL,
               seed_applied INTEGER NOT NULL,
               status INTEGER NOT NULL,
               image_path TEXT NULL,
               media_type TEXT NULL,
               cost_usd TEXT NULL,
               error_kind INTEGER NULL,
               error_message TEXT NULL,
               created_at TEXT NOT NULL,
               completed_at TEXT NULL,
               sort_order INTEGER NOT NULL
           );

           CREATE INDEX ix_generations_run ON generations(run_id, sort_order);
           CREATE INDEX ix_generations_session ON generations(session_id);
           CREATE INDEX ix_generations_status ON generations(status);

           CREATE TABLE image_models (
               id TEXT NOT NULL PRIMARY KEY,
               name TEXT NOT NULL,
               supports_seed INTEGER NOT NULL,
               supports_image_input INTEGER NOT NULL,
               max_images_per_request INTEGER NOT NULL,
               fetched_at TEXT NOT NULL
           );
           """,
       ];
   }
   ```

3. Создать `ImageGen/ImageGen.Data/SqliteDatabase.cs`:
   ```csharp
   using ImageGen.Core.Platform;
   using Microsoft.Data.Sqlite;

   namespace ImageGen.Data;

   public sealed class SqliteDatabase
   {
       public const string FileName = "imagegen.db";

       private readonly SemaphoreSlim _initLock = new(1, 1);
       private bool _initialized;

       public SqliteDatabase(IAppPaths appPaths)
           : this(Path.Combine(appPaths.DataDirectory, FileName))
       {
       }

       public SqliteDatabase(string databasePath)
       {
           ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
           DatabasePath = databasePath;
           ConnectionString = new SqliteConnectionStringBuilder
           {
               DataSource = databasePath,
               ForeignKeys = true,
           }.ToString();
       }

       public string DatabasePath { get; }

       public string ConnectionString { get; }

       /// <summary>Opens a new connection. Call <see cref="InitializeAsync"/> once before the first use.</summary>
       public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
       {
           var connection = new SqliteConnection(ConnectionString);
           await connection.OpenAsync(cancellationToken);
           return connection;
       }

       /// <summary>Creates the folder and the file if needed and applies pending migrations. Safe to call many times.</summary>
       public async Task InitializeAsync(CancellationToken cancellationToken)
       {
           await _initLock.WaitAsync(cancellationToken);
           try
           {
               if (_initialized)
               {
                   return;
               }

               var directory = Path.GetDirectoryName(DatabasePath);
               if (!string.IsNullOrEmpty(directory))
               {
                   Directory.CreateDirectory(directory);
               }

               await using var connection = await OpenAsync(cancellationToken);

               await using (var wal = connection.CreateCommand())
               {
                   wal.CommandText = "PRAGMA journal_mode = WAL;";
                   await wal.ExecuteNonQueryAsync(cancellationToken);
               }

               var version = await GetSchemaVersionAsync(connection, cancellationToken);
               for (var index = version; index < Migrations.All.Count; index++)
               {
                   await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
                   await using (var command = connection.CreateCommand())
                   {
                       command.Transaction = transaction;
                       command.CommandText = Migrations.All[index];
                       await command.ExecuteNonQueryAsync(cancellationToken);
                   }

                   await using (var command = connection.CreateCommand())
                   {
                       command.Transaction = transaction;
                       command.CommandText = $"PRAGMA user_version = {index + 1};";
                       await command.ExecuteNonQueryAsync(cancellationToken);
                   }

                   await transaction.CommitAsync(cancellationToken);
               }

               _initialized = true;
           }
           finally
           {
               _initLock.Release();
           }
       }

       internal static async Task<int> GetSchemaVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
       {
           await using var command = connection.CreateCommand();
           command.CommandText = "PRAGMA user_version;";
           var result = await command.ExecuteScalarAsync(cancellationToken);
           return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
       }
   }
   ```

4. Создать `ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj`:
   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
       <TargetFramework>net10.0</TargetFramework>
       <OutputType>Exe</OutputType>
       <IsPackable>false</IsPackable>
     </PropertyGroup>
     <ItemGroup>
       <PackageReference Include="Microsoft.NET.Test.Sdk" />
       <PackageReference Include="xunit.v3" />
       <PackageReference Include="xunit.runner.visualstudio" />
       <PackageReference Include="NSubstitute" />
       <PackageReference Include="Microsoft.Extensions.DependencyInjection" />
     </ItemGroup>
     <ItemGroup>
       <Using Include="Xunit" />
       <Using Include="NSubstitute" />
     </ItemGroup>
     <ItemGroup>
       <ProjectReference Include="../ImageGen.Data/ImageGen.Data.csproj" />
     </ItemGroup>
   </Project>
   ```

   Создать `ImageGen/ImageGen.Data.Tests/TestDatabase.cs` — помощник для всех тестов хранилища:
   ```csharp
   using Microsoft.Data.Sqlite;

   namespace ImageGen.Data.Tests;

   /// <summary>Temporary database file that is deleted on dispose.</summary>
   public sealed class TestDatabase : IAsyncDisposable
   {
       private TestDatabase(string directory)
       {
           Directory = directory;
           Database = new SqliteDatabase(Path.Combine(directory, SqliteDatabase.FileName));
       }

       public string Directory { get; }

       public SqliteDatabase Database { get; }

       public static async Task<TestDatabase> CreateAsync(CancellationToken cancellationToken)
       {
           var directory = Path.Combine(Path.GetTempPath(), "imagegen-tests", Guid.NewGuid().ToString("N"));
           var testDatabase = new TestDatabase(directory);
           await testDatabase.Database.InitializeAsync(cancellationToken);
           return testDatabase;
       }

       public ValueTask DisposeAsync()
       {
           SqliteConnection.ClearAllPools();
           if (System.IO.Directory.Exists(Directory))
           {
               System.IO.Directory.Delete(Directory, recursive: true);
           }

           return ValueTask.CompletedTask;
       }
   }
   ```

5. Тесты `ImageGen/ImageGen.Data.Tests/SqliteDatabaseTests.cs`, класс `SqliteDatabaseTests` (во всех вызовах передавать `TestContext.Current.CancellationToken`):
   - `InitializeAsync_NewFile_SetsSchemaVersionToMigrationCount`: после `TestDatabase.CreateAsync` открыть соединение, `SqliteDatabase.GetSchemaVersionAsync` равен `Migrations.All.Count` (сейчас 1).
   - `InitializeAsync_NewFile_CreatesAllTables`: запрос `SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name;` возвращает ровно `chat_messages`, `generations`, `image_models`, `runs`, `sessions`.
   - `InitializeAsync_CalledTwice_DoesNotFail`: второй вызов `InitializeAsync` на том же объекте и на новом объекте `SqliteDatabase` с тем же путём проходит без исключения, версия схемы та же.
   - `OpenAsync_ForeignKeys_AreEnabled`: `PRAGMA foreign_keys;` возвращает `1`.

6. Добавить проекты в решение и CI:
   - `ImageGen/ImageGen.slnx`: добавить строки `<Project Path="ImageGen.Data/ImageGen.Data.csproj" />` и `<Project Path="ImageGen.Data.Tests/ImageGen.Data.Tests.csproj" />` после строки с `ImageGen.Core.Tests`.
   - `.github/workflows/build.yml`, задача `core`: после строки `- run: dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj -c Release --no-build` добавить:
     ```yaml
         - run: dotnet format ImageGen/ImageGen.Data/ImageGen.Data.csproj --verify-no-changes
         - run: dotnet format ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj --verify-no-changes
         - run: dotnet build ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj -c Release
         - run: dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj -c Release --no-build
     ```
     (отступ как у соседних строк `- run:`).

Файлы: `ImageGen/ImageGen.Data/ImageGen.Data.csproj`, `ImageGen/ImageGen.Data/Migrations.cs`, `ImageGen/ImageGen.Data/SqliteDatabase.cs`, `ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj`, `ImageGen/ImageGen.Data.Tests/TestDatabase.cs`, `ImageGen/ImageGen.Data.Tests/SqliteDatabaseTests.cs`, `ImageGen/ImageGen.slnx`, `.github/workflows/build.yml`

### Проверка
```
dotnet format ImageGen/ImageGen.Data/ImageGen.Data.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj --verify-no-changes
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
scripts/check.sh
```

## Отчёт
- Создан проект `ImageGen/ImageGen.Data` (net10.0, `Microsoft.Data.Sqlite`, ссылка на `ImageGen.Core`): `SqliteDatabase` (путь к файлу из `IAppPaths.DataDirectory` или явный путь, `Foreign Keys=True`, `OpenAsync`, `InitializeAsync` с WAL и миграциями в транзакции, `GetSchemaVersionAsync` через `PRAGMA user_version`), `Migrations.cs` с первой миграцией: таблицы `sessions`, `chat_messages`, `runs`, `generations`, `image_models` и индексы. SQL из плана перенесён дословно.
- Создан тестовый проект `ImageGen/ImageGen.Data.Tests` (xUnit v3, NSubstitute): `TestDatabase` (временная БД в каталоге temp, удаляется в `DisposeAsync` после `SqliteConnection.ClearAllPools()`) и `SqliteDatabaseTests` с 4 тестами по плану.
- Оба проекта добавлены в `ImageGen/ImageGen.slnx`. В `.github/workflows/build.yml` в задачу `core` добавлены четыре шага по плану (format для Data и Data.Tests, build и test Data.Tests в Release).
- `ImageGen/Directory.Packages.props` не менялся: все нужные пакеты уже там.
- Проверки: `dotnet format --verify-no-changes` для `ImageGen.Data`, `ImageGen.Data.Tests` и `ImageGen.Core.Tests` прошёл без изменений. `dotnet test --project ImageGen/ImageGen.Data.Tests` прошёл: 4 из 4. `dotnet test --project ImageGen/ImageGen.Core.Tests` прошёл: 10 из 10 (регрессий нет). Release-сборка и `--no-build` тест для Data.Tests прошли, как в CI.
- `docs/architecture.md`, раздел «Состояние кода»: описаны `ImageGen.Data` и `ImageGen.Data.Tests`; уточнено, что ядро содержит доменную модель и интерфейсы (задача 002), а не пустой каркас.
- Отклонений от плана нет. Новых ADR и пакетов не понадобилось.
- `scripts/check.sh` падает по задачам 001 и 002: у них статус `done`, а занятость (`assignee`) не снята. Ошибка есть в исходной ветке, файлы 001 и 002 в этой задаче не менялись. Эти задачи нужно закрыть через `scripts/release-task.sh 001` и `002` отдельно. Для 003 занятость снята по `docs/tasks.md` (шаг «Завершение», п. 4).

## Комментарии
