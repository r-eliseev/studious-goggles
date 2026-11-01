using System.Text.Json;
using System.Threading.Channels;
using ImageGen.Core.Services;

namespace ImageGen.Web.Events;

public sealed class EventHub : IDisposable
{
    private const int Capacity = 256;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly object _gate = new();
    private readonly HashSet<Subscriber> _subscribers = [];
    private readonly GenerationService _generations;
    private readonly SessionChangeNotifier _sessions;
    private bool _disposed;

    public EventHub(GenerationService generations, SessionChangeNotifier sessions)
    {
        _generations = generations;
        _sessions = sessions;
        _generations.GenerationUpdated += OnGenerationUpdated;
        _sessions.SessionChanged += OnSessionChanged;
    }

    public Subscription Subscribe()
    {
        var subscriber = new Subscriber();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _subscribers.Add(subscriber);
        }

        return new Subscription(this, subscriber);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (var subscriber in _subscribers)
            {
                subscriber.Channel.Writer.TryComplete();
            }

            _subscribers.Clear();
        }

        _generations.GenerationUpdated -= OnGenerationUpdated;
        _sessions.SessionChanged -= OnSessionChanged;
    }

    private void OnGenerationUpdated(object? sender, GenerationUpdatedEventArgs args)
    {
        Publish("generation", new GenerationEvent(args.Generation.Id, args.Generation.SessionId, args.Generation.Status.ToString()));
    }

    private void OnSessionChanged(object? sender, Guid sessionId) => Publish("session", new SessionEvent(sessionId));

    private void Publish<T>(string name, T payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        Subscriber[] subscribers;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            subscribers = [.. _subscribers];
        }

        foreach (var subscriber in subscribers)
        {
            lock (subscriber)
            {
                if (subscriber.NeedsResync)
                {
                    if (subscriber.Channel.Writer.TryWrite(new WebEvent("resync", "{}")))
                    {
                        subscriber.NeedsResync = false;
                    }
                }

                if (!subscriber.Channel.Writer.TryWrite(new WebEvent(name, json)))
                {
                    while (subscriber.Channel.Reader.TryRead(out _))
                    {
                    }

                    subscriber.NeedsResync = true;
                }
            }
        }
    }

    private void Unsubscribe(Subscriber subscriber)
    {
        lock (_gate)
        {
            _subscribers.Remove(subscriber);
        }

        lock (subscriber)
        {
            subscriber.Channel.Writer.TryComplete();
        }
    }

    public sealed class Subscription : IDisposable
    {
        private EventHub? _owner;
        private readonly Subscriber _subscriber;

        internal Subscription(EventHub owner, Subscriber subscriber)
        {
            _owner = owner;
            _subscriber = subscriber;
        }

        public ChannelReader<WebEvent> Reader => _subscriber.Channel.Reader;

        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Unsubscribe(_subscriber);
    }

    public sealed record WebEvent(string Name, string Data);

    internal sealed class Subscriber
    {
        public Channel<WebEvent> Channel { get; } = System.Threading.Channels.Channel.CreateBounded<WebEvent>(new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });

        public bool NeedsResync { get; set; }
    }

    private sealed record GenerationEvent(Guid GenerationId, Guid SessionId, string Status);

    private sealed record SessionEvent(Guid SessionId);
}
