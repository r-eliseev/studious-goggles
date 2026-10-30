using System.Globalization;
using System.Net;
using System.Text;

namespace ImageGen.Core.Tests.TestDoubles;

public sealed class StubHttpMessageHandlerTests
{
    [Fact]
    public async Task SendAsync_ParallelBodies_RecordsRequestAndBodyPairsWithoutLoss()
    {
        const int requestCount = 16;
        var contents = Enumerable.Range(0, requestCount)
            .Select(index => new GatedContent(index.ToString(CultureInfo.InvariantCulture)))
            .ToArray();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };

        var requests = contents.Select((content, index) => SendAsync(client, content, index)).ToArray();
        await Task.WhenAll(contents.Select(content => content.SerializationStarted.Task));
        foreach (var content in contents.Reverse())
        {
            content.ReleaseSerialization.TrySetResult();
        }
        await Task.WhenAll(requests);

        Assert.Equal(requestCount, handler.Requests.Count);
        Assert.Equal(requestCount, handler.RequestBodies.Count);
        for (var index = 0; index < requestCount; index++)
        {
            var pathId = handler.Requests[index].RequestUri!.Segments[^1].TrimEnd('/');
            Assert.Equal(pathId, handler.RequestBodies[index]);
        }
    }

    private static async Task SendAsync(HttpClient client, GatedContent content, int index)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, index.ToString(CultureInfo.InvariantCulture))
        {
            Content = content,
        };
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private sealed class GatedContent(string body) : HttpContent
    {
        public TaskCompletionSource SerializationStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseSerialization { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            SerializationStarted.TrySetResult();
            await ReleaseSerialization.Task;
            var bytes = Encoding.UTF8.GetBytes(body);
            await stream.WriteAsync(bytes, TestContext.Current.CancellationToken);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }
    }
}
