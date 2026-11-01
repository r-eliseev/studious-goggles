using System.Text;
using Microsoft.AspNetCore.Http.Features;

namespace ImageGen.Web.Events;

public static class EventsEndpoint
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(20);

    public static IEndpointRouteBuilder MapEventsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/events", StreamAsync);
        return endpoints;
    }

    private static async Task StreamAsync(HttpContext context, EventHub hub)
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache, no-transform";
        context.Response.Headers.Append("X-Accel-Buffering", "no");
        context.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        using var subscription = hub.Subscribe();
        using var heartbeatCancellation = new CancellationTokenSource();
        var reader = subscription.Reader;
        while (!context.RequestAborted.IsCancellationRequested)
        {
            heartbeatCancellation.CancelAfter(HeartbeatInterval);
            try
            {
                using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    context.RequestAborted,
                    heartbeatCancellation.Token);
                if (!await reader.WaitToReadAsync(waitCancellation.Token))
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (heartbeatCancellation.IsCancellationRequested && !context.RequestAborted.IsCancellationRequested)
            {
                await context.Response.WriteAsync(": heartbeat\n\n", context.RequestAborted);
                await context.Response.Body.FlushAsync(context.RequestAborted);
                continue;
            }
            finally
            {
                heartbeatCancellation.CancelAfter(Timeout.InfiniteTimeSpan);
            }

            while (reader.TryRead(out var item))
            {
                await context.Response.WriteAsync($"event: {item.Name}\ndata: {item.Data}\n\n", context.RequestAborted);
                await context.Response.Body.FlushAsync(context.RequestAborted);
            }
        }
    }
}
