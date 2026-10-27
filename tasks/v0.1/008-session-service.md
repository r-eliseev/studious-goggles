---
id: 008
status: planned
assignee:
claimed:
created: 2026-10-27
---

# Ядро: сервис сессий и порядок ленты чата

## Описание
`SessionService`: создать сессию с настройками по умолчанию, список, удалить вместе с файлами, изменить настройки (проверка и очистка списка моделей), задать название по первому сообщению, получить ленту сессии. `TimelineOrdering`: порядок строк чата — сообщения и прогоны по времени, прогон ассистента сразу после его сообщения.

Зависит от: 002.

## Критерии готовности
- Файлы из плана созданы с содержимым как в плане.
- Все тесты `ImageGen.Core.Tests` проходят.
- `dotnet format` для `ImageGen.Core` и `ImageGen.Core.Tests` проходит.

## Анализ
- Спецификация: S1.1–S1.3, S5.1–S5.2 (`docs/specs/chat-sessions.md`).
- Порядок ленты вынесен в ядро, чтобы его можно было проверить тестами без MAUI. Прогон, запущенный ассистентом, создаётся раньше сообщения ассистента, поэтому ставится по ссылке `ChatMessage.RunId`, а не по времени.
- `TimelineEntry` лежит в `Domain/` рядом с остальными типами задачи 002.
- Код проверен сборкой и тестами 2026-10-27 в пробной копии решения.

