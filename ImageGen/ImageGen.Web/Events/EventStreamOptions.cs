namespace ImageGen.Web.Events;

public sealed class EventStreamOptions
{
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(20);
}
