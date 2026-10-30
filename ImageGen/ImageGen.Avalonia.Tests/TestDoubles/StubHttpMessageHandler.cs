using System.Net;
using System.Text;

namespace ImageGen.Avalonia.Tests.TestDoubles;

/// <summary>Returns a prepared response and records every request. Same idea as the Core tests' stub.</summary>
public sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    private readonly object _requestGate = new();

    public List<HttpRequestMessage> Requests { get; } = [];

    public static StubHttpMessageHandler Status(HttpStatusCode statusCode) =>
        new(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        });

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (_requestGate)
        {
            Requests.Add(request);
        }

        return Task.FromResult(respond(request));
    }
}
