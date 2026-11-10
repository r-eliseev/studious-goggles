using System.Net;
using System.Text;

namespace ImageGen.Acceptance.Tests.Support;

/// <summary>A deterministic HTTP response for the initial acceptance scenarios.</summary>
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