## План
1. Создать файлы (содержимое дословно):

   `ImageGen/ImageGen.Core/Domain/TimelineEntry.cs`
   ```csharp
   namespace ImageGen.Core.Domain;

   /// <summary>One row of the chat: either a message or a run with its generations.</summary>
   public sealed record TimelineEntry(ChatMessage? Message, Run? Run, IReadOnlyList<Generation> Generations)
   {
       public static TimelineEntry ForMessage(ChatMessage message) => new(message, null, []);

       public static TimelineEntry ForRun(Run run, IReadOnlyList<Generation> generations) => new(null, run, generations);
   }
   ```

   `ImageGen/ImageGen.Core/Services/SessionService.cs`
   ```csharp
   using ImageGen.Core.Domain;
   using ImageGen.Core.Storage;

   namespace ImageGen.Core.Services;

   public sealed class SessionService(
       ISessionRepository sessions,
       IChatMessageRepository messages,
       IRunRepository runs,
       IImageFileStore files,
       TimeProvider timeProvider)
   {
       public async Task<Session> CreateAsync(CancellationToken cancellationToken)
       {
           var now = timeProvider.GetUtcNow();
           var session = new Session
           {
               Id = Guid.NewGuid(),
               Title = Session.NewSessionTitle,
               CreatedAt = now,
               UpdatedAt = now,
               Settings = SessionSettings.CreateDefault(),
           };
           await sessions.AddAsync(session, cancellationToken);
           return session;
       }

       public Task<IReadOnlyList<Session>> ListAsync(CancellationToken cancellationToken) => sessions.ListAsync(cancellationToken);

       public Task<Session?> GetAsync(Guid sessionId, CancellationToken cancellationToken) =>
           sessions.GetAsync(sessionId, cancellationToken);

       /// <summary>Deletes the session, its records and its image files.</summary>
       public async Task DeleteAsync(Guid sessionId, CancellationToken cancellationToken)
       {
           await sessions.DeleteAsync(sessionId, cancellationToken);
           await files.DeleteSessionFilesAsync(sessionId, cancellationToken);
       }

       /// <summary>Validates and saves new settings. Model ids are trimmed and de-duplicated in order.</summary>
       public async Task<Session> UpdateSettingsAsync(Guid sessionId, SessionSettings settings, CancellationToken cancellationToken)
       {
           ArgumentNullException.ThrowIfNull(settings);
           ArgumentOutOfRangeException.ThrowIfLessThan(settings.RunsPerModel, SessionSettings.MinRunsPerModel);
           ArgumentOutOfRangeException.ThrowIfGreaterThan(settings.RunsPerModel, SessionSettings.MaxRunsPerModel);
           ArgumentException.ThrowIfNullOrWhiteSpace(settings.AssistantModelId);

           var session = await sessions.GetAsync(sessionId, cancellationToken)
               ?? throw new InvalidOperationException($"Session {sessionId} not found.");
           session.Settings = new SessionSettings
           {
               ImageModelIds = settings.ImageModelIds
                   .Where(id => !string.IsNullOrWhiteSpace(id))
                   .Select(id => id.Trim())
                   .Distinct(StringComparer.Ordinal)
                   .ToList(),
               RunsPerModel = settings.RunsPerModel,
               AssistantModelId = settings.AssistantModelId.Trim(),
           };
           session.UpdatedAt = timeProvider.GetUtcNow();
           await sessions.UpdateAsync(session, cancellationToken);
           return session;
       }

       /// <summary>Sets the title from the first user message if the session still has the default title.</summary>
       public async Task SetTitleFromFirstMessageAsync(Guid sessionId, string text, CancellationToken cancellationToken)
       {
           var session = await sessions.GetAsync(sessionId, cancellationToken)
               ?? throw new InvalidOperationException($"Session {sessionId} not found.");
           session.UpdatedAt = timeProvider.GetUtcNow();
           if (session.Title == Session.NewSessionTitle)
           {
               var title = text.Trim().ReplaceLineEndings(" ");
               session.Title = title.Length > Session.MaxTitleLength ? title[..Session.MaxTitleLength] : title;
           }

           await sessions.UpdateAsync(session, cancellationToken);
       }

       public async Task<SessionTimeline> GetTimelineAsync(Guid sessionId, CancellationToken cancellationToken)
       {
           var session = await sessions.GetAsync(sessionId, cancellationToken)
               ?? throw new InvalidOperationException($"Session {sessionId} not found.");
           var sessionMessages = await messages.ListBySessionAsync(sessionId, cancellationToken);
           var sessionRuns = await runs.ListRunsBySessionAsync(sessionId, cancellationToken);
           var generations = await runs.ListGenerationsBySessionAsync(sessionId, cancellationToken);
           return new SessionTimeline(session, sessionMessages, sessionRuns, generations);
       }
   }
   ```

   `ImageGen/ImageGen.Core/Services/TimelineOrdering.cs`
   ```csharp
   using ImageGen.Core.Domain;

   namespace ImageGen.Core.Services;

   public static class TimelineOrdering
   {
       /// <summary>
       /// Orders messages and runs by creation time. A run linked to a message (<see cref="ChatMessage.RunId"/>)
       /// is placed right after that message. Generations of a run are ordered by <see cref="Generation.SortOrder"/>.
       /// </summary>
       public static IReadOnlyList<TimelineEntry> Order(SessionTimeline timeline)
       {
           ArgumentNullException.ThrowIfNull(timeline);
           var generationsByRun = timeline.Generations
               .GroupBy(g => g.RunId)
               .ToDictionary(g => g.Key, g => (IReadOnlyList<Generation>)g.OrderBy(x => x.SortOrder).ToList());
           var runsById = timeline.Runs.ToDictionary(r => r.Id);
           var linkedRunIds = timeline.Messages
               .Where(m => m.RunId.HasValue && runsById.ContainsKey(m.RunId.Value))
               .Select(m => m.RunId!.Value)
               .ToHashSet();

           var rows = timeline.Messages
               .Select(m => (Time: m.CreatedAt, Kind: 0, Message: (ChatMessage?)m, Run: (Run?)null))
               .Concat(timeline.Runs
                   .Where(r => !linkedRunIds.Contains(r.Id))
                   .Select(r => (Time: r.CreatedAt, Kind: 1, Message: (ChatMessage?)null, Run: (Run?)r)))
               .OrderBy(x => x.Time)
               .ThenBy(x => x.Kind);

           var result = new List<TimelineEntry>();
           foreach (var row in rows)
           {
               if (row.Message is not null)
               {
                   result.Add(TimelineEntry.ForMessage(row.Message));
                   if (row.Message.RunId is { } runId && runsById.TryGetValue(runId, out var linkedRun))
                   {
                       result.Add(TimelineEntry.ForRun(linkedRun, generationsByRun.GetValueOrDefault(runId, [])));
                   }
               }
               else
               {
                   result.Add(TimelineEntry.ForRun(row.Run!, generationsByRun.GetValueOrDefault(row.Run!.Id, [])));
               }
           }

           return result;
       }
   }
   ```

