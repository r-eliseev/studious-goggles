using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Sessions;

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
