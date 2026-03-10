using System.Collections.Concurrent;
using AethericForge.Runtime.Bus.Abstractions;

namespace AethericForge.Runtime.Bus.Transports;

/// <summary>
/// A simple in-memory transport implementing topic-style routing with '.' separators.
/// Supports '*' to match a single segment and '#' to match zero or more segments.
/// </summary>
public class InMemoryTransport : ITransport
{
    private readonly ConcurrentDictionary<RouteKey, List<EnvelopeHandler>> _routes = new();
    private volatile bool _started;

    public Task StartAsync(CancellationToken ct = default)
    {
        _started = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        _started = false;
        return Task.CompletedTask;
    }

    public Task SubscribeAsync(RouteKey routeKey, EnvelopeHandler handler, CancellationToken ct = default)
    {
        var list = _routes.GetOrAdd(routeKey, _ => new List<EnvelopeHandler>());
        lock (list)
        {
            list.Add(handler);
        }
        return Task.CompletedTask;
    }

    public Task PublishAsync(Envelope envelope, CancellationToken ct = default)
    {
        if (!_started)
            throw new InvalidOperationException("Transport not started");

        var handlers = CollectHandlers(envelope.RouteKey);
        if (handlers.Count == 0) return Task.CompletedTask;

        // invoke sequentially to keep deterministic behavior for tests
        return InvokeAll(handlers, envelope, ct);
    }

    private static async Task InvokeAll(List<EnvelopeHandler> handlers, Envelope envelope, CancellationToken ct = default)
    {
        foreach (var h in handlers)
        {
            await h(envelope, ct);
        }
    }

    private List<EnvelopeHandler> CollectHandlers(RouteKey routingKey)
    {
        var result = new List<EnvelopeHandler>();
        foreach (var kv in _routes)
        {
            var list = kv.Value;
            bool match = routingKey == kv.Key;
            if (!match) continue;
            lock (list)
            {
                result.AddRange(list);
            }
        }
        return result;
    }
}