2. Создать тесты (содержимое дословно):

   `ImageGen/ImageGen.Core.Tests/Services/SessionServiceTests.cs`
   ```csharp
   using ImageGen.Core.Domain;
   using ImageGen.Core.Services;
   using ImageGen.Core.Storage;

   namespace ImageGen.Core.Tests.Services;

   public class SessionServiceTests
   {
       private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

       private readonly ISessionRepository _sessions = Substitute.For<ISessionRepository>();
       private readonly IChatMessageRepository _messages = Substitute.For<IChatMessageRepository>();
       private readonly IRunRepository _runs = Substitute.For<IRunRepository>();
       private readonly IImageFileStore _files = Substitute.For<IImageFileStore>();
       private readonly SessionService _service;

       public SessionServiceTests()
       {
           var time = Substitute.For<TimeProvider>();
           time.GetUtcNow().Returns(Now);
           _service = new SessionService(_sessions, _messages, _runs, _files, time);
       }

       private static Session ExistingSession(string title = Session.NewSessionTitle) => new()
       {
           Id = Guid.NewGuid(),
           Title = title,
           CreatedAt = Now.AddDays(-1),
           UpdatedAt = Now.AddDays(-1),
           Settings = SessionSettings.CreateDefault(),
       };

       [Fact]
       public async Task CreateAsync_NewSession_HasDefaultTitleAndSettings()
       {
           var session = await _service.CreateAsync(TestContext.Current.CancellationToken);

           Assert.Equal(Session.NewSessionTitle, session.Title);
           Assert.Equal(Now, session.CreatedAt);
           Assert.Equal([SessionSettings.DefaultImageModelId], session.Settings.ImageModelIds);
           await _sessions.Received(1).AddAsync(session, Arg.Any<CancellationToken>());
       }

       [Fact]
       public async Task DeleteAsync_DeletesRecordsAndFiles()
       {
           var id = Guid.NewGuid();

           await _service.DeleteAsync(id, TestContext.Current.CancellationToken);

           await _sessions.Received(1).DeleteAsync(id, Arg.Any<CancellationToken>());
           await _files.Received(1).DeleteSessionFilesAsync(id, Arg.Any<CancellationToken>());
       }

       [Fact]
       public async Task UpdateSettingsAsync_DuplicateAndBlankIds_AreRemoved()
       {
           var session = ExistingSession();
           _sessions.GetAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
           var settings = new SessionSettings { ImageModelIds = [" a ", "b", "a", ""], RunsPerModel = 3 };

           var updated = await _service.UpdateSettingsAsync(session.Id, settings, TestContext.Current.CancellationToken);

           Assert.Equal(["a", "b"], updated.Settings.ImageModelIds);
           Assert.Equal(3, updated.Settings.RunsPerModel);
           Assert.Equal(Now, updated.UpdatedAt);
           await _sessions.Received(1).UpdateAsync(session, Arg.Any<CancellationToken>());
       }

       [Theory]
       [InlineData(0)]
       [InlineData(11)]
       public async Task UpdateSettingsAsync_RunsPerModelOutOfRange_Throws(int runs)
       {
           var settings = new SessionSettings { ImageModelIds = ["a"], RunsPerModel = runs };

           await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
               () => _service.UpdateSettingsAsync(Guid.NewGuid(), settings, TestContext.Current.CancellationToken));
       }

       [Fact]
       public async Task SetTitleFromFirstMessageAsync_DefaultTitle_UsesFirst40Characters()
       {
           var session = ExistingSession();
           _sessions.GetAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
           var text = new string('x', 50);

           await _service.SetTitleFromFirstMessageAsync(session.Id, text, TestContext.Current.CancellationToken);

           Assert.Equal(new string('x', 40), session.Title);
           Assert.Equal(Now, session.UpdatedAt);
       }

       [Fact]
       public async Task SetTitleFromFirstMessageAsync_CustomTitle_KeepsTitle()
       {
           var session = ExistingSession("My fox");
           _sessions.GetAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);

           await _service.SetTitleFromFirstMessageAsync(session.Id, "something else", TestContext.Current.CancellationToken);

           Assert.Equal("My fox", session.Title);
       }

       [Fact]
       public async Task GetTimelineAsync_ReturnsAllParts()
       {
           var session = ExistingSession();
           _sessions.GetAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
           _messages.ListBySessionAsync(session.Id, Arg.Any<CancellationToken>()).Returns([]);
           _runs.ListRunsBySessionAsync(session.Id, Arg.Any<CancellationToken>()).Returns([]);
           _runs.ListGenerationsBySessionAsync(session.Id, Arg.Any<CancellationToken>()).Returns([]);

           var timeline = await _service.GetTimelineAsync(session.Id, TestContext.Current.CancellationToken);

           Assert.Same(session, timeline.Session);
           Assert.Empty(timeline.Messages);
       }
   }
   ```

   `ImageGen/ImageGen.Core.Tests/Services/TimelineOrderingTests.cs`
   ```csharp
   using ImageGen.Core.Domain;
   using ImageGen.Core.Services;

   namespace ImageGen.Core.Tests.Services;

   public class TimelineOrderingTests
   {
       private static readonly DateTimeOffset T0 = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
       private static readonly Session TestSession = new()
       {
           Id = Guid.NewGuid(),
           Title = "s",
           CreatedAt = T0,
           UpdatedAt = T0,
           Settings = SessionSettings.CreateDefault(),
       };

       private static ChatMessage Message(int second, string text, Guid? runId = null) => new()
       {
           Id = Guid.NewGuid(),
           SessionId = TestSession.Id,
           Role = ChatRole.User,
           Text = text,
           CreatedAt = T0.AddSeconds(second),
           RunId = runId,
       };

       private static Run Run(int second, string prompt) => new()
       {
           Id = Guid.NewGuid(),
           SessionId = TestSession.Id,
           Prompt = prompt,
           Origin = RunOrigin.Assistant,
           CreatedAt = T0.AddSeconds(second),
       };

       private static Generation Generation(Run run, int sortOrder) => new()
       {
           Id = Guid.NewGuid(),
           RunId = run.Id,
           SessionId = TestSession.Id,
           ModelId = "m",
           Prompt = run.Prompt,
           Seed = sortOrder,
           SeedApplied = true,
           Status = GenerationStatus.Queued,
           CreatedAt = run.CreatedAt,
           SortOrder = sortOrder,
       };

       [Fact]
       public void Order_LinkedRun_IsPlacedRightAfterItsMessage()
       {
           var assistantRun = Run(1, "fox");
           var user = Message(0, "draw a fox");
           var assistant = Message(2, "Prompt: fox", assistantRun.Id);

           var entries = TimelineOrdering.Order(new SessionTimeline(TestSession, [user, assistant], [assistantRun], []));

           Assert.Equal(3, entries.Count);
           Assert.Same(user, entries[0].Message);
           Assert.Same(assistant, entries[1].Message);
           Assert.Same(assistantRun, entries[2].Run);
       }

       [Fact]
       public void Order_UnlinkedRun_IsPlacedByTime()
       {
           var first = Message(0, "a");
           var repeat = Run(5, "fox");
           var later = Message(10, "b");

           var entries = TimelineOrdering.Order(new SessionTimeline(TestSession, [first, later], [repeat], []));

           Assert.Same(first, entries[0].Message);
           Assert.Same(repeat, entries[1].Run);
           Assert.Same(later, entries[2].Message);
       }

       [Fact]
       public void Order_Generations_AreGroupedByRunAndSorted()
       {
           var run = Run(0, "fox");
           var g1 = Generation(run, 1);
           var g0 = Generation(run, 0);

           var entries = TimelineOrdering.Order(new SessionTimeline(TestSession, [], [run], [g1, g0]));

           var entry = Assert.Single(entries);
           Assert.Equal([g0.Id, g1.Id], entry.Generations.Select(g => g.Id));
       }
   }
   ```

Файлы: `ImageGen/ImageGen.Core/Domain/TimelineEntry.cs`, `ImageGen/ImageGen.Core/Services/SessionService.cs`, `ImageGen/ImageGen.Core/Services/TimelineOrdering.cs`, `ImageGen/ImageGen.Core.Tests/Services/SessionServiceTests.cs`, `ImageGen/ImageGen.Core.Tests/Services/TimelineOrderingTests.cs`

### Проверка
```
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
scripts/check.sh
```

## Отчёт

## Комментарии
