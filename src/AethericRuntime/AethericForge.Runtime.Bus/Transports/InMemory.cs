using System.Collections.Concurrent;
using AethericForge.Runtime.Bus.Abstractions;

namespace AethericForge.Runtime.Bus.Transports;

/// <summary>
/// A simple in-memory transport implementing topic-style routing with '.' separators.
/// Supports '*' to match a single segment and '#' to match zero or more segments.
/// </summary>
public class InMemoryTransport : ITransport
{
    private readonly ConcurrentDictionary<string, List<EnvelopeHandler>> _routes = new();
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

    public Task SubscribeAsync(string pattern, EnvelopeHandler handler, CancellationToken ct = default)
    {
        var list = _routes.GetOrAdd(pattern, _ => new List<EnvelopeHandler>());
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
        
        string routingKey = envelope.Kind switch 
        {
            "request" => $"{envelope.Service}.commands",
            "error" => $"{envelope.Service}.errors",
            "event" => $"{envelope.Service}.events",
            _ => throw new ArgumentException($"Unknown envelope kind {envelope.Kind}")
        };

        var handlers = CollectHandlers(routingKey);
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

    private List<EnvelopeHandler> CollectHandlers(string routingKey)
    {
        var result = new List<EnvelopeHandler>();
        foreach (var kv in _routes)
        {
            var list = kv.Value;
            bool match = TopicMatch(kv.Key, routingKey);
            if (!match) continue;
            lock (list)
            {
                result.AddRange(list);
            }
        }
        return result;
    }

    // Topic-style matcher with '.'-separated segments.
    // '*' matches a single segment, '#' matches zero or more segments.
    private static bool TopicMatch(string pattern, string key)
    {
        if (pattern == "#") return true;
        var p = pattern.Split('.');
        var k = key.Split('.');

        int i = 0, j = 0;
        while (i < p.Length && j < k.Length)
        {
            var ps = p[i];
            if (ps == "#")
            {
                // consume rest of key
                return true;
            }
            if (ps == "*" || ps == k[j])
            {
                i++; j++;
                continue;
            }
            return false;
        }

        // if key remains but pattern ended (without '#') → no match
        if (j < k.Length && (i >= p.Length || p[^1] != "#")) return false;

        // allow trailing '#' in pattern
        if (i < p.Length)
        {
            if (p[i] == "#" && i == p.Length - 1) return true;
        }

        return i == p.Length && j == k.Length;
    }
}
